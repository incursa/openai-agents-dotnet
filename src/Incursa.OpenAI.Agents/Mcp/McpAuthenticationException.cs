using System.Net;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Thrown when an MCP server rejects authentication.
/// </summary>

public sealed class McpAuthenticationException : McpException
{
    /// <summary>
    /// Creates an authentication failure without an inner exception or status code.
    /// </summary>

    public McpAuthenticationException(string serverLabel, string method, string? toolName, string message)
        : this(serverLabel, method, toolName, message, null, null)
    {
    }

    /// <summary>
    /// Creates an authentication failure with an HTTP status code.
    /// </summary>

    public McpAuthenticationException(string serverLabel, string method, string? toolName, string message, HttpStatusCode? statusCode)
        : this(serverLabel, method, toolName, message, statusCode, null)
    {
    }

    /// <summary>
    /// Creates an authentication failure with optional HTTP status and inner exception.
    /// </summary>

    public McpAuthenticationException(string serverLabel, string method, string? toolName, string message, HttpStatusCode? statusCode, Exception? innerException)
        : base(serverLabel, method, toolName, message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets or sets the HTTP status code returned by the MCP server.
    /// </summary>

    public HttpStatusCode? StatusCode { get; }
}
