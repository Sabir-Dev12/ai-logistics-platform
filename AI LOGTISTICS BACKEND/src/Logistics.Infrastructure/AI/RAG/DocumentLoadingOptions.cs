namespace Logistics.Infrastructure.AI.RAG;

public sealed record DocumentLoadingOptions
{
    public int MaximumCharacters { get; init; } = 1_000_000;

    public void Validate()
    {
        if (MaximumCharacters < 1 || MaximumCharacters == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(MaximumCharacters));
    }
}
