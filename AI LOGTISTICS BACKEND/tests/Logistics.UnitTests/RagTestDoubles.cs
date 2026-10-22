using Logistics.Application.AI.RAG;

namespace Logistics.UnitTests;

internal static class RagTestDoubles
{
    public static EmbeddingVector Vector(params float[] values) => new("fixture-model:v1", values);

    public static EmbeddedChunk Entry(string documentId, int index, string text, params float[] vector) =>
        new(new($"{documentId}:{index}", documentId, index, text, index * 10, index * 10 + text.Length, documentId + ".txt"), Vector(vector));

    public sealed class Embeddings(Func<string, CancellationToken, Task<EmbeddingVector>>? handler = null) : IEmbeddingService
    {
        public List<(string Text, CancellationToken Token)> Calls { get; } = [];
        public Func<string, CancellationToken, Task<EmbeddingVector>> Handler { get; set; } =
            handler ?? ((_, _) => Task.FromResult(Vector(1, 0)));

        public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            Calls.Add((text, cancellationToken));
            return Handler(text, cancellationToken);
        }
    }

    public sealed class Store : IVectorStore
    {
        public List<(VectorSearchRequest Request, CancellationToken Token)> Searches { get; } = [];
        public List<(string Collection, string Document, IReadOnlyList<EmbeddedChunk> Chunks, CancellationToken Token)> Replacements { get; } = [];
        public Func<VectorSearchRequest, CancellationToken, Task<IReadOnlyList<RetrievedPassage>>> Handler { get; set; } =
            (_, _) => Task.FromResult<IReadOnlyList<RetrievedPassage>>([]);

        public Task<IReadOnlyList<RetrievedPassage>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default)
        {
            Searches.Add((request, cancellationToken));
            return Handler(request, cancellationToken);
        }

        public Task ReplaceDocumentAsync(string collectionId, string documentId, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken = default)
        {
            Replacements.Add((collectionId, documentId, chunks, cancellationToken));
            return Task.CompletedTask;
        }
    }
}
