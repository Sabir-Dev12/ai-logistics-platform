using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Logistics.Domain.Common;
using ModelContextProtocol.Protocol;

namespace Logistics.Api.Mcp;

/// <summary>Common MCP transport concerns; never receives a repository, DbContext or SQL executor.</summary>
public abstract class ReadOnlyMcpTool<T> : ILogisticsMcpTool where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly JsonElement OutputSchema = CreateOutputSchema();
    private readonly ILogger _logger;

    protected ReadOnlyMcpTool(string name, string description, JsonElement inputSchema, ILogger logger)
    {
        _logger = logger;
        Definition = new Tool
        {
            Name = name, Description = description, InputSchema = inputSchema,
            OutputSchema = OutputSchema,
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false
            }
        };
    }

    public Tool Definition { get; }

    protected abstract Task<T> ReadAsync(McpToolInput input, CancellationToken cancellationToken);

    public async Task<CallToolResult> ExecuteAsync(IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var started = Stopwatch.GetTimestamp();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["McpTool"] = Definition.Name, ["McpCorrelationId"] = correlationId
        });
        _logger.LogInformation("MCP tool {Tool} started; correlation {CorrelationId}", Definition.Name, correlationId);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = await ReadAsync(new McpToolInput(arguments), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(data);
            var result = Result(new McpToolResponse<T>(true, data, null, correlationId));
            _logger.LogInformation("MCP tool {Tool} completed in {ElapsedMs} ms; correlation {CorrelationId}",
                Definition.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, correlationId);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("MCP tool {Tool} cancelled; correlation {CorrelationId}", Definition.Name, correlationId);
            throw;
        }
        catch (Exception exception)
        {
            var error = exception switch
            {
                McpInputException input => new McpToolError("validation_failed", input.Message, input.Field),
                EntityNotFoundException => new McpToolError("not_found", "The requested entity was not found."),
                ForbiddenException => new McpToolError("forbidden", "Access to this entity is forbidden."),
                DomainException domain => new McpToolError(domain.Code, "The business service rejected the request."),
                DbException => new McpToolError("database_unavailable", "The database is temporarily unavailable."),
                _ => new McpToolError("server_error", "Unable to read this entity. Use the correlation id to locate the server log entry.")
            };
            // Do not log input arguments, entity contents or provider exception messages containing SQL/secrets.
            _logger.LogWarning("MCP tool {Tool} failed: {Code}, exception {ExceptionType}; correlation {CorrelationId}",
                Definition.Name, error.Code, exception.GetType().FullName, correlationId);
            return Result(new McpToolResponse<T>(false, null, error, correlationId));
        }
    }

    private static CallToolResult Result(McpToolResponse<T> response)
    {
        var json = JsonSerializer.SerializeToElement(response, JsonOptions);
        return new CallToolResult
        {
            IsError = !response.Success, StructuredContent = json,
            Content = [new TextContentBlock { Text = json.GetRawText() }]
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private static JsonElement CreateOutputSchema()
    {
        var schema = JsonOptions.GetJsonSchemaAsNode(typeof(McpToolResponse<T>));
        // The envelope is always present; only its data/error members may be null.
        // Older MCP clients require an object at the root of outputSchema.
        schema["type"] = "object";
        return JsonSerializer.SerializeToElement(schema);
    }
}
