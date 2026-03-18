using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Carries the server, tool, auth, and argument context used to resolve tool metadata.
/// </summary>

public sealed record McpToolMetadataContext
{
    /// <summary>
    /// Creates a metadata context from the server, tool, auth, and argument values.
    /// </summary>

    public McpToolMetadataContext(string serverLabel, string toolName, McpAuthContext authContext, JsonNode? arguments)
    {
        ServerLabel = serverLabel;
        ToolName = toolName;
        AuthContext = authContext;
        Arguments = arguments;
    }

    /// <summary>
    /// Gets or sets the MCP server label for the current tool call.
    /// </summary>

    public string ServerLabel { get; init; }

    /// <summary>
    /// Gets or sets the tool name being resolved.
    /// </summary>

    public string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the auth context associated with the tool call.
    /// </summary>

    public McpAuthContext AuthContext { get; init; }

    /// <summary>
    /// Gets or sets the tool call arguments used during metadata resolution.
    /// </summary>

    public JsonNode? Arguments { get; init; }
}
