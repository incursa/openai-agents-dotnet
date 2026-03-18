using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for enriching MCP tool calls with metadata.
/// </summary>

public interface IMcpToolMetadataResolver
{
    ValueTask<JsonObject?> ResolveAsync(McpToolMetadataContext context, CancellationToken cancellationToken);
}
