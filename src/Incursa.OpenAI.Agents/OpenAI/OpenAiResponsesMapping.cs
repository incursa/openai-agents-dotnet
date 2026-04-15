#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents;

internal sealed class OpenAiResponsesRequestMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HostedMcpToolFactory? hostedMcpToolFactory;
    private readonly Func<object?, McpAuthContext?>? authContextFactory;

    internal OpenAiResponsesRequestMapper(
        HostedMcpToolFactory? hostedMcpToolFactory = null,
        Func<object?, McpAuthContext?>? authContextFactory = null)
    {
        this.hostedMcpToolFactory = hostedMcpToolFactory;
        this.authContextFactory = authContextFactory;
    }

    internal async ValueTask<OpenAiResponsesTurnPlan<TContext>> CreateAsync<TContext>(
        AgentTurnRequest<TContext> request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Agent.Model))
        {
            throw new InvalidOperationException($"Agent '{request.Agent.Name}' must specify a model for OpenAI Responses runs.");
        }

        AgentRunOptions<TContext> options = request.Options ?? new AgentRunOptions<TContext>();
        string? instructions = request.Agent.Instructions is null
            ? null
            : await request.Agent.Instructions.ResolveAsync(
                new AgentInstructionContext<TContext>(request.Agent, request.Context, request.SessionKey, request.Conversation),
                cancellationToken).ConfigureAwait(false);

        CreateResponseOptions requestOptions = new()
        {
            Model = request.Agent.Model,
            Instructions = instructions,
            PreviousResponseId = request.PreviousResponseId,
        };

        requestOptions.Metadata["session_key"] = request.SessionKey;
        requestOptions.Metadata["agent_name"] = request.Agent.Name;
        requestOptions.Metadata["turn_number"] = request.TurnNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        IReadOnlyList<ResponseItem> inputItems = await BuildInputItemsAsync(request, options, cancellationToken).ConfigureAwait(false);
        foreach (ResponseItem inputItem in inputItems)
        {
            requestOptions.InputItems.Add(inputItem);
        }

        Dictionary<string, AgentHandoff<TContext>> handoffMap = new(StringComparer.Ordinal);

        foreach (IAgentTool<TContext> tool in request.Agent.Tools)
        {
            requestOptions.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.Name,
                BinaryData.FromString((tool.InputSchema?.DeepClone() ?? CreateDefaultToolSchema()).ToJsonString(SerializerOptions)),
                strictModeEnabled: null,
                functionDescription: tool.Description));
        }

        foreach (AgentHandoff<TContext> handoff in request.Agent.Handoffs)
        {
            if (!await IsEnabledAsync(handoff, request, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            string toolName = handoff.ToolNameOverride ?? ToHandoffToolName(handoff.TargetAgent.Name);
            handoffMap[toolName] = handoff;

            requestOptions.Tools.Add(ResponseTool.CreateFunctionTool(
                toolName,
                BinaryData.FromString((handoff.InputSchema?.DeepClone() ?? CreateDefaultToolSchema()).ToJsonString(SerializerOptions)),
                strictModeEnabled: null,
                functionDescription: handoff.Description ?? handoff.TargetAgent.HandoffDescription ?? $"Transfer to {handoff.TargetAgent.Name}."));
        }

        foreach (HostedMcpToolDefinition hostedMcp in request.Agent.HostedMcpTools)
        {
            HostedMcpToolDefinition resolved = hostedMcp;
            McpAuthContext? authContext = authContextFactory?.Invoke(request);
            if (hostedMcpToolFactory is not null)
            {
                resolved = await hostedMcpToolFactory.CreateAsync(hostedMcp, authContext, cancellationToken).ConfigureAwait(false);
            }

            requestOptions.Tools.Add(CreateHostedMcpTool(resolved));
        }

        if (request.Agent.OutputContract is not null)
        {
            requestOptions.TextOptions ??= new ResponseTextOptions();
            requestOptions.TextOptions.TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                request.Agent.OutputContract.Name ?? "structured_output",
                BinaryData.FromString(request.Agent.OutputContract.Schema.ToJsonString(SerializerOptions)),
                jsonSchemaIsStrict: true);
        }

        ApplyModelSettings(ref requestOptions, request.Agent.ModelSettings);
        return new OpenAiResponsesTurnPlan<TContext>(requestOptions, request.Agent, handoffMap);
    }

    private static async ValueTask<IReadOnlyList<ResponseItem>> BuildInputItemsAsync<TContext>(
        AgentTurnRequest<TContext> request,
        AgentRunOptions<TContext> options,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentConversationItem> conversation = request.Conversation;

        if (ShouldNormalizeHandoffInput(request, options))
        {
            conversation = options.HandoffHistoryTransformerAsync is not null
                ? await options.HandoffHistoryTransformerAsync(
                    new AgentHandoffHistoryTransformContext<TContext>(
                        FindLastHandoffSourceAgentName(request.Conversation) ?? request.Agent.Name,
                        request.Agent.Name,
                        request.Context,
                        request.SessionKey,
                        FindLastHandoffArguments(request.Conversation, request.Agent.Name),
                        request.Conversation),
                    cancellationToken).ConfigureAwait(false)
                : NormalizeConversationForHandoff(request.Conversation);
        }

        if (options.ModelInputFilterAsync is not null)
        {
            conversation = await options.ModelInputFilterAsync(
                new AgentModelInputFilterContext<TContext>(
                    request.Agent,
                    request.Context,
                    request.SessionKey,
                    request.TurnNumber,
                    request.PreviousResponseId,
                    conversation),
                cancellationToken).ConfigureAwait(false);
        }

        List<ResponseItem> input = [];
        foreach (AgentConversationItem item in conversation)
        {
            input.Add(MapConversationItem(item, options.ReasoningItemIdPolicy));
        }

        return input;
    }

    private static ResponseItem MapConversationItem(AgentConversationItem item, ReasoningItemIdPolicy reasoningItemIdPolicy)
        => item.ItemType switch
        {
            AgentItemTypes.UserInput or AgentItemTypes.MessageOutput or AgentItemTypes.FinalOutput or AgentItemTypes.GuardrailTripwire or AgentItemTypes.ApprovalRejected
                => CreateMessageItem(item),
            AgentItemTypes.ToolCall
                => CreateFunctionCallItem(item),
            AgentItemTypes.ToolOutput
                => CreateFunctionCallOutputItem(item),
            AgentItemTypes.Reasoning
                => CreateReasoningItem(item.Data, reasoningItemIdPolicy),
            _ => OpenAiSdkSerialization.ReadModel<ResponseItem>(new JsonObject
            {
                ["type"] = item.ItemType,
                ["role"] = item.Role,
                ["name"] = item.Name,
                ["content"] = item.Text,
                ["tool_call_id"] = item.ToolCallId,
                ["data"] = item.Data?.DeepClone(),
            }),
        };

    private static MessageResponseItem CreateMessageItem(AgentConversationItem item)
    {
        ResponseContentPart contentPart = item.Role == "assistant"
            ? ResponseContentPart.CreateOutputTextPart(item.Text ?? string.Empty, [])
            : ResponseContentPart.CreateInputTextPart(item.Text ?? string.Empty);

        MessageResponseItem message = item.Role switch
        {
            "assistant" => ResponseItem.CreateAssistantMessageItem([contentPart]),
            "system" => ResponseItem.CreateSystemMessageItem([contentPart]),
            "developer" => ResponseItem.CreateDeveloperMessageItem([contentPart]),
            _ => ResponseItem.CreateUserMessageItem([contentPart]),
        };

        return message;
    }

    private static FunctionCallResponseItem CreateFunctionCallItem(AgentConversationItem item)
    {
        FunctionCallResponseItem toolCall = ResponseItem.CreateFunctionCallItem(
            item.ToolCallId ?? Guid.NewGuid().ToString("n"),
            item.Name ?? string.Empty,
            BinaryData.FromString((item.Data?.DeepClone() ?? new JsonObject()).ToJsonString(SerializerOptions)));

        if (TryParseFunctionCallStatus(item.Status, out FunctionCallStatus status))
        {
            toolCall.Status = status;
        }

        return toolCall;
    }

    private static FunctionCallOutputResponseItem CreateFunctionCallOutputItem(AgentConversationItem item)
    {
        FunctionCallOutputResponseItem toolOutput = ResponseItem.CreateFunctionCallOutputItem(
            item.ToolCallId ?? Guid.NewGuid().ToString("n"),
            item.Text ?? item.Data?.ToJsonString(SerializerOptions) ?? string.Empty);

        if (TryParseFunctionCallOutputStatus(item.Status, out FunctionCallOutputStatus status))
        {
            toolOutput.Status = status;
        }

        return toolOutput;
    }

    private static ReasoningResponseItem CreateReasoningItem(JsonNode? node, ReasoningItemIdPolicy policy)
    {
        JsonObject normalized = NormalizeReasoningNode(node, policy);
        string summaryText = ExtractReasoningSummaryText(normalized["summary"]);
        ReasoningResponseItem reasoning = ResponseItem.CreateReasoningItem(summaryText);

        if (normalized["id"] is JsonValue idValue && idValue.TryGetValue<string>(out string? id) && !string.IsNullOrWhiteSpace(id))
        {
            reasoning.Id = id;
        }

        if (normalized["status"] is JsonValue statusValue
            && statusValue.TryGetValue<string>(out string? status)
            && Enum.TryParse(status, ignoreCase: true, out ReasoningStatus reasoningStatus))
        {
            reasoning.Status = reasoningStatus;
        }

        if (normalized["encrypted_content"] is JsonValue encryptedValue
            && encryptedValue.TryGetValue<string>(out string? encryptedContent)
            && !string.IsNullOrWhiteSpace(encryptedContent))
        {
            reasoning.EncryptedContent = encryptedContent;
        }

        return reasoning;
    }

    private static McpTool CreateHostedMcpTool(HostedMcpToolDefinition resolved)
    {
        McpTool tool = resolved.ServerUrl is not null
            ? new McpTool(resolved.ServerLabel, resolved.ServerUrl)
            : new McpTool(resolved.ServerLabel, resolved.ConnectorId ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(resolved.ConnectorId))
        {
            tool.ConnectorId = resolved.ConnectorId;
        }

        tool.AuthorizationToken = resolved.Authorization;
        tool.ServerDescription = resolved.Description;
        tool.Headers = resolved.Headers is null ? null : new Dictionary<string, string>(resolved.Headers, StringComparer.Ordinal);
        tool.ToolCallApprovalPolicy = resolved.ApprovalRequired
            ? GlobalMcpToolCallApprovalPolicy.AlwaysRequireApproval
            : GlobalMcpToolCallApprovalPolicy.NeverRequireApproval;

        if (!string.IsNullOrWhiteSpace(resolved.ApprovalReason))
        {
            tool.Patch.Set("$.approval_reason"u8, resolved.ApprovalReason);
        }

        return tool;
    }

    private static void ApplyModelSettings(ref CreateResponseOptions options, IReadOnlyDictionary<string, object?> modelSettings)
    {
        ref JsonPatch patch = ref options.Patch;
        foreach (KeyValuePair<string, object?> pair in modelSettings)
        {
            if (IsExplicitTopLevelField(pair.Key))
            {
                continue;
            }

            JsonNode? node = JsonSerializer.SerializeToNode(pair.Value, SerializerOptions);
            ApplyJsonPatchValue(ref patch, BuildJsonPath(pair.Key), node);
        }
    }

    private static void ApplyJsonPatchValue(ref JsonPatch patch, string path, JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> property in obj)
            {
                ApplyJsonPatchValue(ref patch, $"{path}.{property.Key}", property.Value);
            }

            return;
        }

        byte[] jsonPath = Encoding.UTF8.GetBytes(path);
        switch (node)
        {
            case null:
                patch.SetNull(jsonPath);
                return;
            case JsonValue value when value.TryGetValue<string>(out string? text):
                patch.Set(jsonPath, text);
                return;
            case JsonValue value when value.TryGetValue<bool>(out bool boolean):
                patch.Set(jsonPath, boolean);
                return;
            case JsonValue value when value.TryGetValue<int>(out int int32):
                patch.Set(jsonPath, int32);
                return;
            case JsonValue value when value.TryGetValue<long>(out long int64):
                patch.Set(jsonPath, int64);
                return;
            case JsonValue value when value.TryGetValue<double>(out double number):
                patch.Set(jsonPath, number);
                return;
            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                {
                    ApplyJsonPatchValue(ref patch, $"{path}[{i}]", array[i]);
                }
                return;
            default:
                patch.Set(jsonPath, node.ToJsonString(SerializerOptions));
                return;
        }
    }

    private static string BuildJsonPath(string propertyName)
        => $"$.{propertyName}";

    private static bool IsExplicitTopLevelField(string propertyName)
        => propertyName is "model" or "instructions" or "previous_response_id" or "metadata" or "input" or "tools" or "stream";

    private static bool TryParseFunctionCallStatus(string? value, out FunctionCallStatus status)
        => Enum.TryParse(value, ignoreCase: true, out status);

    private static bool TryParseFunctionCallOutputStatus(string? value, out FunctionCallOutputStatus status)
        => Enum.TryParse(value, ignoreCase: true, out status);

    private static bool ShouldNormalizeHandoffInput<TContext>(AgentTurnRequest<TContext> request, AgentRunOptions<TContext> options)
        => options.HandoffHistoryMode == AgentHandoffHistoryMode.NormalizeModelInputAfterHandoff
            && request.Conversation.Any(item => item.ItemType == AgentItemTypes.HandoffOccurred && string.Equals(item.AgentName, request.Agent.Name, StringComparison.Ordinal));

    private static IReadOnlyList<AgentConversationItem> NormalizeConversationForHandoff(IReadOnlyList<AgentConversationItem> conversation)
        => conversation
            .Where(item => item.ItemType is not AgentItemTypes.ToolCall
                and not AgentItemTypes.ToolOutput
                and not AgentItemTypes.Reasoning
                and not AgentItemTypes.ApprovalRequired
                and not AgentItemTypes.McpListTools)
            .ToArray();

    private static string? FindLastHandoffSourceAgentName(IReadOnlyList<AgentConversationItem> conversation)
        => conversation.LastOrDefault(item => item.ItemType == AgentItemTypes.HandoffRequested)?.AgentName;

    private static JsonNode? FindLastHandoffArguments(IReadOnlyList<AgentConversationItem> conversation, string targetAgentName)
        => conversation.LastOrDefault(item => item.ItemType == AgentItemTypes.HandoffOccurred && string.Equals(item.AgentName, targetAgentName, StringComparison.Ordinal))?.Data?.DeepClone();

    private static JsonObject NormalizeReasoningNode(JsonNode? node, ReasoningItemIdPolicy policy)
    {
        JsonObject clone = node?.DeepClone() as JsonObject ?? new JsonObject { ["type"] = "reasoning" };
        if (policy == ReasoningItemIdPolicy.Omit)
        {
            clone.Remove("id");
        }

        return clone;
    }

    private static string ExtractReasoningSummaryText(JsonNode? node)
    {
        if (node is not JsonArray summary)
        {
            return string.Empty;
        }

        List<string> parts = [];
        foreach (JsonNode? entry in summary)
        {
            switch (entry)
            {
                case JsonValue value when value.TryGetValue<string>(out string? text) && !string.IsNullOrWhiteSpace(text):
                    parts.Add(text);
                    break;
                case JsonObject obj when obj["text"] is JsonValue textValue && textValue.TryGetValue<string>(out string? text) && !string.IsNullOrWhiteSpace(text):
                    parts.Add(text);
                    break;
            }
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static async ValueTask<bool> IsEnabledAsync<TContext>(AgentHandoff<TContext> handoff, AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
    {
        if (handoff.IsEnabledAsync is null)
        {
            return true;
        }

        return await handoff.IsEnabledAsync(new AgentHandoffContext<TContext>(request.Agent, request.Context, request.SessionKey, request.Conversation), cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject CreateDefaultToolSchema()
        => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
            ["additionalProperties"] = true,
        };

    private static string ToHandoffToolName(string agentName)
    {
        char[] chars = agentName.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return $"transfer_to_{new string(chars).Trim('_')}";
    }
}
