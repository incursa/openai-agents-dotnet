using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for enriching MCP tool calls with metadata.
/// </summary>

public interface IMcpToolMetadataResolver
{
    /// <summary>
    /// Resolves additional metadata to attach to an MCP tool call.
    /// </summary>
    ValueTask<JsonObject?> ResolveAsync(McpToolMetadataContext context, CancellationToken cancellationToken);
}
