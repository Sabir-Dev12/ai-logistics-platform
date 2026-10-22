namespace Logistics.Application.AI.RAG;

public sealed record ChunkingOptions
{
    public int ChunkSize { get; init; } = 1000;
    public int Overlap { get; init; } = 150;
    public int MaximumChunks { get; init; } = 10_000;

    public void Validate()
    {
        if (ChunkSize < 2) throw new ArgumentOutOfRangeException(nameof(ChunkSize));
        if (Overlap < 0 || Overlap >= ChunkSize) throw new ArgumentOutOfRangeException(nameof(Overlap));
        if (MaximumChunks < 1) throw new ArgumentOutOfRangeException(nameof(MaximumChunks));
    }
}
