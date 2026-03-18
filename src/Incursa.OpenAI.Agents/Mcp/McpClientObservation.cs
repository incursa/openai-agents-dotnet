using System.Net;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Describes one MCP client call and its outcome.
/// </summary>

public sealed record McpClientObservation
{
    /// <summary>
    /// Creates an observation describing one MCP client call.
    /// </summary>
    public McpClientObservation(
        string serverLabel,
        string method,
        string? toolName,
        int attempt,
        TimeSpan duration,
        McpCallOutcome outcome,
        HttpStatusCode? statusCode,
        string? detail)
    {
        ServerLabel = serverLabel;
        Method = method;
        ToolName = toolName;
        Attempt = attempt;
        Duration = duration;
        Outcome = outcome;
        StatusCode = statusCode;
        Detail = detail;
    }

    /// <summary>
    /// Gets or sets the MCP server label.
    /// </summary>

    public string ServerLabel { get; init; }

    /// <summary>
    /// Gets or sets the MCP method being called.
    /// </summary>

    public string Method { get; init; }

    /// <summary>
    /// Gets or sets the tool name, when the call targets a specific tool.
    /// </summary>

    public string? ToolName { get; init; }

    /// <summary>
    /// Gets or sets the attempt number for the call.
    /// </summary>

    public int Attempt { get; init; }

    /// <summary>
    /// Gets or sets the elapsed duration for the call.
    /// </summary>

    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or sets the final outcome of the MCP call.
    /// </summary>

    public McpCallOutcome Outcome { get; init; }

    /// <summary>
    /// Gets or sets the HTTP status code returned by the server, when available.
    /// </summary>

    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>
    /// Gets or sets additional diagnostic detail about the call.
    /// </summary>

    public string? Detail { get; init; }
}
