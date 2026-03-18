namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for resolving user-scoped MCP auth material.
/// </summary>

public interface IUserScopedMcpAuthResolver
{
    /// <summary>
    /// Resolves user-scoped authorization material for an MCP call.
    /// </summary>
    ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken);
}
