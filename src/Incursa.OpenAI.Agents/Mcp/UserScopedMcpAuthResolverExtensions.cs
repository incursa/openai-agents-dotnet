namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Adds a convenience overload for IUserScopedMcpAuthResolver.
/// </summary>

public static class UserScopedMcpAuthResolverExtensions
{
    /// <summary>
    /// Resolves MCP auth without requiring an explicit cancellation token.
    /// </summary>

    public static ValueTask<McpAuthResult> ResolveAsync(this IUserScopedMcpAuthResolver resolver, McpAuthContext context)
        => resolver.ResolveAsync(context, CancellationToken.None);
}
