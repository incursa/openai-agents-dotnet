using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Represents a JSON-RPC request sent to a streamable MCP server.
/// </summary>

public sealed record StreamableHttpMcpRequest
{
    /// <summary>
    /// Creates a request with no params or id.
    /// </summary>

    public StreamableHttpMcpRequest(string method)
        : this(method, null, null)
    {
    }

    /// <summary>
    /// Creates a request with params but no explicit id.
    /// </summary>

    public StreamableHttpMcpRequest(string method, JsonNode? @params)
        : this(method, @params, null)
    {
    }

    /// <summary>
    /// Creates a request with explicit params and id.
    /// </summary>

    public StreamableHttpMcpRequest(string method, JsonNode? @params, string? id)
    {
        Method = method;
        Params = @params;
        Id = id;
    }

    /// <summary>
    /// Gets or sets the JSON-RPC method name.
    /// </summary>

    public string Method { get; init; }

    /// <summary>
    /// Gets or sets the JSON-RPC params payload.
    /// </summary>

    public JsonNode? Params { get; init; }

    /// <summary>
    /// Gets or sets the JSON-RPC request identifier.
    /// </summary>

    public string? Id { get; init; }
}
