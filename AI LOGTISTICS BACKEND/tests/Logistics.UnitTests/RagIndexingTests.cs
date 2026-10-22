using System.Text;
using Logistics.Application.AI.RAG;
using Logistics.Infrastructure.AI.RAG;
using static Logistics.UnitTests.RagTestDoubles;

namespace Logistics.UnitTests;

public sealed class RagIndexingTests
{
    [Fact]
    public async Task Loads_chunks_and_embeds_before_a_single_document_replacement()
    {
        var provider = new Embeddings();
        var store = new Store();
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(new() { ChunkSize = 4, Overlap = 0 }), provider, store);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("AAAABBBBCCCC"));
        using var source = new CancellationTokenSource();
        var result = await indexer.IndexAsync("S01", new("doc", stream, "guide.txt"), source.Token);
        Assert.Equal(new DocumentIndexingResult("doc", 3, "fixture-model:v1", 2), result);
        Assert.Equal(new[] { "AAAA", "BBBB", "CCCC" }, provider.Calls.Select(call => call.Text));
        Assert.All(provider.Calls, call => Assert.Equal(source.Token, call.Token));
        var write = Assert.Single(store.Replacements);
        Assert.Equal("S01", write.Collection);
        Assert.Equal("doc", write.Document);
        Assert.Equal(source.Token, write.Token);
        Assert.Equal(3, write.Chunks.Count);
        Assert.All(write.Chunks, chunk => Assert.Equal("guide.txt", chunk.Chunk.SourceName));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task A_provider_failure_keeps_the_previous_index_intact()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("S01", "doc", [Entry("doc", 0, "original", 1, 0)]);
        var failure = new IOException("Embedding failed");
        var count = 0;
        var provider = new Embeddings((_, _) => ++count == 2 ? Task.FromException<EmbeddingVector>(failure) : Task.FromResult(Vector(1, 0)));
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(new() { ChunkSize = 4, Overlap = 0 }), provider, store);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("AAAABBBBCCCC"));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => indexer.IndexAsync("S01", new("doc", stream))));
        Assert.Equal("original", Assert.Single(await store.SearchAsync(new("S01", Vector(1, 0)))).Chunk.Text);
        Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public async Task Model_changes_within_an_indexing_batch_do_not_replace_existing_passages()
    {
        var store = new InMemoryVectorStore();
        await store.ReplaceDocumentAsync("S01", "doc", [Entry("doc", 0, "original", 1, 0)]);
        var count = 0;
        var provider = new Embeddings((_, _) => Task.FromResult(++count == 2 ? new EmbeddingVector("other-model", [1, 0]) : Vector(1, 0)));
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(new() { ChunkSize = 4, Overlap = 0 }), provider, store);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("AAAABBBB"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => indexer.IndexAsync("S01", new("doc", stream)));
        Assert.Equal("original", Assert.Single(await store.SearchAsync(new("S01", Vector(1, 0)))).Chunk.Text);
    }

    [Fact]
    public async Task Loader_or_chunking_failures_do_not_start_embedding_or_write_a_partial_document()
    {
        var provider = new Embeddings();
        var store = new Store();
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(new() { ChunkSize = 3, Overlap = 0, MaximumChunks = 1 }), provider, store);
        using var blank = new MemoryStream(Encoding.UTF8.GetBytes(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => indexer.IndexAsync("S01", new("doc", blank)));
        using var tooManyChunks = new MemoryStream(Encoding.UTF8.GetBytes("ABCDEFG"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => indexer.IndexAsync("S01", new("doc", tooManyChunks)));
        Assert.Empty(provider.Calls);
        Assert.Empty(store.Replacements);
    }

    [Fact]
    public async Task A_null_embedding_is_not_indexed()
    {
        var store = new Store();
        var provider = new Embeddings((_, _) => Task.FromResult<EmbeddingVector>(null!));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content"));
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(), provider, store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => indexer.IndexAsync("S01", new("doc", stream)));
        Assert.Empty(store.Replacements);
    }

    [Fact]
    public async Task Cancellation_during_embedding_never_commits_even_if_the_provider_returns_late()
    {
        using var source = new CancellationTokenSource();
        var provider = new Embeddings((_, _) =>
        {
            source.Cancel();
            return Task.FromResult(Vector(1, 0));
        });
        var store = new Store();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content"));
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(), provider, store);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => indexer.IndexAsync("S01", new("doc", stream), source.Token));
        Assert.Empty(store.Replacements);
    }

    [Fact]
    public async Task Precancelled_or_invalid_indexing_does_not_read_the_document()
    {
        var provider = new Embeddings();
        var store = new Store();
        var indexer = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(), provider, store);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content"));
        await Assert.ThrowsAsync<ArgumentException>(() => indexer.IndexAsync(" ", new("doc", stream)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => indexer.IndexAsync("S01", null!));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => indexer.IndexAsync("S01", new("doc", stream), source.Token));
        Assert.Equal(0, stream.Position);
        Assert.Empty(provider.Calls);
        Assert.Empty(store.Replacements);
    }
}
