using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Represents the outcome of an MCP tool call.
/// </summary>

public sealed record McpToolCallResult
{
    /// <summary>
    /// Creates an empty tool call result.
    /// </summary>

    public McpToolCallResult()
        : this(null, null)
    {
    }

    /// <summary>
    /// Creates a tool call result with text content only.
    /// </summary>

    public McpToolCallResult(string? text)
        : this(text, null)
    {
    }

    /// <summary>
    /// Creates a tool call result with text content and raw payload.
    /// </summary>

    public McpToolCallResult(string? text, JsonNode? raw)
    {
        Text = text;
        Raw = raw;
    }

    /// <summary>
    /// Gets or sets the text extracted from the MCP tool response.
    /// </summary>

    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets the raw JSON payload returned by the MCP server.
    /// </summary>

    public JsonNode? Raw { get; init; }
}
