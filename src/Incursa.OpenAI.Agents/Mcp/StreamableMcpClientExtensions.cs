using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

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
    /// Lists resources without requiring an explicit cancellation token.
    /// </summary>

    public static Task<ListResourcesResult> ListResourcesAsync(this IStreamableMcpClient client)
        => client.ListResourcesAsync(null, CancellationToken.None);

    /// <summary>
    /// Lists resources with an optional cursor without requiring an explicit cancellation token.
    /// </summary>

    public static Task<ListResourcesResult> ListResourcesAsync(this IStreamableMcpClient client, string? cursor)
        => client.ListResourcesAsync(cursor, CancellationToken.None);

    /// <summary>
    /// Lists resource templates without requiring an explicit cancellation token.
    /// </summary>

    public static Task<ListResourceTemplatesResult> ListResourceTemplatesAsync(this IStreamableMcpClient client)
        => client.ListResourceTemplatesAsync(null, CancellationToken.None);

    /// <summary>
    /// Lists resource templates with an optional cursor without requiring an explicit cancellation token.
    /// </summary>

    public static Task<ListResourceTemplatesResult> ListResourceTemplatesAsync(this IStreamableMcpClient client, string? cursor)
        => client.ListResourceTemplatesAsync(cursor, CancellationToken.None);

    /// <summary>
    /// Reads a resource without requiring an explicit cancellation token.
    /// </summary>

    public static Task<ReadResourceResult> ReadResourceAsync(this IStreamableMcpClient client, string uri)
        => client.ReadResourceAsync(uri, CancellationToken.None);

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
