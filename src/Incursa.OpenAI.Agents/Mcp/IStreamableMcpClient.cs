using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for streamable MCP tool discovery and calls.
/// </summary>

public interface IStreamableMcpClient
{
    Uri ServerUrl { get; }

    string ServerLabel { get; }

    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken);

    Task<McpToolCallResult> CallToolAsync(string toolName, JsonNode? arguments, CancellationToken cancellationToken);
}
