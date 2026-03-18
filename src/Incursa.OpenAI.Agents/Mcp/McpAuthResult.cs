namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Represents resolved MCP authentication material.
/// </summary>

public sealed record McpAuthResult
{
    /// <summary>
    /// Creates an empty auth result when no bearer token or headers are returned.
    /// </summary>

    public McpAuthResult()
        : this(null, null)
    {
    }

    /// <summary>
    /// Creates a resolved auth result from a bearer token and optional headers.
    /// </summary>

    public McpAuthResult(string? bearerToken, IReadOnlyDictionary<string, string>? headers)
    {
        BearerToken = bearerToken;
        Headers = headers;
    }

    /// <summary>
    /// Gets or sets the bearer token to attach to outbound requests.
    /// </summary>

    public string? BearerToken { get; init; }

    /// <summary>
    /// Gets or sets additional headers to attach to outbound requests.
    /// </summary>

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets an empty auth result.
    /// </summary>

    public static McpAuthResult Empty { get; } = new();
}
