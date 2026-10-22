using System.Runtime.CompilerServices;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.Chat;

namespace Logistics.UnitTests;

public sealed class ChatStreamingTests
{
    [Fact]
    public async Task Streams_fragments_in_order_without_losing_whitespace()
    {
        var llm = new StreamingLlm((_, _) => Sequence("", "Bon", " ", "jour", "!"));
        var responses = await Read(new ChatService(llm).StreamMessageAsync(new ChatRequest("  Question\n")));

        Assert.Equal(new[] { "Bon", " ", "jour", "!" }, responses.Select(item => item.Message));
        Assert.Equal("  Question\n", llm.Prompt);
    }

    [Fact]
    public async Task First_fragment_is_available_before_generation_finishes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var llm = new StreamingLlm((_, _) => Incremental(release.Task));
        await using var stream = new ChatService(llm).StreamMessageAsync(new ChatRequest("Question")).GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("first", stream.Current.Message);
        var pending = stream.MoveNextAsync().AsTask();
        Assert.False(pending.IsCompleted);
        release.SetResult();
        Assert.True(await pending);
        Assert.Equal("second", stream.Current.Message);
        Assert.False(await stream.MoveNextAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Invalid_messages_are_rejected_before_the_provider_is_called(string? message)
    {
        var llm = new StreamingLlm((_, _) => Sequence("response"));
        await Assert.ThrowsAsync<ArgumentException>(() => Read(
            new ChatService(llm).StreamMessageAsync(new ChatRequest(message!))));
        Assert.Null(llm.Prompt);
    }

    [Fact]
    public async Task Null_requests_are_rejected_before_generation()
    {
        var llm = new StreamingLlm((_, _) => Sequence("response"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Read(new ChatService(llm).StreamMessageAsync(null!)));
        Assert.Null(llm.Prompt);
    }

    [Fact]
    public async Task Already_cancelled_streams_do_not_call_the_provider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var llm = new StreamingLlm((_, _) => Sequence("response"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Read(
            new ChatService(llm).StreamMessageAsync(new ChatRequest("Question"), cancellation.Token)));
        Assert.Null(llm.Prompt);
    }

    [Fact]
    public async Task Cancellation_reaches_the_provider_and_disposes_its_stream()
    {
        using var cancellation = new CancellationTokenSource();
        var disposed = false;
        var llm = new StreamingLlm((_, token) => WaitForCancellation(token, () => disposed = true));
        await using var stream = new ChatService(llm).StreamMessageAsync(new ChatRequest("Question"), cancellation.Token)
            .GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(cancellation.Token, llm.Token);
        var pending = stream.MoveNextAsync().AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(disposed);
    }

    [Fact]
    public async Task Does_not_forward_late_fragments_when_a_provider_ignores_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var llm = new StreamingLlm((_, _) => Incremental(release.Task));
        await using var stream = new ChatService(llm).StreamMessageAsync(new ChatRequest("Question"), cancellation.Token)
            .GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var pending = stream.MoveNextAsync().AsTask();
        cancellation.Cancel();
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Disposal_by_the_consumer_disposes_the_provider_stream()
    {
        var disposed = false;
        var llm = new StreamingLlm((_, token) => WaitForCancellation(token, () => disposed = true));
        var stream = new ChatService(llm).StreamMessageAsync(new ChatRequest("Question")).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        await stream.DisposeAsync();
        Assert.True(disposed);
    }

    [Fact]
    public async Task Empty_provider_streams_are_rejected()
    {
        var llm = new StreamingLlm((_, _) => Sequence());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(
            new ChatService(llm).StreamMessageAsync(new ChatRequest("Question"))));
    }

    [Fact]
    public async Task Whitespace_only_provider_streams_are_rejected()
    {
        var llm = new StreamingLlm((_, _) => Sequence(" ", "\n"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(
            new ChatService(llm).StreamMessageAsync(new ChatRequest("Question"))));
    }

    [Fact]
    public async Task Null_provider_fragments_are_rejected()
    {
        var llm = new StreamingLlm((_, _) => Sequence([null!]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(
            new ChatService(llm).StreamMessageAsync(new ChatRequest("Question"))));
    }

    [Fact]
    public async Task Provider_failure_after_a_fragment_propagates_to_the_caller()
    {
        var failure = new IOException("provider failure");
        var llm = new StreamingLlm((_, _) => FailAfterFragment(failure));
        await using var stream = new ChatService(llm).StreamMessageAsync(new ChatRequest("Question")).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("partial", stream.Current.Message);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => stream.MoveNextAsync().AsTask()));
    }

    private static async Task<List<ChatResponse>> Read(IAsyncEnumerable<ChatResponse> stream)
    {
        var responses = new List<ChatResponse>();
        await foreach (var response in stream) responses.Add(response);
        return responses;
    }

    private static async IAsyncEnumerable<string> Sequence(params string[] values)
    {
        await Task.CompletedTask;
        foreach (var value in values) yield return value;
    }

    private static async IAsyncEnumerable<string> Incremental(Task release)
    {
        yield return "first";
        await release;
        yield return "second";
    }

    private static async IAsyncEnumerable<string> WaitForCancellation(
        [EnumeratorCancellation] CancellationToken token, Action disposed)
    {
        try
        {
            yield return "first";
            await Task.Delay(Timeout.Infinite, token);
        }
        finally { disposed(); }
    }

    private static async IAsyncEnumerable<string> FailAfterFragment(Exception exception)
    {
        yield return "partial";
        await Task.CompletedTask;
        throw exception;
    }

    private sealed class StreamingLlm(Func<string, CancellationToken, IAsyncEnumerable<string>> generate) : IAgentService
    {
        public string? Prompt { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Streaming must not call the buffered generation method.");

        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            Token = cancellationToken;
            return generate(prompt, cancellationToken);
        }
    }
}
