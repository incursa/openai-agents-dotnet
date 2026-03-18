namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for resolving user-scoped MCP auth material.
/// </summary>

public interface IUserScopedMcpAuthResolver
{
    ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken);
}
