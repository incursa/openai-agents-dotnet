namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Base exception for MCP transport and server failures.
/// </summary>

public abstract class McpException : Exception
{
    protected McpException(string serverLabel, string method, string? toolName, string message)
        : this(serverLabel, method, toolName, message, null)
    {
    }

    protected McpException(string serverLabel, string method, string? toolName, string message, Exception? innerException)
        : base(message, innerException)
    {
        ServerLabel = serverLabel;
        Method = method;
        ToolName = toolName;
    }

    /// <summary>
    /// Gets or sets the MCP server label associated with the failure.
    /// </summary>

    public string ServerLabel { get; }

    /// <summary>
    /// Gets or sets the MCP method that failed.
    /// </summary>

    public string Method { get; }

    /// <summary>
    /// Gets or sets the tool name associated with the failure, when available.
    /// </summary>

    public string? ToolName { get; }
}
