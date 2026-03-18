using System.Text.Json;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

internal sealed class OpenAiResponsesResponseMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal AgentTurnResponse<TContext> Map<TContext>(OpenAiResponsesResponse response, OpenAiResponsesTurnPlan<TContext> plan)
    {
        JsonArray output = response.Raw["output"] as JsonArray ?? [];
        var toolCalls = new List<AgentToolCall<TContext>>();
        var handoffs = new List<AgentHandoffRequest<TContext>>();
        var items = new List<AgentRunItem>();
        string? finalText = null;
        JsonNode? structured = null;

        // Convert provider output items into the normalized internal run-item model.
        foreach (JsonObject item in output.OfType<JsonObject>())
        {
            var type = item["type"]?.GetValue<string>();
            switch (type)
            {
                case "message":
                    // Capture final text/structured payload from the first message item; keep both.
                    finalText ??= ExtractText(item["content"]);
                    structured ??= ExtractStructured(item["content"]);
                    items.Add(new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", plan.EffectiveAgent.Name)
                    {
                        Text = finalText,
                        Data = structured,
                        TimestampUtc = DateTimeOffset.UtcNow,
                    });
                    break;
                case "reasoning":
                    items.Add(MapReasoningItem(plan.EffectiveAgent.Name, item));
                    break;
                case "function_call":
                case "tool_call":
                {
                    var toolName = item["name"]?.GetValue<string>() ?? string.Empty;
                    // Resolve handoff calls separately from generic tool calls to route execution.
                    JsonNode? arguments = ParseJsonNode(item["arguments"]);
                    if (plan.HandoffMap.TryGetValue(toolName, out AgentHandoff<TContext>? handoff))
                    {
                        handoffs.Add(new AgentHandoffRequest<TContext>(handoff.Name, handoff.TargetAgent, arguments, item["status"]?.GetValue<string>()));
                    }
                    else
                    {
                        toolCalls.Add(new AgentToolCall<TContext>(
                            item["call_id"]?.GetValue<string>() ?? item["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("n"),
                            toolName,
                            arguments,
                            item["approval_required"]?.GetValue<bool>() ?? false,
                            item["approval_reason"]?.GetValue<string>(),
                            item["tool_type"]?.GetValue<string>() ?? "function"));
                    }

                    break;
                }
                case "mcp_list_tools":
                    // Expose raw MCP tool-list events in the run item stream for observability/debugging.
                    items.Add(new AgentRunItem(AgentItemTypes.McpListTools, "system", plan.EffectiveAgent.Name)
                    {
                        Data = item.DeepClone(),
                        TimestampUtc = DateTimeOffset.UtcNow,
                    });
                    break;
            }
        }

        AgentFinalOutput? finalOutput = null;
        if (handoffs.Count == 0 && toolCalls.Count == 0)
        {
            if (finalText is null && structured is not null)
            {
                finalText = structured.ToJsonString(SerializerOptions);
            }

            finalOutput = new AgentFinalOutput(finalText, structured, null, response.Id);
        }

        return new AgentTurnResponse<TContext>(finalOutput, toolCalls, handoffs, items, response.Id, plan.EffectiveAgent);
    }

    internal static AgentRunItem? TryMapStreamingOutputItem(string agentName, JsonObject item)
    {
        var type = item["type"]?.GetValue<string>();
        // Keep streaming output mapping aligned with non-streaming mapping types to avoid behavioral drift.
        return type switch
        {
            "message" => new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", agentName)
            {
                Text = ExtractText(item["content"]),
                Data = ExtractStructured(item["content"]),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            "reasoning" => MapReasoningItem(agentName, item),
            "function_call" or "tool_call" => new AgentRunItem(AgentItemTypes.ToolCall, "assistant", agentName)
            {
                Name = item["name"]?.GetValue<string>(),
                ToolCallId = item["call_id"]?.GetValue<string>() ?? item["id"]?.GetValue<string>(),
                Data = ParseJsonNode(item["arguments"]),
                Status = item["status"]?.GetValue<string>(),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            "mcp_list_tools" => new AgentRunItem(AgentItemTypes.McpListTools, "system", agentName)
            {
                Data = item.DeepClone(),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            _ => null,
        };
    }

    private static AgentRunItem MapReasoningItem(string agentName, JsonObject item)
        => new(AgentItemTypes.Reasoning, "assistant", agentName)
        {
            Data = item.DeepClone(),
            TimestampUtc = DateTimeOffset.UtcNow,
        };

    private static string? ExtractText(JsonNode? content)
    {
        // For message nodes, concatenate text fragments in stable order to reconstruct final assistant output.
        if (content is JsonArray array)
        {
            var parts = array
                .OfType<JsonObject>()
                .Select(node => node["text"]?.GetValue<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
            return parts.Length > 0 ? string.Join(Environment.NewLine, parts) : null;
        }

        return content?["text"]?.GetValue<string>();
    }

    private static JsonNode? ExtractStructured(JsonNode? content)
    {
        // Structured payload is represented as the first non-output-text component (if present).
        if (content is JsonArray array)
        {
            return array.OfType<JsonObject>().FirstOrDefault(node => node["type"]?.GetValue<string>() is not "output_text");
        }

        return null;
    }

    private static JsonNode? ParseJsonNode(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        // Some APIs return JSON text in a string field; parse it when possible and
        // keep a wrapped fallback node when parsing fails.
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                return JsonNode.Parse(text);
            }
            catch
            {
                return new JsonObject { ["value"] = text };
            }
        }

        return node.DeepClone();
    }
}
