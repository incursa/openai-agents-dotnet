using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Adds convenience overloads for IMcpToolMetadataResolver.
/// </summary>

public static class McpToolMetadataResolverExtensions
{
    /// <summary>
    /// Resolves MCP tool metadata without requiring an explicit cancellation token.
    /// </summary>

    public static ValueTask<JsonObject?> ResolveAsync(this IMcpToolMetadataResolver resolver, McpToolMetadataContext context)
        => resolver.ResolveAsync(context, CancellationToken.None);
}
