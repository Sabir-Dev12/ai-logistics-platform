using System.Text.Json;
using System.Text.Json.Nodes;

namespace Logistics.Api.Mcp;

/// <summary>Validates the tool contract only; business validation remains in application services.</summary>
public sealed class McpToolInput(IReadOnlyDictionary<string, JsonElement>? arguments)
{
    // Transport bound, deliberately independent of legacy database column lengths.
    public const int MaximumKeyLength = 256;

    public void AllowOnly(params string[] names)
    {
        if (arguments is not null && arguments.Keys.Any(key => !names.Contains(key, StringComparer.Ordinal)))
            throw new McpInputException(null, "Unexpected argument. Use only the fields advertised by inputSchema.");
    }

    public string Key(string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new McpInputException(name, "A string identifier is required.");
        var key = value.GetString()!;
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaximumKeyLength || key.Any(char.IsControl))
            throw new McpInputException(name, $"Use a nonblank identifier of at most {MaximumKeyLength} characters without control characters.");
        return key;
    }

    public T EnumName<T>(string name) where T : struct, Enum
    {
        var value = Key(name);
        if (!Enum.GetNames<T>().Contains(value, StringComparer.Ordinal))
            throw new McpInputException(name, "Use one of the entity names advertised by inputSchema.");
        return Enum.Parse<T>(value);
    }

    public int Take()
    {
        if (arguments is null || !arguments.TryGetValue("take", out var value)) return 100;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var take) || take is < 1 or > 500)
            throw new McpInputException("take", "Use an integer between 1 and 500.");
        return take;
    }

    public bool Boolean(string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value)) return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new McpInputException(name, "Use a boolean value.");
        return value.GetBoolean();
    }

    public static JsonObject KeyProperty(string description) => new()
    {
        ["type"] = "string", ["description"] = description,
        ["minLength"] = 1, ["maxLength"] = MaximumKeyLength,
        ["pattern"] = "^(?=.*\\S)[^\\u0000-\\u001F\\u007F-\\u009F]+$"
    };

    public static JsonElement Schema(JsonObject properties, params string[] required) =>
        JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object", ["properties"] = properties,
            ["required"] = new JsonArray(required.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray()),
            ["additionalProperties"] = false
        });
}

internal sealed class McpInputException(string? field, string message) : Exception(message)
{
    public string? Field { get; } = field;
}
