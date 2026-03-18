namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Carries the server and auth context used when filtering MCP tools.
/// </summary>

public sealed record McpToolFilterContext
{
    /// <summary>
    /// Creates a filter context from the server label and auth context.
    /// </summary>

    public McpToolFilterContext(string serverLabel, McpAuthContext authContext)
    {
        ServerLabel = serverLabel;
        AuthContext = authContext;
    }

    /// <summary>
    /// Gets or sets the MCP server label being filtered.
    /// </summary>

    public string ServerLabel { get; init; }

    /// <summary>
    /// Gets or sets the auth context used to evaluate the filter.
    /// </summary>

    public McpAuthContext AuthContext { get; init; }
}
