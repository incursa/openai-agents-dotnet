using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Adds convenience overloads for IStreamableMcpClient.
/// </summary>

public static class StreamableMcpClientExtensions
{
    /// <summary>
    /// Lists tools without requiring an explicit cancellation token.
    /// </summary>

    public static Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(this IStreamableMcpClient client)
        => client.ListToolsAsync(CancellationToken.None);

    /// <summary>
    /// Calls a tool without requiring an explicit cancellation token.
    /// </summary>

    public static Task<McpToolCallResult> CallToolAsync(this IStreamableMcpClient client, string toolName)
        => client.CallToolAsync(toolName, null, CancellationToken.None);

    /// <summary>
    /// Calls a tool with arguments without requiring an explicit cancellation token.
    /// </summary>

    public static Task<McpToolCallResult> CallToolAsync(this IStreamableMcpClient client, string toolName, JsonNode? arguments)
        => client.CallToolAsync(toolName, arguments, CancellationToken.None);
}
