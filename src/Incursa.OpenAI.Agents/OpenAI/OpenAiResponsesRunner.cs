using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Runs agents against the OpenAI Responses API end to end.
/// </summary>

public sealed class OpenAiResponsesRunner
{
    private readonly IOpenAiResponsesClient client;
    private readonly IAgentSessionStore sessionStore;
    private readonly IAgentApprovalService approvalService;
    private readonly IAgentRuntimeObserver observer;
    private readonly IUserScopedMcpAuthResolver? authResolver;
    private readonly HttpClient mcpHttpClient;
    private readonly IMcpToolMetadataResolver? mcpToolMetadataResolver;
    private readonly McpClientOptions? mcpClientOptions;

    /// <summary>Creates a runner with defaults for session store, approval service, and observer.</summary>
    public OpenAiResponsesRunner(IOpenAiResponsesClient client)
        : this(client, null, null, null, null, null, null, null)
    {
    }

    /// <summary>Creates a runner with explicit infrastructure dependencies.</summary>
    public OpenAiResponsesRunner(
        IOpenAiResponsesClient client,
        IAgentSessionStore? sessionStore,
        IAgentApprovalService? approvalService,
        IUserScopedMcpAuthResolver? authResolver,
        HttpClient? mcpHttpClient,
        IMcpToolMetadataResolver? mcpToolMetadataResolver,
        McpClientOptions? mcpClientOptions,
        IAgentRuntimeObserver? observer)
    {
        this.client = client;
        this.sessionStore = sessionStore ?? new InMemoryAgentSessionStore();
        this.approvalService = approvalService ?? new AllowAllAgentApprovalService();
        this.observer = observer ?? new NullAgentRuntimeObserver();
        this.authResolver = authResolver;
        this.mcpHttpClient = mcpHttpClient ?? new HttpClient();
        this.mcpToolMetadataResolver = mcpToolMetadataResolver;
        this.mcpClientOptions = mcpClientOptions;
    }

    /// <summary>Executes a turned run with default settings for auth context and cancellation.</summary>
    public Task<AgentRunResult<TContext>> RunAsync<TContext>(AgentRunRequest<TContext> request)
        => RunAsync(request, null, CancellationToken.None);

    /// <summary>Executes a run with an optional MCP authorization-context factory.</summary>
    public Task<AgentRunResult<TContext>> RunAsync<TContext>(
        AgentRunRequest<TContext> request,
        Func<AgentTurnRequest<TContext>, McpAuthContext?>? authContextFactory)
        => RunAsync(request, authContextFactory, CancellationToken.None);

    /// <summary>Executes a run with an explicit cancellation token.</summary>
    public Task<AgentRunResult<TContext>> RunAsync<TContext>(
        AgentRunRequest<TContext> request,
        CancellationToken cancellationToken)
        => RunAsync(request, null, cancellationToken);

    /// <summary>Executes a run with explicit cancellation token and optional MCP authorization-context factory.</summary>
    public Task<AgentRunResult<TContext>> RunAsync<TContext>(
        AgentRunRequest<TContext> request,
        Func<AgentTurnRequest<TContext>, McpAuthContext?>? authContextFactory,
        CancellationToken cancellationToken)
    {
        // Composition root for standard (non-streaming) execution: runner + turn executor injection.
        var runner = new AgentRunner(sessionStore, approvalService, observer);
        var turnExecutor = new OpenAiResponsesTurnExecutor<TContext>(client, mcpHttpClient, authResolver, authContextFactory, mcpToolMetadataResolver, mcpClientOptions);
        return runner.RunAsync(request, turnExecutor, cancellationToken);
    }

    /// <summary>Executes a streamed run with default auth context and cancellation handling.</summary>
    public IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync<TContext>(AgentRunRequest<TContext> request)
        => RunStreamingAsync(request, null, CancellationToken.None);

    /// <summary>Executes a streamed run with a custom MCP authorization-context factory.</summary>
    public IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync<TContext>(
        AgentRunRequest<TContext> request,
        Func<AgentTurnRequest<TContext>, McpAuthContext?>? authContextFactory)
        => RunStreamingAsync(request, authContextFactory, CancellationToken.None);

    /// <summary>Executes a streamed run with an explicit cancellation token.</summary>
    public IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync<TContext>(
        AgentRunRequest<TContext> request,
        CancellationToken cancellationToken)
        => RunStreamingAsync(request, null, cancellationToken);

    /// <summary>Executes a streamed run with cancellation and optional auth-context factory.</summary>
    public IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync<TContext>(
        AgentRunRequest<TContext> request,
        Func<AgentTurnRequest<TContext>, McpAuthContext?>? authContextFactory,
        CancellationToken cancellationToken)
    {
        // Streaming variant keeps exactly the same execution chain, but wires a turn executor that emits AgentStreamEvent.
        var runner = new AgentRunner(sessionStore, approvalService, observer);
        var turnExecutor = new OpenAiResponsesTurnExecutor<TContext>(client, mcpHttpClient, authResolver, authContextFactory, mcpToolMetadataResolver, mcpClientOptions);
        return runner.RunStreamingAsync(request, turnExecutor, cancellationToken);
    }
}
