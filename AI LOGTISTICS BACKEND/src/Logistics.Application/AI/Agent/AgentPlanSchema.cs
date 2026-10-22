using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Logistics.Application.AI.Agent;

/// <summary>Uses the advertised MCP contracts to constrain planning, without executing tools.</summary>
internal static class AgentPlanSchema
{
    public static string[] ClientReferences(string question) => Regex.Matches(question,
        @"\b(?:r[eé]f(?:[eé]rence)?\s+(?:(?:du|de\s+ce)\s+)?client|client\s+(?:de\s+)?r[eé]f(?:[eé]rence)?)\s*[:=#]?\s*(?<reference>[\p{L}\p{N}_][\p{L}\p{N}_.-]{0,255})(?![\p{L}\p{N}_-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
        .Select(match => match.Groups["reference"].Value.TrimEnd('.'))
        // Only unambiguous reference-code forms get decoder hints. Other forms still
        // use normal planning and the explicit-identifier guard; no key is derived.
        .Where(value => value.Any(char.IsDigit)).Distinct(StringComparer.Ordinal).Take(4).ToArray();

    public static JsonElement Create(IReadOnlyList<AgentToolDefinition> tools, string question)
    {
        var references = ClientReferences(question);
        var choices = new JsonArray();
        foreach (var tool in tools)
        {
            var input = JsonNode.Parse(tool.InputSchema.GetRawText())!.AsObject();
            if (input["properties"] is JsonObject fields)
                foreach (var property in fields)
                    if (property.Value is JsonObject field)
                    {
                        // MCP validates its full schema. The decoder only needs the field
                        // names/types; transport regex lookaheads are not supported by every model.
                        field.Remove("pattern");
                    }
            if (tool.Name == "GetClient" && references.Length > 0 && input["properties"] is JsonObject clientFields
                && clientFields["keyClient"] is JsonObject clientKey && clientFields.ContainsKey("byReference"))
            {
                clientKey["enum"] = new JsonArray(references.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
                clientFields["byReference"] = new JsonObject { ["const"] = true };
                input["required"] = new JsonArray("keyClient", "byReference");
            }
            choices.Add(new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["name"] = new JsonObject { ["const"] = tool.Name },
                    ["arguments"] = input
                },
                ["required"] = new JsonArray("name", "arguments")
            });
        }
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["requiresBusinessData"] = references.Length > 0 ? new JsonObject { ["const"] = true } : new JsonObject { ["type"] = "boolean" },
                ["useRag"] = new JsonObject { ["type"] = "boolean" },
                ["toolCalls"] = new JsonObject
                {
                    ["type"] = "array", ["maxItems"] = tools.Count == 0 ? 0 : 4,
                    ["minItems"] = references.Length > 0 && tools.Any(t => t.Name == "GetClient") ? 1 : 0,
                    ["items"] = tools.Count == 0 ? new JsonObject { ["type"] = "object" } : new JsonObject { ["anyOf"] = choices }
                }
            },
            ["required"] = new JsonArray("requiresBusinessData", "useRag", "toolCalls")
        });
    }
}
