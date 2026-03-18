namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Represents transport failures while calling an MCP server.
/// </summary>

public sealed class McpTransportException : McpException
{
    /// <summary>
    /// Creates a transport failure without an inner exception.
    /// </summary>

    public McpTransportException(string serverLabel, string method, string? toolName, string message)
        : this(serverLabel, method, toolName, message, null)
    {
    }

    /// <summary>
    /// Creates a transport failure with an inner exception.
    /// </summary>

    public McpTransportException(string serverLabel, string method, string? toolName, string message, Exception? innerException)
        : base(serverLabel, method, toolName, message, innerException)
    {
    }
}
