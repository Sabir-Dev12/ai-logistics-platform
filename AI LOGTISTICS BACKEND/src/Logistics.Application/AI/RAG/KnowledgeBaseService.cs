using System.Globalization;
using System.Text;
using Logistics.Application.Abstractions;
using Logistics.Domain.Common;

namespace Logistics.Application.AI.RAG;

public sealed class KnowledgeBaseService(IKnowledgeDocumentStore store, IKnowledgeTextExtractor extractor,
    IDocumentIndexingService indexing, KnowledgeBaseOptions options) : IKnowledgeBaseService
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal) { "PDF", "DOCX", "TXT", "MD", "CSV" };
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "indexed", "processing", "failed" };
    public async Task<PagedResult<KnowledgeDocument>> ListAsync(string collection, KnowledgeSearch search, CancellationToken token = default)
    {
        Paging.Valider(search.Page, search.PageSize);
        var type = search.Type?.ToUpperInvariant();
        if ((!string.IsNullOrEmpty(type) && !Types.Contains(type)) || (!string.IsNullOrEmpty(search.Status) && !Statuses.Contains(search.Status)))
            throw new BusinessRuleException("documents.filter", "Filtre documentaire invalide.");
        var query = Normalize(search.Search ?? "");
        var rows = (await store.ListAsync(collection, token)).Select(row => row.Metadata)
            .Where(row => Normalize(row.Name).Contains(query, StringComparison.Ordinal) && (string.IsNullOrEmpty(type) || row.Type == type)
                && (string.IsNullOrEmpty(search.Status) || row.Status == search.Status))
            .OrderByDescending(row => row.Date).ThenBy(row => row.Id, StringComparer.Ordinal).ToArray();
        return new(rows.Skip((int)Math.Min((long)(search.Page - 1) * search.PageSize, rows.Length)).Take(search.PageSize).ToArray(), search.Page, search.PageSize, rows.Length);
    }
    public async Task<KnowledgeDocument> GetAsync(string collection, string id, CancellationToken token = default) => (await store.GetAsync(collection, id, token)).Metadata;
    public async Task<IReadOnlyList<KnowledgeDocument>> UploadAsync(string collection, IReadOnlyList<KnowledgeUpload> files, CancellationToken token = default)
    {
        if (files.Count is < 1 || files.Count > options.MaximumBatchFiles) throw new BusinessRuleException("documents.files", "Sélectionnez entre 1 et 10 fichiers.");
        // Validate the batch before accepting any document.
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.Name).TrimStart('.').ToUpperInvariant();
            if (!Types.Contains(extension) || string.IsNullOrWhiteSpace(file.Name) || file.Name.Length > 200 || file.Name.IndexOfAny(['/', '\\', '\0']) >= 0)
                throw new BusinessRuleException("documents.format", "Formats autorisés : PDF, DOCX, TXT, MD, CSV ; nom de fichier simple requis.");
            if (file.Content.LongLength is < 1 || file.Content.LongLength > options.MaximumFileBytes)
                throw new BusinessRuleException("documents.size", "Fichier vide ou supérieur à la taille maximale autorisée.");
        }
        var results = new List<KnowledgeDocument>();
        var accepted = files.Select(file => new StoredKnowledgeDocument(new(Guid.NewGuid().ToString("N"), file.Name,
            Path.GetExtension(file.Name)[1..].ToUpperInvariant(), file.Content.LongLength, DateTimeOffset.UtcNow, "processing"), null, [])).ToArray();
        await store.CreateManyAsync(collection, accepted, token);
        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var file = files[index]; var id = accepted[index].Metadata.Id;
                try
                {
                    var text = await extractor.ExtractAsync(file, token);
                    await store.UpdateAsync(collection, id, current => current with { Text = text }, token);
                    await Index(collection, id, text, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { await Failed(collection, id, "indexing_cancelled"); throw; }
                catch (EntityNotFoundException) { throw; } // Concurrent deletion cannot resurrect a document.
                catch (Exception error) { await Failed(collection, id, error is KnowledgeDocumentException documentError ? documentError.Code : "indexing_failed"); }
                results.Add(await GetAsync(collection, id, token));
            }
        }
        catch
        {
            foreach (var document in accepted)
            {
                try { if ((await store.GetAsync(collection, document.Metadata.Id, CancellationToken.None)).Metadata.Status == "processing") await Failed(collection, document.Metadata.Id, "indexing_cancelled"); }
                catch (EntityNotFoundException) { }
            }
            throw;
        }
        return results;
    }
    public async Task<KnowledgeDocument> ReindexAsync(string collection, string id, CancellationToken token = default)
    {
        await store.UpdateAsync(collection, id, current =>
        {
            if (current.Metadata.Status == "processing") throw new BusinessRuleException("documents.processing", "Indexation déjà en cours.");
            if (string.IsNullOrWhiteSpace(current.Text)) throw new BusinessRuleException("documents.text", "Aucun texte exploitable ; importez un document corrigé.");
            return current with { Metadata = current.Metadata with { Status = "processing", ErrorCode = null, ChunkCount = 0, ModelId = null } };
        }, token);
        var document = await store.GetAsync(collection, id, token);
        try { await Index(collection, id, document.Text!, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { await Failed(collection, id, "indexing_cancelled"); throw; }
        catch (EntityNotFoundException) { throw; }
        catch { await Failed(collection, id, "indexing_failed"); }
        return await GetAsync(collection, id, token);
    }
    public Task DeleteAsync(string collection, string id, CancellationToken token = default) => store.DeleteAsync(collection, id, token);
    private async Task Index(string collection, string id, string text, CancellationToken token)
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var sourceName = (await store.GetAsync(collection, id, token)).Metadata.Name;
        var result = await indexing.IndexAsync(collection, new(id, content, sourceName), token);
        await store.UpdateAsync(collection, id, current => current with { Metadata = current.Metadata with { Status = "indexed", ErrorCode = null, ModelId = result.ModelId, ChunkCount = result.ChunkCount } }, token);
    }
    private async Task Failed(string collection, string id, string code)
    {
        try { await store.UpdateAsync(collection, id, current => current with { Metadata = current.Metadata with { Status = "failed", ErrorCode = code, ChunkCount = 0, ModelId = null }, Chunks = [] }, CancellationToken.None); }
        catch (EntityNotFoundException) { } // Deleted while processing.
    }
    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant().Trim();
}
