using Logistics.Application.Abstractions;

namespace Logistics.Application.AI.RAG;

public sealed record KnowledgeDocument(string Id, string Name, string Type, long Size, DateTimeOffset Date,
    string Status, int ChunkCount = 0, string? ModelId = null, string? ErrorCode = null);
public sealed record StoredKnowledgeDocument(KnowledgeDocument Metadata, string? Text, IReadOnlyList<EmbeddedChunk> Chunks);
public sealed record KnowledgeUpload(string Name, byte[] Content);
public sealed record KnowledgeSearch(string? Search = null, string? Type = null, string? Status = null, int Page = 1, int PageSize = 20);

public interface IKnowledgeDocumentStore
{
    Task<IReadOnlyList<StoredKnowledgeDocument>> ListAsync(string collection, CancellationToken token = default);
    Task<StoredKnowledgeDocument> GetAsync(string collection, string id, CancellationToken token = default);
    Task CreateAsync(string collection, StoredKnowledgeDocument document, CancellationToken token = default);
    Task CreateManyAsync(string collection, IReadOnlyList<StoredKnowledgeDocument> documents, CancellationToken token = default);
    Task UpdateAsync(string collection, string id, Func<StoredKnowledgeDocument, StoredKnowledgeDocument> update, CancellationToken token = default);
    Task DeleteAsync(string collection, string id, CancellationToken token = default);
}
public interface IKnowledgeTextExtractor { Task<string> ExtractAsync(KnowledgeUpload upload, CancellationToken token = default); }
public interface IKnowledgeBaseService
{
    Task<PagedResult<KnowledgeDocument>> ListAsync(string collection, KnowledgeSearch search, CancellationToken token = default);
    Task<KnowledgeDocument> GetAsync(string collection, string id, CancellationToken token = default);
    Task<IReadOnlyList<KnowledgeDocument>> UploadAsync(string collection, IReadOnlyList<KnowledgeUpload> files, CancellationToken token = default);
    Task<KnowledgeDocument> ReindexAsync(string collection, string id, CancellationToken token = default);
    Task DeleteAsync(string collection, string id, CancellationToken token = default);
}
public sealed class KnowledgeDocumentException(string code) : Exception("Document processing failed.") { public string Code { get; } = code; }
public sealed class KnowledgeBaseOptions
{
    public long MaximumFileBytes { get; set; } = 10 * 1024 * 1024;
    public long MaximumCollectionBytes { get; set; } = 100 * 1024 * 1024;
    public int MaximumDocuments { get; set; } = 500;
    public int MaximumBatchFiles { get; set; } = 10;
    public string DataDirectory { get; set; } = "App_Data/rag";
}
