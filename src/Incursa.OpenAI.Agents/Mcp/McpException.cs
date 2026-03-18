namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Base exception for MCP transport and server failures.
/// </summary>

public abstract class McpException : Exception
{
    /// <summary>
    /// Initializes an MCP exception with server and tool context.
    /// </summary>
    protected McpException(string serverLabel, string method, string? toolName, string message)
        : this(serverLabel, method, toolName, message, null)
    {
    }

    /// <summary>
    /// Initializes an MCP exception with server and tool context and an inner exception.
    /// </summary>
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
