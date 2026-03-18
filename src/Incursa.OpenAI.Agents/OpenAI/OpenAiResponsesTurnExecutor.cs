using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents;

internal sealed class OpenAiResponsesTurnExecutor<TContext> : IStreamingAgentTurnExecutor<TContext>
{
    private readonly IOpenAiResponsesClient client;
    private readonly Func<AgentTurnRequest<TContext>, McpAuthContext?>? authContextFactory;
    private readonly IUserScopedMcpAuthResolver? authResolver;
    private readonly HttpClient mcpHttpClient;
    private readonly IMcpToolMetadataResolver? mcpToolMetadataResolver;
    private readonly McpClientOptions? mcpClientOptions;

    // Creates the executor with optional MCP/session dependencies used when runtime tool discovery
    // and authorization are required for a turn.
    internal OpenAiResponsesTurnExecutor(
        IOpenAiResponsesClient client,
        HttpClient? mcpHttpClient = null,
        IUserScopedMcpAuthResolver? authResolver = null,
        Func<AgentTurnRequest<TContext>, McpAuthContext?>? authContextFactory = null,
        IMcpToolMetadataResolver? mcpToolMetadataResolver = null,
        McpClientOptions? mcpClientOptions = null)
    {
        this.client = client;
        this.authResolver = authResolver;
        this.authContextFactory = authContextFactory;
        this.mcpHttpClient = mcpHttpClient ?? new HttpClient();
        this.mcpToolMetadataResolver = mcpToolMetadataResolver;
        this.mcpClientOptions = mcpClientOptions;
    }

    /// <summary>
    /// Creates a streaming turn executor that can resolve runtime MCP tools and auth context.
    /// </summary>

