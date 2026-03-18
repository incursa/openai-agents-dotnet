namespace Incursa.OpenAI.Agents.Mcp;

internal sealed class StreamableMcpClientFactory
{
    private readonly HttpClient httpClient;
    private readonly IUserScopedMcpAuthResolver? authResolver;
    private readonly Func<McpAuthContext>? authContextFactory;
    private readonly IMcpToolMetadataResolver? metadataResolver;
    private readonly McpClientOptions? options;

    internal StreamableMcpClientFactory(
        HttpClient httpClient,
        IUserScopedMcpAuthResolver? authResolver = null,
        Func<McpAuthContext>? authContextFactory = null,
        IMcpToolMetadataResolver? metadataResolver = null,
        McpClientOptions? options = null)
    {
        this.httpClient = httpClient;
        this.authResolver = authResolver;
        this.authContextFactory = authContextFactory;
        this.metadataResolver = metadataResolver;
        this.options = options;
    }

    // Resolve auth lazily per request and construct a streamable MCP client for a configured server definition.
    internal IStreamableMcpClient Create(StreamableHttpMcpServerDefinition definition, McpAuthContext? authContext = null)
        => new StreamableHttpMcpClient(
            httpClient,
            definition.ServerLabel,
            definition.ServerUrl,
            definition.Headers,
            authResolver,
            authContext ?? authContextFactory?.Invoke() ?? new McpAuthContext(),
            metadataResolver,
            definition.ToolFilter,
            definition.CacheToolsList,
            options);
}
