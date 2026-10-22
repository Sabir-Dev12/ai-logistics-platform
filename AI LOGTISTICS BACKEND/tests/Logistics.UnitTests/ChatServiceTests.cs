using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.Chat;

namespace Logistics.UnitTests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task Returns_the_provider_response_and_preserves_message_formatting()
    {
        const string prompt = "  Bonjour !\n    Où est mon colis ?  ";
        const string reply = "  Réponse du modèle.\n";
        var provider = new FakeLlmService((_, _) => Task.FromResult(reply));
        IChatService service = new ChatService(provider);
        var request = new ChatRequest(prompt);

        var response = await service.SendMessageAsync(request);

        Assert.Equal(reply, response.Message);
        Assert.Equal(prompt, Assert.Single(provider.Calls).Prompt);
        Assert.Equal(prompt, request.Message);
    }

    [Fact]
    public async Task Rejects_a_missing_request_without_calling_the_provider()
    {
        var provider = new FakeLlmService();
        var service = new ChatService(provider);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SendMessageAsync(null!));

        Assert.Empty(provider.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task Rejects_blank_messages_without_calling_the_provider(string? message)
    {
        var provider = new FakeLlmService();
        var service = new ChatService(provider);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendMessageAsync(new ChatRequest(message!)));

        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task Passes_the_callers_cancellation_token_to_the_provider()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new FakeLlmService();
        var service = new ChatService(provider);

        await service.SendMessageAsync(new ChatRequest("Bonjour"), cancellation.Token);

        Assert.Equal(cancellation.Token, Assert.Single(provider.Calls).CancellationToken);
    }

    [Fact]
    public async Task Already_cancelled_calls_do_not_invoke_the_provider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var provider = new FakeLlmService();
        var service = new ChatService(provider);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SendMessageAsync(new ChatRequest("Bonjour"), cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task Cancellation_during_generation_propagates_to_the_caller()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new FakeLlmService(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return "Never reached";
        });
        var service = new ChatService(provider);

        var pending = service.SendMessageAsync(new ChatRequest("Bonjour"), cancellation.Token);
        cancellation.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task Does_not_deliver_a_response_when_the_provider_ignores_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeLlmService((_, _) => completion.Task);
        var service = new ChatService(provider);

        var pending = service.SendMessageAsync(new ChatRequest("Bonjour"), cancellation.Token);
        cancellation.Cancel();
        completion.SetResult("Late reply");

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task Rejects_empty_provider_responses(string? reply)
    {
        var provider = new FakeLlmService((_, _) => Task.FromResult(reply!));
        var service = new ChatService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendMessageAsync(new ChatRequest("Bonjour")));

        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task Propagates_provider_failures_without_returning_a_fabricated_response()
    {
        var failure = new HttpRequestException("The provider is unavailable.");
        var provider = new FakeLlmService((_, _) => Task.FromException<string>(failure));
        var service = new ChatService(provider);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendMessageAsync(new ChatRequest("Bonjour")));

        Assert.Same(failure, error);
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task Concurrent_calls_keep_their_own_responses_and_tokens()
    {
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var firstCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeLlmService((prompt, _) =>
            prompt == "first" ? firstCompletion.Task : secondCompletion.Task);
        var service = new ChatService(provider);

        var first = service.SendMessageAsync(new ChatRequest("first"), firstCancellation.Token);
        var second = service.SendMessageAsync(new ChatRequest("second"), secondCancellation.Token);
        secondCompletion.SetResult("second reply");

        Assert.Equal("second reply", (await second).Message);
        Assert.False(first.IsCompleted);
        firstCompletion.SetResult("first reply");
        Assert.Equal("first reply", (await first).Message);
        Assert.Collection(provider.Calls,
            call =>
            {
                Assert.Equal("first", call.Prompt);
                Assert.Equal(firstCancellation.Token, call.CancellationToken);
            },
            call =>
            {
                Assert.Equal("second", call.Prompt);
                Assert.Equal(secondCancellation.Token, call.CancellationToken);
            });
    }

    [Fact]
    public void Rejects_a_missing_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new ChatService(null!));
    }

    private sealed class FakeLlmService(
        Func<string, CancellationToken, Task<string>>? generate = null) : IAgentService
    {
        public List<(string Prompt, CancellationToken CancellationToken)> Calls { get; } = [];

        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Calls.Add((prompt, cancellationToken));
            return generate?.Invoke(prompt, cancellationToken) ?? Task.FromResult("Reply");
        }

        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This fake tests non-streaming calls only.");
    }
}
