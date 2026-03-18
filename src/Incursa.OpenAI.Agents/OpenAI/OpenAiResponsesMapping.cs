using System.Text.Json;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents;

internal sealed class OpenAiResponsesRequestMapper
{
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
        // Validate required model input before constructing the provider payload.
        if (string.IsNullOrWhiteSpace(request.Agent.Model))
        {
            throw new InvalidOperationException($"Agent '{request.Agent.Name}' must specify a model for OpenAI Responses runs.");
        }

        AgentRunOptions<TContext> options = request.Options ?? new AgentRunOptions<TContext>();
        // Resolve dynamic instructions from context/agent composition.
        var instructions = request.Agent.Instructions is null
            ? null
            : await request.Agent.Instructions.ResolveAsync(
                new AgentInstructionContext<TContext>(request.Agent, request.Context, request.SessionKey, request.Conversation),
                cancellationToken).ConfigureAwait(false);

        var body = new JsonObject
        {
            ["model"] = request.Agent.Model,
            ["instructions"] = instructions,
            ["previous_response_id"] = request.PreviousResponseId,
            ["metadata"] = new JsonObject
            {
                ["session_key"] = request.SessionKey,
                ["agent_name"] = request.Agent.Name,
                ["turn_number"] = request.TurnNumber,
            },
            ["input"] = await BuildInputItemsAsync(request, options, cancellationToken).ConfigureAwait(false),
        };

