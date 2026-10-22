# SignalR chat

Endpoint: `/hubs/chat`. The authenticated stream method is `StreamMessage` with one
`ChatRequest` argument: `{ "message": "Bonjour" }`. Every stream item is a text delta:
`{ "message": "fragment" }`. Concatenate the messages in the order received.

The dependency chain is `ChatHub → IChatService → ILLMService`. The Hub contains transport
and connection handling only. Validation, generation and stream rules live in the application
service. There are no broadcasts, retained conversations or server-side connection registries.

The API registers the built-in ASP.NET Core SignalR server; no additional SignalR NuGet package
is needed. `AddChatSignalR()` preserves an existing `ILLMService` registration. Register a real
streaming provider adapter before that call. The default `UnconfiguredLLMService` permits API
startup but returns `chat_llm_not_configured` on generation. It is not a fake production model.

## Frontend contract

Illustrative usage with the official `@microsoft/signalr` client (no frontend files are changed):

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5192/hubs/chat", {
    accessTokenFactory: () => token, // accessToken from POST /api/v1/auth/token
    withCredentials: false          // bearer authentication, without cookies
  })
  .withAutomaticReconnect()
  .build();

connection.onreconnecting(error => console.log("Reconnecting", error));
connection.onreconnected(id => console.log("Connected", id));
connection.onclose(error => console.log("Disconnected", error));
await connection.start();

let answer = "";
const subscription = connection.stream("StreamMessage", { message: "Bonjour" })
  .subscribe({
    next: delta => { answer += delta.message; console.log(answer); },
    error: error => console.error("Chat stream failed", error),
    complete: () => console.log("Generation finished")
  });

// Cancel this generation, while keeping the connection:
// subscription.dispose();

// Disconnect; this also cancels any active generation:
// await connection.stop();
```

For a frontend hosted on another origin, configure its exact origin in `Cors:AllowedOrigins`.
The existing CORS policy is retained; `withCredentials: false` matches bearer authentication.
An automatic reconnection establishes a new connection; it does not resume a partially consumed
answer. A new stream must be requested explicitly, with the current token.

## Authentication and errors

`[Authorize]` protects the Hub. Authorization headers are accepted normally; browser WebSocket/SSE
clients may supply `access_token` in the query string only on `/hubs/chat` and its subpaths.
Other API endpoints do not accept query-string bearer tokens. Connections close when the signed
token's authentication ticket expires. Framework request-URL logging is suppressed to avoid
recording query-string tokens; application HTTP logging still records method, path, status and duration.

Client stream cancellation and `Context.ConnectionAborted` are linked per invocation and
propagated through `IChatService` to the upstream provider. Async enumerators and cancellation
sources are disposed. The provider must honour cancellation and release its upstream response.

The stream emits a completion frame when done; errors reach the subscription's `error` callback.
Public error codes carried by `HubException` are:

| Code | Meaning |
|---|---|
| `chat_invalid_request` | Application rejected the request. |
| `chat_llm_not_configured` | A real LLM adapter has not been registered. |
| `chat_generation_failed` | Provider/generation failure; details are logged only on the server. |
| `chat_cancelled` | Generation cancelled. A disposed client subscription may ignore its completion. |

SignalR may prepend its exception type to these codes. Partial fragments already received remain
partial: an error is not a successful full answer. A failed invocation does not terminate the
connection. RAG, MCP and agent execution are not part of this transport.

## Verification

```powershell
dotnet build Logistics.slnx --no-restore -m:1
dotnet build tests/Logistics.Verify/Logistics.Verify.csproj --no-restore -m:1
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --unit-tests-only
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --signalr-tests-only
```

The existing in-process runner is used because Windows prevents VSTest's testhost from monitoring
its parent process in this environment. Tests exercise the canonical xUnit methods, including async
facts/theories. `--signalr-tests-only` runs actual Kestrel/WebSocket tests with a controlled LLM fake:
incremental output, client cancellation, disconnect disposal, safe errors after partial output,
continued connection use, authentication and query-token scope, connection isolation, and unavailable
provider handling. It never runs SQL scenarios or contacts a real LLM.

References: [SignalR streaming](https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming?view=aspnetcore-10.0),
[SignalR authentication](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0).
