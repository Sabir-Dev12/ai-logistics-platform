using System.Text;
using Logistics.Application.AI.RAG;
using Logistics.Infrastructure.AI.RAG;

namespace Logistics.UnitTests;

public sealed class RagDocumentLoaderTests
{
    [Fact]
    public async Task Loads_utf8_preserves_text_and_source_and_leaves_the_stream_open()
    {
        const string text = "  Délais d’expédition 🚚\r\n    Casablanca → Paris.  ";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var document = await new TextDocumentLoader().LoadAsync(new("procedure", stream, "procedure.txt"));
        Assert.Equal(new RagDocument("procedure", text, "procedure.txt"), document);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Removes_a_utf8_bom_from_the_returned_text()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Livraison")).ToArray());
        Assert.Equal("Livraison", (await new TextDocumentLoader().LoadAsync(new("doc", stream))).Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\r\n\t")]
    [InlineData("\uFEFF")]
    public async Task Rejects_documents_without_indexable_text(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await Assert.ThrowsAsync<ArgumentException>(() => new TextDocumentLoader().LoadAsync(new("doc", stream)));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Enforces_character_limits_and_accepts_exactly_the_limit()
    {
        var loader = new TextDocumentLoader(new() { MaximumCharacters = 5 });
        using var exact = new MemoryStream(Encoding.UTF8.GetBytes("ééééé"));
        Assert.Equal("ééééé", (await loader.LoadAsync(new("doc", exact))).Text);
        using var tooLarge = new MemoryStream(Encoding.UTF8.GetBytes("éééééé"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(new("doc", tooLarge)));
        Assert.True(tooLarge.CanRead);
    }

    [Fact]
    public async Task Rejects_invalid_utf8_and_binary_null_bytes()
    {
        using var invalid = new MemoryStream([0xc3, 0x28]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => new TextDocumentLoader().LoadAsync(new("doc", invalid)));
        using var binary = new MemoryStream([0x41, 0, 0x42]);
        await Assert.ThrowsAsync<ArgumentException>(() => new TextDocumentLoader().LoadAsync(new("doc", binary)));
    }

    [Fact]
    public async Task Rejects_invalid_sources_without_reading()
    {
        var loader = new TextDocumentLoader();
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadAsync(new("doc", null!)));
        using var readable = new MemoryStream(Encoding.UTF8.GetBytes("text"));
        await Assert.ThrowsAsync<ArgumentException>(() => loader.LoadAsync(new(" ", readable)));
        Assert.Equal(0, readable.Position);
        var closed = new MemoryStream();
        closed.Dispose();
        await Assert.ThrowsAsync<ArgumentException>(() => loader.LoadAsync(new("doc", closed)));
    }

    [Fact]
    public async Task Precancelled_loading_does_not_read_or_dispose_the_stream()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("text"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TextDocumentLoader().LoadAsync(new("doc", stream), source.Token));
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Cancellation_during_stream_read_is_propagated()
    {
        using var stream = new WaitingStream();
        using var source = new CancellationTokenSource();
        var read = new TextDocumentLoader().LoadAsync(new("doc", stream), source.Token);
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Rejects_invalid_loading_limits(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextDocumentLoader(new() { MaximumCharacters = limit }));

    private sealed class WaitingStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
