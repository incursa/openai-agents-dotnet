using System.Net;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Thrown when an MCP server returns a failure or JSON-RPC error.
/// </summary>

public sealed class McpServerException : McpException
{
    /// <summary>
    /// Creates a server failure without an HTTP status or JSON-RPC code.
    /// </summary>

    public McpServerException(string serverLabel, string method, string? toolName, string message)
        : this(serverLabel, method, toolName, message, null, null, null)
    {
    }

    /// <summary>
    /// Creates a server failure with an HTTP status code.
    /// </summary>

    public McpServerException(string serverLabel, string method, string? toolName, string message, HttpStatusCode? statusCode)
        : this(serverLabel, method, toolName, message, statusCode, null, null)
    {
    }

    /// <summary>
    /// Creates a server failure with HTTP status and JSON-RPC error code.
    /// </summary>

    public McpServerException(string serverLabel, string method, string? toolName, string message, HttpStatusCode? statusCode, int? jsonRpcCode)
        : this(serverLabel, method, toolName, message, statusCode, jsonRpcCode, null)
    {
    }

    /// <summary>
    /// Creates a server failure with all available error details.
    /// </summary>

    public McpServerException(string serverLabel, string method, string? toolName, string message, HttpStatusCode? statusCode, int? jsonRpcCode, Exception? innerException)
        : base(serverLabel, method, toolName, message, innerException)
    {
        StatusCode = statusCode;
        JsonRpcCode = jsonRpcCode;
    }

    /// <summary>
    /// Gets or sets the HTTP status code returned by the MCP server.
    /// </summary>

    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets or sets the JSON-RPC error code returned by the MCP server.
    /// </summary>

    public int? JsonRpcCode { get; }
}
