namespace Incursa.OpenAI.Agents.Mcp;

internal sealed class HostedMcpToolFactory
{
    private readonly IUserScopedMcpAuthResolver? authResolver;
    private readonly Func<McpAuthContext>? authContextFactory;

    internal HostedMcpToolFactory(IUserScopedMcpAuthResolver? authResolver = null, Func<McpAuthContext>? authContextFactory = null)
    {
        this.authResolver = authResolver;
        this.authContextFactory = authContextFactory;
    }

    // Resolve and merge auth-derived headers/token into a hosted MCP definition before invocation.
    internal async ValueTask<HostedMcpToolDefinition> CreateAsync(
        HostedMcpToolDefinition definition,
        McpAuthContext? authContext = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> resolvedHeaders = new(definition.Headers ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var resolvedAuthorization = definition.Authorization;
        McpAuthContext context = authContext ?? authContextFactory?.Invoke() ?? new McpAuthContext();

        // If auth is available, defer to resolver output; local headers override remote-provided defaults.
        if (authResolver is not null)
        {
            McpAuthResult auth = await authResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(auth.BearerToken))
            {
                resolvedAuthorization ??= $"Bearer {auth.BearerToken}";
            }

            if (auth.Headers is not null)
            {
                foreach (KeyValuePair<string, string> pair in auth.Headers)
                {
                    resolvedHeaders[pair.Key] = pair.Value;
                }
            }
        }

        return definition with
        {
            Authorization = resolvedAuthorization,
            Headers = resolvedHeaders,
        };
    }
}