    public async ValueTask<AgentTurnResponse<TContext>> ExecuteTurnAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
    {
        // 1) Expand agent tools from any configured streamable MCP servers.
        Agent<TContext> effectiveAgent = await AugmentWithRuntimeMcpToolsAsync(request, cancellationToken).ConfigureAwait(false);
        AgentTurnRequest<TContext> effectiveRequest = request with { Agent = effectiveAgent };

        // 2) Build MCP-aware tool factory only when auth plumbing is actually configured.
        HostedMcpToolFactory? hostedFactory = authContextFactory is null && authResolver is null
            ? null
            : new HostedMcpToolFactory(authResolver, () => authContextFactory?.Invoke(request) ?? new McpAuthContext());
        var mapper = new OpenAiResponsesRequestMapper(hostedFactory, authContextFactory is null ? null : _ => authContextFactory(request));

        // 3) Translate the turn request into the OpenAI responses wire format and execute once.
        OpenAiResponsesTurnPlan<TContext> plan = await mapper.CreateAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);
        OpenAiResponsesResponse response = await client.CreateResponseAsync(new OpenAiResponsesRequest(plan.Body), cancellationToken).ConfigureAwait(false);
        return new OpenAiResponsesResponseMapper().Map(response, plan);
    }

    public async ValueTask<AgentTurnResponse<TContext>> ExecuteStreamingTurnAsync(
        AgentTurnRequest<TContext> request,
        Func<AgentStreamEvent, ValueTask> emitAsync,
        CancellationToken cancellationToken)
    {
        // Same execution pipeline as non-streaming, but keep raw model events and intermediate items live.
        Agent<TContext> effectiveAgent = await AugmentWithRuntimeMcpToolsAsync(request, cancellationToken).ConfigureAwait(false);
        AgentTurnRequest<TContext> effectiveRequest = request with { Agent = effectiveAgent };

        HostedMcpToolFactory? hostedFactory = authContextFactory is null && authResolver is null
            ? null
            : new HostedMcpToolFactory(authResolver, () => authContextFactory?.Invoke(request) ?? new McpAuthContext());
        var mapper = new OpenAiResponsesRequestMapper(hostedFactory, authContextFactory is null ? null : _ => authContextFactory(request));
        OpenAiResponsesTurnPlan<TContext> plan = await mapper.CreateAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);

        var accumulator = new StreamingResponseAccumulator();

        // Stream in-order model deltas, emit raw events, and emit normalized run items when complete frames arrive.
        await foreach (OpenAiResponsesStreamEvent? streamEvent in client.StreamResponseAsync(new OpenAiResponsesRequest(plan.Body, true), cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            accumulator.Accept(streamEvent);
            await emitAsync(new AgentStreamEvent(AgentStreamEventTypes.RawModelEvent, plan.EffectiveAgent.Name)
            {
                Data = streamEvent.Data.DeepClone(),
                TimestampUtc = DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);

            if (accumulator.TryCreateRunItem(streamEvent, plan.EffectiveAgent.Name, out AgentRunItem? runItem))
            {
                await emitAsync(new AgentStreamEvent(AgentStreamEventTypes.RunItem, plan.EffectiveAgent.Name, runItem, null, null, runItem!.TimestampUtc)).ConfigureAwait(false);
            }
        }

        OpenAiResponsesResponse response = accumulator.CreateResponse();
        return new OpenAiResponsesResponseMapper().Map(response, plan);
    }

    private async Task<Agent<TContext>> AugmentWithRuntimeMcpToolsAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
    {
        if (request.Agent.StreamableMcpServers.Count == 0)
        {
            return request.Agent;
        }

        // Resolve auth context once per turn and construct a streamable MCP client factory.
        McpAuthContext authContext = authContextFactory?.Invoke(request) ?? new McpAuthContext
        {
            SessionKey = request.SessionKey,
            AgentName = request.Agent.Name,
        };
        var factory = new StreamableMcpClientFactory(mcpHttpClient, authResolver, () => authContext, mcpToolMetadataResolver, mcpClientOptions);
        var tools = request.Agent.Tools.ToList();

        // For each MCP server, enumerate tools and register proxy tools that invoke the server tool through the MCP client.
        foreach (StreamableHttpMcpServerDefinition server in request.Agent.StreamableMcpServers)
        {
            IStreamableMcpClient client = factory.Create(server, authContext);
            IReadOnlyList<McpToolDescriptor> descriptors = await client.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            foreach (McpToolDescriptor descriptor in descriptors)
            {
                var proxyName = CreateMcpProxyToolName(server.ServerLabel, descriptor.Name);
                tools.Add(new AgentTool<TContext>
                {
                    Name = proxyName,
                    Description = descriptor.Description ?? $"Call {descriptor.Name} on MCP server {server.ServerLabel}.",
                    RequiresApproval = descriptor.RequiresApproval || server.ApprovalRequired,
                    InputSchema = descriptor.InputSchema,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["mcp_server"] = server.ServerLabel,
                        ["mcp_tool_name"] = descriptor.Name,
                    },
                    ExecuteAsync = async (invocation, ct) =>
                    {
                        // Invoke the remote MCP tool and wrap the transport result in the agent tool contract.
                        McpToolCallResult result = await client.CallToolAsync(descriptor.Name, invocation.Arguments, ct).ConfigureAwait(false);
                        return new AgentToolResult(result.Text, result.Raw);
                    },
                });
            }
        }

        return request.Agent.CloneWith(tools);
    }

    private static string CreateMcpProxyToolName(string serverLabel, string toolName)
    {
        // Normalize MCP metadata into safe function names with deterministic underscore separators.
        static string Normalize(string value) => new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        return $"mcp_{Normalize(serverLabel)}__{Normalize(toolName)}";
    }

    private sealed class StreamingResponseAccumulator
    {
        // Collects streaming output frames and reconstructs final output items (including function-arg deltas).
        private readonly JsonArray output = [];
        // Tracks in-progress function call output fragments by output_index until completion.
        private readonly Dictionary<int, JsonObject> pendingByOutputIndex = [];
        private string? responseId;

        /// <summary>
        /// Consumes streaming model events and rebuilds the final response payload.
        /// </summary>

        public void Accept(OpenAiResponsesStreamEvent streamEvent)
        {
            if (streamEvent.Data["response"]?["id"] is JsonValue responseId && responseId.TryGetValue<string>(out var value))
            {
                this.responseId = value;
            }

            // Delegate each event type to the assembler rule for that transition.
            switch (streamEvent.Type)
            {
                case "response.output_item.added":
                    // Seed a pending item shell so later delta packets can append arguments.
                    if (streamEvent.Data["item"] is JsonObject addedItem)
                    {
                        pendingByOutputIndex[GetOutputIndex(streamEvent.Data)] = addedItem.DeepClone() as JsonObject ?? new JsonObject();
                    }

                    break;
                case "response.function_call_arguments.delta":
                    AppendArgumentsDelta(streamEvent.Data);
                    break;
                case "response.function_call_arguments.done":
                    CompleteArguments(streamEvent.Data);
                    break;
                case "response.output_item.done":
                    // Merge pending incremental arguments and persist final output item.
                    if (streamEvent.Data["item"] is JsonObject doneItem)
                    {
                        JsonObject merged = MergePendingArguments(doneItem, GetOutputIndex(streamEvent.Data));
                        this.output.Add(merged.DeepClone());
                    }

                    break;
                case "response.completed":
                    // Server may emit a full final output array; treat that as the source of truth when present.
                    if (streamEvent.Data["response"] is JsonObject response && response["output"] is JsonArray output)
                    {
                        this.output.Clear();
                        foreach (JsonNode? item in output)
                        {
                            this.output.Add(item?.DeepClone());
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Emits a normalized run item when a streamed output item is complete.
        /// </summary>

        public bool TryCreateRunItem(OpenAiResponsesStreamEvent streamEvent, string agentName, out AgentRunItem? runItem)
        {
            runItem = null;
            if (!string.Equals(streamEvent.Type, "response.output_item.done", StringComparison.Ordinal))
            {
                return false;
            }

            if (streamEvent.Data["item"] is not JsonObject doneItem)
            {
                return false;
            }

            // Emit a normalized streaming run item only when the final item frame can be mapped.
            JsonObject merged = MergePendingArguments(doneItem, GetOutputIndex(streamEvent.Data));
            runItem = OpenAiResponsesResponseMapper.TryMapStreamingOutputItem(agentName, merged);
            return runItem is not null;
        }

        /// <summary>
        /// Materializes the accumulated stream state into a response object.
        /// </summary>

        public OpenAiResponsesResponse CreateResponse()
            => new(responseId ?? string.Empty, new JsonObject
            {
                ["id"] = responseId ?? string.Empty,
                ["output"] = output.DeepClone(),
            });

        private void AppendArgumentsDelta(JsonNode data)
        {
            var outputIndex = GetOutputIndex(data);
            if (!pendingByOutputIndex.TryGetValue(outputIndex, out JsonObject? item))
            {
                return;
            }

            // Incrementally concatenate argument chunks on the pending item.
            var current = item["arguments"]?.GetValue<string>() ?? string.Empty;
            var delta = data["delta"]?.GetValue<string>() ?? string.Empty;
            item["arguments"] = current + delta;
        }

        private void CompleteArguments(JsonNode data)
        {
            var outputIndex = GetOutputIndex(data);
            if (!pendingByOutputIndex.TryGetValue(outputIndex, out JsonObject? item))
            {
                return;
            }

            // Replace partial accumulation with final argument payload when delta stream indicates completion.
            if (data["arguments"] is JsonValue arguments && arguments.TryGetValue<string>(out var value))
            {
                item["arguments"] = value;
            }
        }

        private JsonObject MergePendingArguments(JsonObject doneItem, int outputIndex)
        {
            if (!pendingByOutputIndex.TryGetValue(outputIndex, out JsonObject? pending))
            {
                return doneItem.DeepClone() as JsonObject ?? new JsonObject();
            }

            // Start from the pending shell and overlay the completed item fields, preferring explicit done values.
            JsonObject merged = pending.DeepClone() as JsonObject ?? new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> pair in doneItem)
            {
                merged[pair.Key] = pair.Value?.DeepClone();
            }

            var doneArguments = doneItem["arguments"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(doneArguments) && pending["arguments"] is JsonValue pendingArguments && pendingArguments.TryGetValue<string>(out var pendingValue) && !string.IsNullOrWhiteSpace(pendingValue))
            {
                merged["arguments"] = pendingValue;
            }

            return merged;
        }

        private static int GetOutputIndex(JsonNode data)
            => data["output_index"]?.GetValue<int>() ?? 0;
    }
}
