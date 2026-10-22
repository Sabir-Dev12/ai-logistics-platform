using System.Text;
using Logistics.Application.AI.RAG;

namespace Logistics.UnitTests;

public sealed class RagChunkingTests
{
    [Fact]
    public void Produces_overlapping_passages_with_correct_source_offsets_and_stable_ids()
    {
        var service = new CharacterChunkingService(new() { ChunkSize = 6, Overlap = 2 });
        var document = new RagDocument("procedure", "ABCDEFGHIJKLMNO", "procedure.txt");
        var chunks = service.Chunk(document);
        Assert.Equal(new[] { "ABCDEF", "EFGHIJ", "IJKLMN", "MNO" }, chunks.Select(chunk => chunk.Text));
        Assert.Equal(new[] { 0, 4, 8, 12 }, chunks.Select(chunk => chunk.StartOffset));
        Assert.Equal(new[] { 6, 10, 14, 15 }, chunks.Select(chunk => chunk.EndOffset));
        Assert.All(chunks, chunk =>
        {
            Assert.Equal(document.Text[chunk.StartOffset..chunk.EndOffset], chunk.Text);
            Assert.Equal("procedure", chunk.DocumentId);
            Assert.Equal("procedure.txt", chunk.SourceName);
        });
        Assert.Equal(new[] { 0, 1, 2, 3 }, chunks.Select(chunk => chunk.Index));
        Assert.Equal(chunks, service.Chunk(document));
    }

    [Fact]
    public void Short_documents_preserve_whitespace_and_are_not_padded()
    {
        var text = "  Livraison\n demain.  ";
        var chunk = Assert.Single(new CharacterChunkingService().Chunk(new("doc", text)));
        Assert.Equal(text, chunk.Text);
        Assert.Equal(0, chunk.StartOffset);
        Assert.Equal(text.Length, chunk.EndOffset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Unicode_passages_do_not_split_surrogate_pairs(int overlap)
    {
        var text = "A🚚B📦C🚢D";
        var chunks = new CharacterChunkingService(new() { ChunkSize = 3, Overlap = overlap }).Chunk(new("doc", text));
        var strictUtf8 = new UTF8Encoding(false, true);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal(text[chunk.StartOffset..chunk.EndOffset], chunk.Text);
            Assert.InRange(chunk.Text.Length, 1, 3);
            Assert.False(char.IsLowSurrogate(chunk.Text[0]));
            Assert.False(char.IsHighSurrogate(chunk.Text[^1]));
            Assert.NotEmpty(strictUtf8.GetBytes(chunk.Text));
        });
        for (var i = 0; i < text.Length; i++)
            Assert.Contains(chunks, chunk => i >= chunk.StartOffset && i < chunk.EndOffset);
    }

    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(1, 0, 10)]
    [InlineData(5, -1, 10)]
    [InlineData(5, 5, 10)]
    [InlineData(5, 6, 10)]
    [InlineData(5, 0, 0)]
    public void Rejects_invalid_chunking_options(int size, int overlap, int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterChunkingService(new()
        {
            ChunkSize = size, Overlap = overlap, MaximumChunks = limit
        }));

    [Fact]
    public void Enforces_the_chunk_limit_before_returning_a_partial_document()
    {
        var service = new CharacterChunkingService(new() { ChunkSize = 3, Overlap = 0, MaximumChunks = 2 });
        Assert.Throws<InvalidOperationException>(() => service.Chunk(new("doc", "ABCDEFG")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_documents(string? text) =>
        Assert.ThrowsAny<ArgumentException>(() => new CharacterChunkingService().Chunk(new("doc", text!)));

    [Fact]
    public void Cancelled_chunking_returns_no_passages()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new CharacterChunkingService().Chunk(new("doc", "Text"), source.Token));
    }
}
