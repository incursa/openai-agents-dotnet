#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using OpenAI.Responses;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

internal sealed class OpenAiResponsesResponseMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal AgentTurnResponse<TContext> Map<TContext>(OpenAiResponsesResponse response, OpenAiResponsesTurnPlan<TContext> plan)
    {
        List<AgentToolCall<TContext>> toolCalls = [];
        List<AgentHandoffRequest<TContext>> handoffs = [];
        List<AgentRunItem> items = [];
        string? finalText = null;
        JsonNode? structured = null;

        ResponseResult? result = response.Result;
        if (result is null)
        {
            try
            {
                result = OpenAiSdkSerialization.ReadModel<ResponseResult>(response.Raw);
            }
            catch
            {
            }
        }

        if (result is not null)
        {
            foreach (ResponseItem outputItem in result.OutputItems)
            {
                switch (outputItem)
                {
                    case MessageResponseItem message:
                        JsonObject rawMessage = SerializeModelSafely(message, "message");
                        finalText ??= ExtractText(rawMessage["content"]);
                        structured ??= ExtractStructured(rawMessage["content"]);
                        items.Add(new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", plan.EffectiveAgent.Name)
                        {
                            Text = finalText,
                            Data = structured,
                            TimestampUtc = DateTimeOffset.UtcNow,
                        });
                        break;

                    case ReasoningResponseItem reasoning:
                        items.Add(MapReasoningItem(plan.EffectiveAgent.Name, reasoning));
                        break;

                    case FunctionCallResponseItem functionCall:
                        MapToolCall(plan, toolCalls, handoffs, functionCall.CallId, functionCall.FunctionName, functionCall.FunctionArguments?.ToString(), SerializeModelSafely(functionCall, "function_call")["status"]?.GetValue<string>(), false, null, "function");
                        break;

                    case McpToolCallApprovalRequestItem approvalRequest:
                        toolCalls.Add(new AgentToolCall<TContext>(
                            approvalRequest.Id ?? Guid.NewGuid().ToString("n"),
                            approvalRequest.ToolName,
                            ParseJsonNode(approvalRequest.ToolArguments is null ? null : JsonValue.Create(approvalRequest.ToolArguments.ToString())),
                            true,
                            SerializeModelSafely(approvalRequest, "mcp_approval_request")["approval_reason"]?.GetValue<string>(),
                            "mcp"));
                        break;

                    case McpToolCallItem mcpCall:
                        MapToolCall(plan, toolCalls, handoffs, mcpCall.Id ?? Guid.NewGuid().ToString("n"), mcpCall.ToolName, mcpCall.ToolArguments?.ToString(), SerializeModelSafely(mcpCall, "mcp_call")["status"]?.GetValue<string>(), false, null, "mcp");
                        break;

                    case McpToolDefinitionListItem mcpList:
                        items.Add(new AgentRunItem(AgentItemTypes.McpListTools, "system", plan.EffectiveAgent.Name)
                        {
                            Data = SerializeModelSafely(mcpList, "mcp_list_tools"),
                            TimestampUtc = DateTimeOffset.UtcNow,
                        });
                        break;

                    default:
                        MapRawOutputItem(plan, SerializeModelSafely(outputItem, "unknown"), toolCalls, handoffs, items, ref finalText, ref structured);
                        break;
                }
            }
        }
        else if (response.Raw["output"] is JsonArray rawOutput)
        {
            foreach (JsonObject rawItem in rawOutput.OfType<JsonObject>())
            {
                MapRawOutputItem(plan, rawItem, toolCalls, handoffs, items, ref finalText, ref structured);
            }
        }

        AgentFinalOutput? finalOutput = null;
        if (handoffs.Count == 0 && toolCalls.Count == 0)
        {
            if (finalText is null && structured is not null)
            {
                finalText = structured.ToJsonString(SerializerOptions);
            }

            finalOutput = new AgentFinalOutput(finalText, structured, plan.EffectiveAgent.OutputContract?.ClrType, response.Id);
        }

        return new AgentTurnResponse<TContext>(finalOutput, toolCalls, handoffs, items, response.Id, plan.EffectiveAgent);
    }

    internal static AgentRunItem? TryMapStreamingOutputItem(string agentName, JsonObject item)
    {
        ResponseItem typedItem;
        try
        {
            typedItem = OpenAiSdkSerialization.ReadModel<ResponseItem>(item);
        }
        catch
        {
            return TryMapUnknownStreamingOutputItem(agentName, item);
        }

        return typedItem switch
        {
            MessageResponseItem => new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", agentName)
            {
                Text = ExtractText(item["content"]),
                Data = ExtractStructured(item["content"]),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            ReasoningResponseItem reasoning => MapReasoningItem(agentName, reasoning),
            FunctionCallResponseItem functionCall => new AgentRunItem(AgentItemTypes.ToolCall, "assistant", agentName)
            {
                Name = functionCall.FunctionName,
                ToolCallId = functionCall.CallId ?? functionCall.Id,
                Data = ParseJsonNode(JsonValue.Create(functionCall.FunctionArguments?.ToString())),
                Status = item["status"]?.GetValue<string>(),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            McpToolCallItem mcpCall => new AgentRunItem(AgentItemTypes.ToolCall, "assistant", agentName)
            {
                Name = mcpCall.ToolName,
                ToolCallId = mcpCall.Id,
                Data = ParseJsonNode(JsonValue.Create(mcpCall.ToolArguments?.ToString())),
                Status = item["status"]?.GetValue<string>(),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            McpToolDefinitionListItem => new AgentRunItem(AgentItemTypes.McpListTools, "system", agentName)
            {
                Data = item.DeepClone(),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            _ => TryMapUnknownStreamingOutputItem(agentName, item),
        };
    }

    private static void MapToolCall<TContext>(
        OpenAiResponsesTurnPlan<TContext> plan,
        List<AgentToolCall<TContext>> toolCalls,
        List<AgentHandoffRequest<TContext>> handoffs,
        string? callId,
        string toolName,
        string? argumentsJson,
        string? status,
        bool requiresApproval,
        string? approvalReason,
        string toolType)
    {
        JsonNode? arguments = ParseJsonNode(argumentsJson is null ? null : JsonValue.Create(argumentsJson));
        if (plan.HandoffMap.TryGetValue(toolName, out AgentHandoff<TContext>? handoff))
        {
            handoffs.Add(new AgentHandoffRequest<TContext>(handoff.Name, handoff.TargetAgent, arguments, status));
            return;
        }

        toolCalls.Add(new AgentToolCall<TContext>(
            callId ?? Guid.NewGuid().ToString("n"),
            toolName,
            arguments,
            requiresApproval,
            approvalReason,
            toolType));
    }

    private static void MapRawOutputItem<TContext>(
        OpenAiResponsesTurnPlan<TContext> plan,
        JsonObject rawItem,
        List<AgentToolCall<TContext>> toolCalls,
        List<AgentHandoffRequest<TContext>> handoffs,
        List<AgentRunItem> items,
        ref string? finalText,
        ref JsonNode? structured)
    {
        string? type = rawItem["type"]?.GetValue<string>();

        switch (type)
        {
            case "message":
                finalText ??= ExtractText(rawItem["content"]);
                structured ??= ExtractStructured(rawItem["content"]);
                items.Add(new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", plan.EffectiveAgent.Name)
                {
                    Text = finalText,
                    Data = structured,
                    TimestampUtc = DateTimeOffset.UtcNow,
                });
                break;

            case "reasoning":
                items.Add(new AgentRunItem(AgentItemTypes.Reasoning, "assistant", plan.EffectiveAgent.Name)
                {
                    Data = rawItem.DeepClone(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                });
                break;

            case "function_call" or "tool_call":
                MapToolCall(plan, toolCalls, handoffs, rawItem["call_id"]?.GetValue<string>() ?? rawItem["id"]?.GetValue<string>(), rawItem["name"]?.GetValue<string>() ?? string.Empty, rawItem["arguments"]?.GetValue<string>(), rawItem["status"]?.GetValue<string>(), rawItem["approval_required"]?.GetValue<bool>() ?? false, rawItem["approval_reason"]?.GetValue<string>(), rawItem["tool_type"]?.GetValue<string>() ?? "function");
                break;

            case "mcp_approval_request":
                toolCalls.Add(new AgentToolCall<TContext>(
                    rawItem["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("n"),
                    rawItem["name"]?.GetValue<string>() ?? string.Empty,
                    ParseJsonNode(rawItem["arguments"]),
                    true,
                    rawItem["approval_reason"]?.GetValue<string>(),
                    "mcp"));
                break;

            case "mcp_call" or "mcp_tool_call":
                MapToolCall(plan, toolCalls, handoffs, rawItem["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("n"), rawItem["name"]?.GetValue<string>() ?? string.Empty, rawItem["arguments"]?.GetValue<string>(), rawItem["status"]?.GetValue<string>(), false, null, "mcp");
                break;

            case "mcp_list_tools":
                items.Add(new AgentRunItem(AgentItemTypes.McpListTools, "system", plan.EffectiveAgent.Name)
                {
                    Data = rawItem.DeepClone(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                });
                break;
        }
    }

    private static AgentRunItem? TryMapUnknownStreamingOutputItem(string agentName, JsonObject item)
    {
        string? type = item["type"]?.GetValue<string>();
        return type switch
        {
            "message" => new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", agentName)
            {
                Text = ExtractText(item["content"]),
                Data = ExtractStructured(item["content"]),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
            "reasoning" => new AgentRunItem(AgentItemTypes.Reasoning, "assistant", agentName)
            {
                Data = item.DeepClone(),
                TimestampUtc = DateTimeOffset.UtcNow,
            },
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

    private static AgentRunItem MapReasoningItem(string agentName, ReasoningResponseItem item)
        => new(AgentItemTypes.Reasoning, "assistant", agentName)
        {
            Data = SerializeModelSafely(item, "reasoning"),
            TimestampUtc = DateTimeOffset.UtcNow,
        };

    private static JsonObject SerializeModelSafely<T>(T value, string fallbackType)
    {
        try
        {
            return OpenAiSdkSerialization.ToJsonObject(value);
        }
        catch
        {
            return new JsonObject
            {
                ["type"] = fallbackType,
            };
        }
    }

    private static string? ExtractText(JsonNode? content)
    {
        if (content is JsonArray array)
        {
            string[] parts = array
                .OfType<JsonObject>()
                .Select(node => node["text"]?.GetValue<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray()!;
            return parts.Length > 0 ? string.Join(Environment.NewLine, parts) : null;
        }

        return content?["text"]?.GetValue<string>();
    }

    private static JsonNode? ExtractStructured(JsonNode? content)
    {
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

        if (node is JsonValue value && value.TryGetValue<string>(out string? text) && !string.IsNullOrWhiteSpace(text))
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
