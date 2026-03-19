#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;
using OpenAI.Responses;

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
        OpenAiResponsesRequestMapper mapper = new(hostedFactory, authContextFactory is null ? null : _ => authContextFactory(request));

        // 3) Translate the turn request into the OpenAI responses wire format and execute once.
        OpenAiResponsesTurnPlan<TContext> plan = await mapper.CreateAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);
        OpenAiResponsesResponse response = await client.CreateResponseAsync(new OpenAiResponsesRequest(plan.Options), cancellationToken).ConfigureAwait(false);
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
        OpenAiResponsesRequestMapper mapper = new(hostedFactory, authContextFactory is null ? null : _ => authContextFactory(request));
        OpenAiResponsesTurnPlan<TContext> plan = await mapper.CreateAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);

        StreamingResponseAccumulator accumulator = new();

        // Stream in-order model deltas, emit raw events, and emit normalized run items when complete frames arrive.
        await foreach (OpenAiResponsesStreamEvent? streamEvent in client.StreamResponseAsync(new OpenAiResponsesRequest(plan.Options, true), cancellationToken).ConfigureAwait(false))
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
        StreamableMcpClientFactory factory = new(mcpHttpClient, authResolver, () => authContext, mcpToolMetadataResolver, mcpClientOptions);
        List<IAgentTool<TContext>> tools = request.Agent.Tools.ToList();

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
        private readonly JsonArray output = [];
        private readonly Dictionary<int, JsonObject> pendingByOutputIndex = [];
        private ResponseResult? completedResponse;
        private string? responseId;

        /// <summary>
        /// Consumes streaming model events and rebuilds the final response payload.
        /// </summary>

        public void Accept(OpenAiResponsesStreamEvent streamEvent)
        {
            StreamingResponseUpdate update = streamEvent.Update ?? OpenAiSdkSerialization.ReadModel<StreamingResponseUpdate>(streamEvent.Data);

            switch (update)
            {
                case StreamingResponseCreatedUpdate created when !string.IsNullOrWhiteSpace(created.Response?.Id):
                    responseId = created.Response.Id;
                    break;

                case StreamingResponseInProgressUpdate inProgress when !string.IsNullOrWhiteSpace(inProgress.Response?.Id):
                    responseId = inProgress.Response.Id;
                    break;

                case StreamingResponseOutputItemAddedUpdate added:
                    pendingByOutputIndex[added.OutputIndex] = OpenAiSdkSerialization.ToJsonObject(added.Item);
                    break;

                case StreamingResponseFunctionCallArgumentsDeltaUpdate delta:
                    AppendArgumentsDelta(delta.OutputIndex, delta.Delta?.ToString());
                    break;

                case StreamingResponseFunctionCallArgumentsDoneUpdate done:
                    CompleteArguments(done.OutputIndex, done.FunctionArguments?.ToString());
                    break;

                case StreamingResponseMcpCallArgumentsDeltaUpdate delta:
                    AppendArgumentsDelta(delta.OutputIndex, delta.Delta?.ToString());
                    break;

                case StreamingResponseMcpCallArgumentsDoneUpdate done:
                    CompleteArguments(done.OutputIndex, done.ToolArguments?.ToString());
                    break;

                case StreamingResponseOutputItemDoneUpdate done:
                    JsonObject merged = MergePendingArguments(OpenAiSdkSerialization.ToJsonObject(done.Item), done.OutputIndex);
                    output.Add(merged.DeepClone());
                    break;

                case StreamingResponseCompletedUpdate completed:
                    completedResponse = completed.Response;
                    responseId = completed.Response?.Id ?? responseId;
                    break;

                case StreamingResponseFailedUpdate failed:
                    responseId = failed.Response?.Id ?? responseId;
                    break;

                case StreamingResponseIncompleteUpdate incomplete:
                    responseId = incomplete.Response?.Id ?? responseId;
                    break;
            }
        }

        /// <summary>
        /// Emits a normalized run item when a streamed output item is complete.
        /// </summary>

        public bool TryCreateRunItem(OpenAiResponsesStreamEvent streamEvent, string agentName, out AgentRunItem? runItem)
        {
            runItem = null;
            StreamingResponseUpdate update = streamEvent.Update ?? OpenAiSdkSerialization.ReadModel<StreamingResponseUpdate>(streamEvent.Data);
            if (update is not StreamingResponseOutputItemDoneUpdate doneUpdate)
            {
                return false;
            }

            JsonObject merged = MergePendingArguments(OpenAiSdkSerialization.ToJsonObject(doneUpdate.Item), doneUpdate.OutputIndex);
            runItem = OpenAiResponsesResponseMapper.TryMapStreamingOutputItem(agentName, merged);
            return runItem is not null;
        }

        /// <summary>
        /// Materializes the accumulated stream state into a response object.
        /// </summary>

        public OpenAiResponsesResponse CreateResponse()
            => completedResponse is not null
                ? new OpenAiResponsesResponse(completedResponse)
                : new OpenAiResponsesResponse(responseId ?? string.Empty, new JsonObject
                {
                    ["id"] = responseId ?? string.Empty,
                    ["output"] = output.DeepClone(),
                });

        private void AppendArgumentsDelta(int outputIndex, string? delta)
        {
            if (!pendingByOutputIndex.TryGetValue(outputIndex, out JsonObject? item))
            {
                return;
            }

            var current = item["arguments"]?.GetValue<string>() ?? string.Empty;
            item["arguments"] = current + (delta ?? string.Empty);
        }

        private void CompleteArguments(int outputIndex, string? arguments)
        {
            if (!pendingByOutputIndex.TryGetValue(outputIndex, out JsonObject? item))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                item["arguments"] = arguments;
            }
        }

        private JsonObject MergePendingArguments(JsonObject doneItem, int outputIndex)
        {
            if (!pendingByOutputIndex.TryGetValue(outputIndex, out JsonObject? pending))
            {
                return doneItem.DeepClone() as JsonObject ?? new JsonObject();
            }

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
    }
}
