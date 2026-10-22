namespace Logistics.Application.AI.RAG;

public interface IRetrievalService
{
    /// <summary>Embeds a question and retrieves its relevant source passages; does not generate a chat response.</summary>
    Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalRequest request,
        CancellationToken cancellationToken = default);
}
