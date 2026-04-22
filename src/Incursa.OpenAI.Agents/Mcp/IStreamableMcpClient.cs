using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for streamable MCP tool discovery and calls.
/// </summary>

public interface IStreamableMcpClient
{
    /// <summary>
    /// Gets the MCP server URL.
    /// </summary>
    Uri ServerUrl { get; }

    /// <summary>
    /// Gets the label used to identify the MCP server.
    /// </summary>
    string ServerLabel { get; }

    /// <summary>
    /// Lists the tools exposed by the MCP server.
    /// </summary>
    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists resources exposed by the MCP server.
    /// </summary>
    Task<ListResourcesResult> ListResourcesAsync(string? cursor, CancellationToken cancellationToken);

    /// <summary>
    /// Lists resource templates exposed by the MCP server.
    /// </summary>
    Task<ListResourceTemplatesResult> ListResourceTemplatesAsync(string? cursor, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a resource from the MCP server.
    /// </summary>
    Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken);

    /// <summary>
    /// Calls a tool exposed by the MCP server.
    /// </summary>
    Task<McpToolCallResult> CallToolAsync(string toolName, JsonNode? arguments, CancellationToken cancellationToken);
}
