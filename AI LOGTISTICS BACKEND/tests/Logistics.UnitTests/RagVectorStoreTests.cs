using Logistics.Application.AI.RAG;
using Logistics.Infrastructure.AI.RAG;
using static Logistics.UnitTests.RagTestDoubles;

namespace Logistics.UnitTests;

public sealed class RagVectorStoreTests
{
    [Fact]
    public async Task Ranks_by_cosine_similarity_and_respects_topk_and_the_threshold()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("S01", "procedure", [
            Entry("procedure", 0, "Livraison", 20, 0), Entry("procedure", 1, "Douane", 1, 1),
            Entry("procedure", 2, "Facture", 0, 1), Entry("procedure", 3, "Inverse", -1, 0)]);
        var results = await store.SearchAsync(new("S01", Vector(1, 0), 2, 0.5));
        Assert.Equal(new[] { "Livraison", "Douane" }, results.Select(passage => passage.Chunk.Text));
        Assert.Equal(1, results[0].Score, 12);
        Assert.Equal(1 / Math.Sqrt(2), results[1].Score, 12);
        Assert.Single(await store.SearchAsync(new("S01", Vector(1, 0), 5, 0.9)));
        Assert.Equal(-1, (await store.SearchAsync(new("S01", Vector(1, 0), 5, -1)))[^1].Score, 12);
    }

    [Fact]
    public async Task Separates_collections_even_for_identical_document_and_chunk_ids()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("S01", "doc", [Entry("doc", 0, "S01 private", 1, 0)]);
        await store.ReplaceDocumentAsync("S02", "doc", [Entry("doc", 0, "S02 private", 1, 0)]);
        Assert.Equal("S01 private", Assert.Single(await store.SearchAsync(new("S01", Vector(1, 0)))).Chunk.Text);
        Assert.Equal("S02 private", Assert.Single(await store.SearchAsync(new("S02", Vector(1, 0)))).Chunk.Text);
        Assert.Empty(await store.SearchAsync(new("unknown", Vector(1, 0))));
    }

    [Fact]
    public async Task Reindexing_removes_obsolete_chunks_and_does_not_duplicate_other_documents()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("docs", "doc", [Entry("doc", 0, "old1", 1, 0), Entry("doc", 1, "old2", 1, 0)]);
        await store.ReplaceDocumentAsync("docs", "other", [Entry("other", 0, "retain", 1, 0)]);
        for (var i = 0; i < 2; i++) await store.ReplaceDocumentAsync("docs", "doc", [Entry("doc", 0, "new", 1, 0)]);
        Assert.Equal(new[] { "new", "retain" }, (await store.SearchAsync(new("docs", Vector(1, 0)))).Select(p => p.Chunk.Text));
    }

    [Fact]
    public async Task Rejects_incompatible_embedding_spaces_without_changing_existing_data()
    {
        var store = new InMemoryVectorStore();
        var original = Entry("doc", 0, "original", 1, 0);
        await store.ReplaceDocumentAsync("docs", "doc", [original]);
        foreach (var vector in new[] { new EmbeddingVector("other-model", [1, 0]), Vector(1, 0, 0) })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReplaceDocumentAsync("docs", "doc", [original with { Embedding = vector }]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SearchAsync(new("docs", vector)));
            Assert.Equal("original", Assert.Single(await store.SearchAsync(new("docs", Vector(1, 0)))).Chunk.Text);
        }
    }

    [Fact]
    public async Task Invalid_replacements_are_atomic()
    {
        var store = new InMemoryVectorStore();
        var original = Entry("doc", 0, "original", 1, 0);
        await store.ReplaceDocumentAsync("docs", "doc", [original]);
        var invalid = original with { Chunk = original.Chunk with { EndOffset = -1 } };
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReplaceDocumentAsync("docs", "doc", [Entry("doc", 0, "new", 1, 0), invalid]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReplaceDocumentAsync("docs", "doc", [original, original]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReplaceDocumentAsync("docs", "doc", [Entry("another", 0, "wrong-document", 1, 0)]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReplaceDocumentAsync("docs", "doc", []));
        Assert.Equal("original", Assert.Single(await store.SearchAsync(new("docs", Vector(1, 0)))).Chunk.Text);
    }

    [Fact]
    public async Task Stores_a_snapshot_and_returns_deterministically_ordered_ties()
    {
        var store = new InMemoryVectorStore();
        var entries = new[] { Entry("doc", 1, "second", 1, 0), Entry("doc", 0, "first", 1, 0) };
        await store.ReplaceDocumentAsync("docs", "doc", entries);
        entries[0] = Entry("doc", 1, "mutated", 1, 0);
        Assert.Equal(new[] { "first", "second" }, (await store.SearchAsync(new("docs", Vector(1, 0)))).Select(p => p.Chunk.Text));
    }

    [Fact]
    public async Task Readers_observe_whole_document_versions_during_concurrent_replacements()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("docs", "doc", Version("A"));
        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++) await store.ReplaceDocumentAsync("docs", "doc", Version(i % 2 == 0 ? "B" : "A"));
        });
        var reader = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                var results = await store.SearchAsync(new("docs", Vector(1, 0)));
                Assert.Equal(2, results.Count);
                Assert.Single(results.Select(p => p.Chunk.Text[0]).Distinct());
            }
        });
        await Task.WhenAll(writer, reader);
        static EmbeddedChunk[] Version(string prefix) => [Entry("doc", 0, prefix + "1", 1, 0), Entry("doc", 1, prefix + "2", 1, 0)];
    }

    [Fact]
    public async Task Cancelled_operations_do_not_modify_or_return_data()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("docs", "doc", [Entry("doc", 0, "original", 1, 0)]);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReplaceDocumentAsync("docs", "doc", [Entry("doc", 0, "new", 1, 0)], source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SearchAsync(new("docs", Vector(1, 0)), source.Token));
        Assert.Equal("original", Assert.Single(await store.SearchAsync(new("docs", Vector(1, 0)))).Chunk.Text);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1001, 0)]
    [InlineData(5, -1.1)]
    [InlineData(5, 1.1)]
    [InlineData(5, double.NaN)]
    public async Task Rejects_invalid_search_limits(int topK, double score) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new InMemoryVectorStore().SearchAsync(new("docs", Vector(1, 0), topK, score)));

    [Fact]
    public void Embeddings_are_immutable_finite_nonzero_and_identify_the_model()
    {
        float[] source = [3, 4];
        var vector = new EmbeddingVector("provider/model:v2", source);
        source[0] = 0;
        Assert.Equal("provider/model:v2", vector.ModelId);
        Assert.Equal(new float[] { 3, 4 }, vector.Values);
        Assert.Equal(2, vector.Dimensions);
        Assert.Equal(5, vector.Magnitude);
        foreach (var values in new float[][] { [], [0, 0], [float.NaN, 1], [float.PositiveInfinity, 1] })
            Assert.ThrowsAny<ArgumentException>(() => new EmbeddingVector("model", values));
        Assert.Throws<ArgumentException>(() => new EmbeddingVector(" ", [1]));
    }
}
