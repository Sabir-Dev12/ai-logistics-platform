using Logistics.Application.AI.RAG;
using Logistics.Infrastructure.AI.RAG;
using static Logistics.UnitTests.RagTestDoubles;

namespace Logistics.UnitTests;

public sealed class RagRetrievalTests
{
    [Fact]
    public async Task Embeds_the_question_and_returns_passages_with_scores_and_sources()
    {
        var vector = Vector(1, 0);
        var embeddings = new Embeddings((_, _) => Task.FromResult(vector));
        IReadOnlyList<RetrievedPassage> expected = [new(Entry("guide", 0, "Livraison sous trois jours.", 1, 0).Chunk, 0.95)];
        var store = new Store { Handler = (_, _) => Task.FromResult(expected) };
        IRetrievalService service = new RetrievalService(embeddings, store);
        using var source = new CancellationTokenSource();
        const string question = "  Quel est le délai de livraison ?\n";
        var results = await service.RetrieveAsync(new("S01", question, 3, 0.8), source.Token);
        Assert.Same(expected, results);
        Assert.Equal("guide.txt", results[0].Chunk.SourceName);
        Assert.Equal(question, Assert.Single(embeddings.Calls).Text);
        Assert.Equal(source.Token, embeddings.Calls[0].Token);
        var search = Assert.Single(store.Searches);
        Assert.Equal("S01", search.Request.CollectionId);
        Assert.Same(vector, search.Request.Query);
        Assert.Equal(3, search.Request.TopK);
        Assert.Equal(0.8, search.Request.MinimumScore);
        Assert.Equal(source.Token, search.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Blank_questions_do_not_call_embeddings_or_storage(string? question)
    {
        var embeddings = new Embeddings();
        var store = new Store();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new RetrievalService(embeddings, store).RetrieveAsync(new("S01", question!)));
        Assert.Empty(embeddings.Calls);
        Assert.Empty(store.Searches);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1001, 0)]
    [InlineData(5, -2)]
    [InlineData(5, 2)]
    [InlineData(5, double.PositiveInfinity)]
    public async Task Invalid_search_options_are_rejected_before_embedding(int topK, double score)
    {
        var embeddings = new Embeddings();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new RetrievalService(embeddings, new Store()).RetrieveAsync(new("S01", "Question", topK, score)));
        Assert.Empty(embeddings.Calls);
    }

    [Fact]
    public async Task Invalid_requests_and_missing_dependencies_are_rejected()
    {
        var embeddings = new Embeddings();
        var store = new Store();
        var service = new RetrievalService(embeddings, store);
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RetrieveAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RetrieveAsync(new(" ", "Question")));
        Assert.Throws<ArgumentNullException>(() => new RetrievalService(null!, store));
        Assert.Throws<ArgumentNullException>(() => new RetrievalService(embeddings, null!));
        Assert.Empty(embeddings.Calls);
    }

    [Fact]
    public async Task An_empty_search_does_not_fabricate_a_passage()
    {
        var results = await new RetrievalService(new Embeddings(), new Store()).RetrieveAsync(new("S01", "Question"));
        Assert.Empty(results);
    }

    [Fact]
    public async Task Provider_and_storage_failures_are_propagated()
    {
        var failure = new IOException("Embedding service unavailable");
        var store = new Store();
        var provider = new Embeddings((_, _) => Task.FromException<EmbeddingVector>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => new RetrievalService(provider, store).RetrieveAsync(new("S01", "Question"))));
        Assert.Empty(store.Searches);
        store.Handler = (_, _) => Task.FromException<IReadOnlyList<RetrievedPassage>>(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => new RetrievalService(new Embeddings(), store).RetrieveAsync(new("S01", "Question"))));
    }

    [Fact]
    public async Task Null_provider_or_store_outputs_are_explicit_errors()
    {
        var store = new Store();
        var embeddings = new Embeddings((_, _) => Task.FromResult<EmbeddingVector>(null!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RetrievalService(embeddings, store).RetrieveAsync(new("S01", "Question")));
        Assert.Empty(store.Searches);
        store.Handler = (_, _) => Task.FromResult<IReadOnlyList<RetrievedPassage>>(null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RetrievalService(new Embeddings(), store).RetrieveAsync(new("S01", "Question")));
    }

    [Fact]
    public async Task Precancelled_retrieval_does_not_call_dependencies()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var embeddings = new Embeddings();
        var store = new Store();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RetrievalService(embeddings, store).RetrieveAsync(new("S01", "Question"), source.Token));
        Assert.Empty(embeddings.Calls);
        Assert.Empty(store.Searches);
    }

    [Fact]
    public async Task Cancellation_during_embedding_reaches_the_provider()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new CancellationTokenSource();
        var provider = new Embeddings(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Vector(1, 0);
        });
        var store = new Store();
        var retrieve = new RetrievalService(provider, store).RetrieveAsync(new("S01", "Question"), source.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retrieve);
        Assert.Empty(store.Searches);
    }

    [Fact]
    public async Task Ignores_late_vectors_or_passages_after_cancellation()
    {
        using var source = new CancellationTokenSource();
        var provider = new Embeddings((_, _) =>
        {
            source.Cancel();
            return Task.FromResult(Vector(1, 0));
        });
        var store = new Store();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RetrievalService(provider, store).RetrieveAsync(new("S01", "Question"), source.Token));
        Assert.Empty(store.Searches);
        using var storeCancellation = new CancellationTokenSource();
        store.Handler = (_, _) =>
        {
            storeCancellation.Cancel();
            return Task.FromResult<IReadOnlyList<RetrievedPassage>>([new(Entry("doc", 0, "Late", 1, 0).Chunk, 1)]);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RetrievalService(new Embeddings(), store).RetrieveAsync(new("S01", "Question"), storeCancellation.Token));
    }

    [Fact]
    public async Task Retrieves_relevant_indexed_text_and_preserves_citations_without_a_chat_dependency()
    {
        var vectors = new InMemoryVectorStore();
        // Controlled semantic fixture: delivery and billing occupy distinct axes. This is not a production embedding provider.
        var provider = new Embeddings((text, _) => Task.FromResult(text.Contains("livraison", StringComparison.OrdinalIgnoreCase) ? Vector(1, 0) : Vector(0, 1)));
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(), provider, vectors);
        using var delivery = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Le délai de livraison est de trois jours."));
        using var billing = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("La facture est payable sous trente jours."));
        await indexer.IndexAsync("S01", new("delivery", delivery, "delivery.txt"));
        await indexer.IndexAsync("S01", new("billing", billing, "billing.txt"));
        var retrieval = new RetrievalService(provider, vectors);
        var results = await retrieval.RetrieveAsync(new("S01", "Quel est le délai de livraison ?", 5, 0.5));
        var passage = Assert.Single(results);
        Assert.Equal("Le délai de livraison est de trois jours.", passage.Chunk.Text);
        Assert.Equal("delivery", passage.Chunk.DocumentId);
        Assert.Equal("delivery.txt", passage.Chunk.SourceName);
        Assert.Equal(1, passage.Score, 12);
        Assert.Empty(await retrieval.RetrieveAsync(new("S02", "Quel est le délai de livraison ?", 5, 0.5)));
    }
}
