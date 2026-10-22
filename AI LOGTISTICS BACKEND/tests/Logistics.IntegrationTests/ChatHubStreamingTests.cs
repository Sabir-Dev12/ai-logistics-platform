using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Logistics.Api.Authentication;
using Logistics.Api.Extensions;
using Logistics.Api.Hubs;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.Features.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.IntegrationTests;

/// <summary>
/// Real Kestrel/WebSocket/SignalR protocol tests. Only the upstream LLM is a controlled fake.
/// No database, external LLM, SignalR client SDK or browser is required.
/// </summary>
public sealed class ChatHubStreamingTests
{
    [Fact]
    public async Task Sends_the_first_fragment_before_the_provider_has_finished()
    {
        var llm = new ControlledLlm();
        await using var server = await ChatServer.Start(llm);
        using var client = await server.Connect();
        await client.Stream("gradual", "gradual");

        var first = await client.InvocationFrame("gradual");
        Assert.Equal(2, first.GetProperty("type").GetInt32());
        Assert.Equal("first:gradual", first.GetProperty("item").GetProperty("message").GetString());
        Assert.False(llm.Release.Task.IsCompleted);
        llm.Release.SetResult();
        Assert.Equal("second:gradual", (await client.InvocationFrame("gradual")).GetProperty("item").GetProperty("message").GetString());
        var completed = await client.InvocationFrame("gradual");
        Assert.Equal(3, completed.GetProperty("type").GetInt32());
        Assert.False(completed.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Client_cancel_invocation_cancels_the_upstream_and_keeps_the_connection_usable()
    {
        var llm = new ControlledLlm();
        await using var server = await ChatServer.Start(llm);
        using var client = await server.Connect();
        await client.Stream("cancel", "wait");
        Assert.Equal(2, (await client.InvocationFrame("cancel")).GetProperty("type").GetInt32());
        await client.Send(new { type = 5, invocationId = "cancel" });
        Assert.True(await llm.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        await client.Stream("after-cancel", "normal");
        Assert.Equal(2, (await client.InvocationFrame("after-cancel")).GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task Disconnect_cancels_and_disposes_the_upstream_stream()
    {
        var llm = new ControlledLlm();
        await using var server = await ChatServer.Start(llm);
        using var client = await server.Connect();
        await client.Stream("disconnect", "wait");
        Assert.Equal(2, (await client.InvocationFrame("disconnect")).GetProperty("type").GetInt32());
        client.Abort();
        Assert.True(await llm.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Error_after_partial_output_is_safe_and_does_not_close_the_connection()
    {
        var llm = new ControlledLlm();
        await using var server = await ChatServer.Start(llm);
        using var client = await server.Connect(useQueryToken: false);
        await client.Stream("error", "fail");
        Assert.Equal(2, (await client.InvocationFrame("error")).GetProperty("type").GetInt32());
        var completion = await client.InvocationFrame("error");
        Assert.Equal(3, completion.GetProperty("type").GetInt32());
        var error = completion.GetProperty("error").GetString()!;
        Assert.Contains("chat_generation_failed", error);
        Assert.DoesNotContain("private-provider-token", error);
        await client.Stream("recovery", "normal");
        Assert.Equal(2, (await client.InvocationFrame("recovery")).GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task Blank_input_is_rejected_without_invoking_the_provider()
    {
        var llm = new ControlledLlm();
        await using var server = await ChatServer.Start(llm);
        using var client = await server.Connect();
        await client.Stream("validation", " ");
        var frame = await client.InvocationFrame("validation");
        Assert.Equal(3, frame.GetProperty("type").GetInt32());
        Assert.Contains("chat_invalid_request", frame.GetProperty("error").GetString()!);
        Assert.Equal(0, llm.Calls);
    }

    [Fact]
    public async Task Hub_requires_authentication_and_query_tokens_are_not_accepted_by_other_endpoints()
    {
        await using var server = await ChatServer.Start(new ControlledLlm());
        using var http = new HttpClient();
        using var anonymous = await http.PostAsync(server.Url + ChatHub.Endpoint + "/negotiate?negotiateVersion=1", new StringContent(""));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var outsideHub = await http.GetAsync(server.Url + "/protected?access_token=" + Uri.EscapeDataString(server.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, outsideHub.StatusCode);
        using var authorized = await server.Connect();
        await authorized.Stream("authenticated", "normal");
        Assert.Equal(2, (await authorized.InvocationFrame("authenticated")).GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task Two_connections_receive_only_their_own_fragments()
    {
        await using var server = await ChatServer.Start(new ControlledLlm());
        using var first = await server.Connect();
        using var second = await server.Connect();
        await first.Stream("same-id", "alpha");
        await second.Stream("same-id", "beta");
        Assert.Equal("first:alpha", (await first.InvocationFrame("same-id")).GetProperty("item").GetProperty("message").GetString());
        Assert.Equal("first:beta", (await second.InvocationFrame("same-id")).GetProperty("item").GetProperty("message").GetString());
        Assert.Equal("second:alpha", (await first.InvocationFrame("same-id")).GetProperty("item").GetProperty("message").GetString());
        Assert.Equal("second:beta", (await second.InvocationFrame("same-id")).GetProperty("item").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Unconfigured_provider_allows_startup_but_reports_an_explicit_stream_error()
    {
        await using var server = await ChatServer.Start(null);
        using var client = await server.Connect();
        await client.Stream("unconfigured", "Question");
        var completion = await client.InvocationFrame("unconfigured");
        Assert.Equal(3, completion.GetProperty("type").GetInt32());
        Assert.Contains("chat_llm_not_configured", completion.GetProperty("error").GetString()!);
    }

    private sealed class ControlledLlm : ILLMService
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The Hub must stream, not buffer the answer.");

        public async IAsyncEnumerable<string> StreamResponseAsync(
            string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            try
            {
                yield return "first:" + prompt;
                if (prompt == "wait") await Task.Delay(Timeout.Infinite, cancellationToken);
                if (prompt == "gradual") await Release.Task.WaitAsync(cancellationToken);
                if (prompt == "fail") throw new InvalidOperationException("private-provider-token");
                yield return "second:" + prompt;
            }
            finally { if (prompt == "wait") Stopped.TrySetResult(cancellationToken.IsCancellationRequested); }
        }
    }

    private sealed class TransportTestAgent(ILLMService llm) : IAgentService
    {
        public Task<string> GenerateResponseAsync(string question, CancellationToken cancellationToken = default) => llm.GenerateResponseAsync(question, cancellationToken);
        public IAsyncEnumerable<string> StreamResponseAsync(string question, CancellationToken cancellationToken = default) => llm.StreamResponseAsync(question, cancellationToken);
    }

    private sealed class ChatServer(WebApplication application, string url, string token) : IAsyncDisposable
    {
        public string Url { get; } = url;
        public string Token { get; } = token;

        public static async Task<ChatServer> Start(ILLMService? llm)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ["Authentication:AllowHeaderFallback"] = "false",
                ["Authentication:TokenLifetimeMinutes"] = "5"
            });
            builder.Services.AddApiAuthentication(builder.Configuration, estDeveloppement: false);
            if (llm is not null) builder.Services.AddSingleton(llm);
            builder.Services.AddChatSignalR();
            // Isolates the existing transport tests; agent orchestration has its own suite.
            builder.Services.RemoveAll<IAgentService>();
            builder.Services.RemoveAll<IAgentMcpTools>();
            builder.Services.RemoveAll<IAgentContext>();
            builder.Services.AddScoped<IAgentService>(_ => new TransportTestAgent(llm ?? new Logistics.Infrastructure.AI.UnconfiguredLLMService()));
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapHub<ChatHub>(ChatHub.Endpoint, options => options.CloseOnAuthenticationExpiration = true);
            app.MapGet("/protected", () => Results.Ok()).RequireAuthorization();
            try
            {
                await app.StartAsync();
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                var token = app.Services.GetRequiredService<ILogisticsTokenService>().Issue(
                    new CallerIdentity("S01", "AG01", "P001", "TEST", null, DateTime.UtcNow.AddMinutes(5), true));
                return new ChatServer(app, address, token);
            }
            catch { await app.DisposeAsync(); throw; }
        }

        public async Task<WireClient> Connect(bool useQueryToken = true)
        {
            var socket = new ClientWebSocket();
            if (!useQueryToken) socket.Options.SetRequestHeader("Authorization", "Bearer " + Token);
            var uri = Url.Replace("http://", "ws://", StringComparison.Ordinal) + ChatHub.Endpoint
                + (useQueryToken ? "?access_token=" + Uri.EscapeDataString(Token) : "");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await socket.ConnectAsync(new Uri(uri), timeout.Token);
                var client = new WireClient(socket);
                await client.Send(new { protocol = "json", version = 1 });
                var handshake = await client.Frame();
                Assert.False(handshake.TryGetProperty("error", out _));
                return client;
            }
            catch { socket.Dispose(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await application.StopAsync(timeout.Token);
            await application.DisposeAsync();
        }
    }

    private sealed class WireClient(ClientWebSocket socket) : IDisposable
    {
        private readonly Queue<JsonElement> _frames = new();
        private string _pending = "";

        public Task Stream(string id, string message) => Send(new
        {
            type = 4, invocationId = id, target = "StreamMessage", arguments = new[] { new { message } }
        });

        public async Task Send(object value)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + '\u001e');
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        }

        public async Task<JsonElement> InvocationFrame(string id)
        {
            while (true)
            {
                var frame = await Frame();
                if (frame.TryGetProperty("invocationId", out var invocation) && invocation.GetString() == id)
                    return frame;
            }
        }

        public async Task<JsonElement> Frame()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var buffer = new byte[4096];
            while (_frames.Count == 0)
            {
                using var message = new MemoryStream();
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        throw new IOException("SignalR connection closed before the expected frame.");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                _pending += Encoding.UTF8.GetString(message.ToArray());
                int separator;
                while ((separator = _pending.IndexOf('\u001e')) >= 0)
                {
                    var json = _pending[..separator];
                    _pending = _pending[(separator + 1)..];
                    if (json.Length == 0) continue;
                    using var document = JsonDocument.Parse(json);
                    _frames.Enqueue(document.RootElement.Clone());
                }
            }
            return _frames.Dequeue();
        }

        public void Abort() => socket.Abort();
        public void Dispose() => socket.Dispose();
    }
}
