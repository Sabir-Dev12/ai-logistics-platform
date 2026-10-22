namespace Logistics.Application.AI.RAG;

public interface IChunkingService
{
    IReadOnlyList<DocumentChunk> Chunk(RagDocument document, CancellationToken cancellationToken = default);
}
