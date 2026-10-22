# Chat application service

`IChatService.SendMessageAsync(ChatRequest, CancellationToken)` validates a non-empty user message,
calls `IAgentService.GenerateResponseAsync`, and returns the agent's verified results as a `ChatResponse`.
Input and output formatting are preserved. A missing request/message is rejected before any provider
call. Provider failures and cancellation propagate to the caller; an empty provider response is rejected.

`IChatService.StreamMessageAsync` delegates to `IAgentService.StreamResponseAsync` and yields one
`ChatResponse` per text fragment. The caller concatenates their `Message` values. Empty fragments are
ignored; whitespace in other fragments is preserved. Null fragments and a stream with no meaningful
text are rejected. The complete answer is never assembled or split into simulated streaming chunks.
The cancellation token also reaches the provider's async enumerator, which is disposed when the
caller stops consuming, the provider fails, or generation is cancelled.

The service is stateless: it does not persist or reconstruct a conversation, consult SQL Server,
retrieve documents, invoke tools, or depend on ASP.NET Core or SignalR.

`ILLMService` lives at `AI/ILLMService.cs`. A concrete provider adapter can be implemented in
Infrastructure later. No SDK, provider, credential or network endpoint is selected by this foundation.
The API registers `ChatHub → IChatService → IAgentService → LLM / RAG / MCP` through `AddChatSignalR()`.
The agent streams verified source results and uses the model for a strict JSON source plan.
See [the agent contract](../Agent/README.md) for the plan, context, audit, errors and configuration.
Until a real adapter is supplied, `UnconfiguredLLMService` reports `LLMNotConfiguredException`
instead of generating invented text or preventing API startup. The Hub maps it to
`chat_llm_not_configured`. The adapter must implement both buffered and incremental generation.
Register your adapter before `AddChatSignalR()` to replace the default, for example:

```csharp
// Illustrative: YourLLMProvider is a future implementation, not part of this change.
builder.Services.AddScoped<ILLMService, YourLLMProvider>();
builder.Services.AddChatSignalR();
```

`ChatServiceTests` uses an injected fake agent and runs without a database or external service.
From the solution root:

```powershell
dotnet build Logistics.slnx -m:1
dotnet test tests/Logistics.UnitTests/Logistics.UnitTests.csproj --no-build
```

If Windows denies VSTest's testhost access to its parent process, the existing verification
runner can execute the same xUnit unit-test methods in-process (including async tests),
without running its database scenarios:

```powershell
dotnet build tests/Logistics.Verify/Logistics.Verify.csproj -m:1
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --unit-tests-only
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --signalr-tests-only
```

The SignalR test mode starts actual Kestrel servers on ephemeral loopback ports and speaks the
SignalR JSON protocol over real WebSockets. Only the upstream LLM is a controlled test fake;
no database or external provider is contacted. See the Hub README for the frontend contract.