        // Carry forward any caller-supplied model configuration keys unless already explicit in the payload.
        foreach (KeyValuePair<string, object?> pair in request.Agent.ModelSettings)
        {
            if (body.ContainsKey(pair.Key))
            {
                continue;
            }

            body[pair.Key] = JsonSerializer.SerializeToNode(pair.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        var tools = new JsonArray();
        var handoffMap = new Dictionary<string, AgentHandoff<TContext>>(StringComparer.Ordinal);

        // Register user-defined function tools.
        foreach (IAgentTool<TContext> tool in request.Agent.Tools)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = tool.InputSchema?.DeepClone() ?? CreateDefaultToolSchema(),
            });
        }

        // Register conditional handoff tools only when enabled for the current turn context.
        foreach (AgentHandoff<TContext> handoff in request.Agent.Handoffs)
        {
            if (!await IsEnabledAsync(handoff, request, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var toolName = handoff.ToolNameOverride ?? ToHandoffToolName(handoff.TargetAgent.Name);
            handoffMap[toolName] = handoff;
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["name"] = toolName,
                ["description"] = handoff.Description ?? handoff.TargetAgent.HandoffDescription ?? $"Transfer to {handoff.TargetAgent.Name}.",
                ["parameters"] = handoff.InputSchema?.DeepClone() ?? CreateDefaultToolSchema(),
            });
        }

        // Register hosted MCP tools; apply auth/context factory at build time when configured.
        foreach (HostedMcpToolDefinition hostedMcp in request.Agent.HostedMcpTools)
        {
            HostedMcpToolDefinition resolved = hostedMcp;
            McpAuthContext? authContext = authContextFactory?.Invoke(request);
            if (hostedMcpToolFactory is not null)
            {
                resolved = await hostedMcpToolFactory.CreateAsync(hostedMcp, authContext, cancellationToken).ConfigureAwait(false);
            }

            var hosted = new JsonObject
            {
                ["type"] = "mcp",
                ["server_label"] = resolved.ServerLabel,
                ["server_url"] = resolved.ServerUrl?.ToString(),
                ["connector_id"] = resolved.ConnectorId,
                ["authorization"] = resolved.Authorization,
                ["require_approval"] = resolved.ApprovalRequired ? "always" : "never",
                ["approval_reason"] = resolved.ApprovalReason,
                ["description"] = resolved.Description,
            };

            if (resolved.Headers is not null)
            {
                hosted["headers"] = JsonSerializer.SerializeToNode(resolved.Headers, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }

            tools.Add(hosted);
        }

        // Add the final tools collection only when non-empty so model input stays minimal.
        if (tools.Count > 0)
        {
            body["tools"] = tools;
        }

        // Convert an explicit output contract into an OpenAI json_schema response_format.
        if (request.Agent.OutputContract is not null)
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = request.Agent.OutputContract.Name ?? request.Agent.OutputContract.ClrType.Name,
                    ["schema"] = OpenAiJsonSchemaGenerator.CreateSchema(request.Agent.OutputContract.ClrType),
                    ["strict"] = true,
                },
            };
        }

        return new OpenAiResponsesTurnPlan<TContext>(body, request.Agent, handoffMap);
    }

    private static async ValueTask<JsonArray> BuildInputItemsAsync<TContext>(
        AgentTurnRequest<TContext> request,
        AgentRunOptions<TContext> options,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentConversationItem> conversation = request.Conversation;

        // Optionally normalize handoff history to remove tool noise before the model sees it.
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

        // Apply an optional user-provided input filter as the final shape transform.
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

        var input = new JsonArray();
        foreach (AgentConversationItem item in conversation)
        {
            // Preserve one-to-one mapping between internal conversation items and provider item schema.
            input.Add(item.ItemType switch
            {
                AgentItemTypes.UserInput or AgentItemTypes.MessageOutput or AgentItemTypes.FinalOutput or AgentItemTypes.GuardrailTripwire or AgentItemTypes.ApprovalRejected
                    => new JsonObject
                    {
                        ["type"] = "message",
                        ["role"] = item.Role,
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = item.Role == "assistant" ? "output_text" : "input_text",
                                ["text"] = item.Text,
                            },
                        },
                    },
                AgentItemTypes.ToolCall
                    => new JsonObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = item.ToolCallId,
                        ["name"] = item.Name,
                        ["arguments"] = item.Data?.ToJsonString(),
                    },
                AgentItemTypes.ToolOutput
                    => new JsonObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = item.ToolCallId,
                        ["output"] = item.Text ?? item.Data?.ToJsonString(),
                    },
                AgentItemTypes.Reasoning
                    => NormalizeReasoningNode(item.Data, options.ReasoningItemIdPolicy),
                _ => new JsonObject
                {
                    ["type"] = item.ItemType,
                    ["role"] = item.Role,
                    ["name"] = item.Name,
                    ["content"] = item.Text,
                    ["tool_call_id"] = item.ToolCallId,
                    ["data"] = item.Data?.DeepClone(),
                },
            });
        }

        return input;
    }

    private static bool ShouldNormalizeHandoffInput<TContext>(AgentTurnRequest<TContext> request, AgentRunOptions<TContext> options)
        => options.HandoffHistoryMode == AgentHandoffHistoryMode.NormalizeModelInputAfterHandoff
            && request.Conversation.Any(item => item.ItemType == AgentItemTypes.HandoffOccurred && string.Equals(item.AgentName, request.Agent.Name, StringComparison.Ordinal));

    private static IReadOnlyList<AgentConversationItem> NormalizeConversationForHandoff(IReadOnlyList<AgentConversationItem> conversation)
        => conversation
            // Hide noisy handoff/tool plumbing and keep only the most relevant model context.
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

    private static JsonNode NormalizeReasoningNode(JsonNode? node, ReasoningItemIdPolicy policy)
    {
        // Clone the reasoning payload and optionally remove generated IDs depending on policy.
        JsonObject clone = node?.DeepClone() as JsonObject ?? new JsonObject { ["type"] = "reasoning" };
        if (policy == ReasoningItemIdPolicy.Omit)
        {
            clone.Remove("id");
        }

        return clone;
    }

    private static async ValueTask<bool> IsEnabledAsync<TContext>(AgentHandoff<TContext> handoff, AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
    {
        if (handoff.IsEnabledAsync is null)
        {
            return true;
        }

        // Ask the handoff-specific policy whether it is currently allowed.
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
        var chars = agentName.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return $"transfer_to_{new string(chars).Trim('_')}";
    }
}
