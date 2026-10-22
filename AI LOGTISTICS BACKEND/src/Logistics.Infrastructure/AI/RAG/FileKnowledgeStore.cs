using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Logistics.Application.AI.RAG;
using Logistics.Domain.Common;

namespace Logistics.Infrastructure.AI.RAG;

/// <summary>Single-process, durable per-company metadata/text/vectors with atomic file replacement.</summary>
public sealed class FileKnowledgeStore(KnowledgeBaseOptions options) : IKnowledgeDocumentStore, IVectorStore
{
    private readonly string _root = Path.GetFullPath(options.DataDirectory);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _seenCollections = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string PathFor(string collection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        return Path.Combine(_root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(collection))) + ".json");
    }
    private async Task<T> Read<T>(string collection, Func<List<StoredKnowledgeDocument>, T> action, CancellationToken token)
    {
        var gate = _locks.GetOrAdd(collection, _ => new(1)); await gate.WaitAsync(token);
        try { return action(await Load(collection, token)); } finally { gate.Release(); }
    }
    private async Task Mutate(string collection, Action<List<StoredKnowledgeDocument>> action, CancellationToken token)
    {
        var gate = _locks.GetOrAdd(collection, _ => new(1)); await gate.WaitAsync(token);
        try
        {
            var documents = await Load(collection, token); action(documents); token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_root);
            var path = PathFor(collection); var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(documents, Json), token); token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { gate.Release(); }
    }
    private async Task<List<StoredKnowledgeDocument>> Load(string collection, CancellationToken token)
    {
        var path = PathFor(collection);
        var firstRead = _seenCollections.TryAdd(collection, true);
        if (!File.Exists(path)) return [];
        var documents = JsonSerializer.Deserialize<List<StoredKnowledgeDocument>>(await File.ReadAllTextAsync(path, token), Json)
            ?? throw new InvalidDataException("Invalid knowledge store.");
        if (firstRead && documents.Any(document => document.Metadata.Status == "processing"))
        {
            documents = documents.Select(document => document.Metadata.Status == "processing"
                ? document with { Metadata = document.Metadata with { Status = "failed", ErrorCode = "indexing_interrupted", ChunkCount = 0, ModelId = null }, Chunks = [] } : document).ToList();
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(documents, Json), token); token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return documents;
    }
    private static int Find(List<StoredKnowledgeDocument> documents, string id)
    {
        var index = documents.FindIndex(document => document.Metadata.Id == id);
        if (index < 0) throw new EntityNotFoundException("Document", id);
        return index;
    }
    public Task<IReadOnlyList<StoredKnowledgeDocument>> ListAsync(string collection, CancellationToken token = default)
        => Read<IReadOnlyList<StoredKnowledgeDocument>>(collection, documents => documents.ToArray(), token);
    public Task<StoredKnowledgeDocument> GetAsync(string collection, string id, CancellationToken token = default)
        => Read(collection, documents => documents[Find(documents, id)], token);
    public Task CreateAsync(string collection, StoredKnowledgeDocument document, CancellationToken token = default)
        => CreateManyAsync(collection, [document], token);
    public Task CreateManyAsync(string collection, IReadOnlyList<StoredKnowledgeDocument> additions, CancellationToken token = default)
        => Mutate(collection, documents =>
        {
            if (documents.Count + additions.Count > options.MaximumDocuments || documents.Sum(row => row.Metadata.Size) + additions.Sum(row => row.Metadata.Size) > options.MaximumCollectionBytes)
                throw new BusinessRuleException("documents.quota", "Limite documentaire de la société atteinte.");
            if (additions.Select(row => row.Metadata.Id).Distinct(StringComparer.Ordinal).Count() != additions.Count || additions.Any(addition => documents.Any(row => row.Metadata.Id == addition.Metadata.Id)))
                throw new BusinessRuleException("documents.id", "Document déjà présent.");
            documents.AddRange(additions);
        }, token);
    public Task UpdateAsync(string collection, string id, Func<StoredKnowledgeDocument, StoredKnowledgeDocument> update, CancellationToken token = default)
        => Mutate(collection, documents => { var index = Find(documents, id); documents[index] = update(documents[index]); }, token);
    public Task DeleteAsync(string collection, string id, CancellationToken token = default)
        => Mutate(collection, documents => documents.RemoveAt(Find(documents, id)), token);
    public Task ReplaceDocumentAsync(string collectionId, string documentId, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken = default)
        => Mutate(collectionId, documents =>
        {
            var index = Find(documents, documentId);
            // Reuse the existing validation and model/dimension compatibility rules.
            var validator = new InMemoryVectorStore();
            foreach (var document in documents.Where(document => document.Metadata.Id != documentId && document.Metadata.Status == "indexed" && document.Chunks.Count > 0))
                validator.ReplaceDocumentAsync(collectionId, document.Metadata.Id, document.Chunks, cancellationToken).GetAwaiter().GetResult();
            validator.ReplaceDocumentAsync(collectionId, documentId, chunks, cancellationToken).GetAwaiter().GetResult();
            documents[index] = documents[index] with { Chunks = chunks.ToArray() };
        }, cancellationToken);
    public async Task<IReadOnlyList<RetrievedPassage>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default)
    {
        var documents = await ListAsync(request.CollectionId, cancellationToken);
        var index = new InMemoryVectorStore();
        foreach (var document in documents.Where(document => document.Metadata.Status == "indexed" && document.Chunks.Count > 0))
            await index.ReplaceDocumentAsync(request.CollectionId, document.Metadata.Id, document.Chunks, cancellationToken);
        return await index.SearchAsync(request, cancellationToken);
    }
}
