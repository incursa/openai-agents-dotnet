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
        : this(null, null, null, null, null)
    {
    }

    /// <summary>
    /// Creates a tool call result with text content only.
    /// </summary>

    public McpToolCallResult(string? text)
        : this(text, null, null, null, null)
    {
    }

    /// <summary>
    /// Creates a tool call result with text content and raw payload.
    /// </summary>

    public McpToolCallResult(string? text, JsonNode? raw)
        : this(text, raw, null, null, null)
    {
    }

    /// <summary>
    /// Creates a tool call result with text, raw payload, content, structured content, and metadata.
    /// </summary>

    public McpToolCallResult(
        string? text,
        JsonNode? raw,
        IReadOnlyList<JsonNode>? content,
        JsonNode? structuredContent,
        JsonNode? meta)
    {
        Text = text;
        Raw = raw;
        Content = content ?? [];
        StructuredContent = structuredContent;
        Meta = meta;
    }

    /// <summary>
    /// Gets or sets the text extracted from the MCP tool response.
    /// </summary>

    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets the raw JSON payload returned by the MCP server.
    /// </summary>

    public JsonNode? Raw { get; init; }

    /// <summary>
    /// Gets or sets the content array returned by the MCP server.
    /// </summary>

    public IReadOnlyList<JsonNode> Content { get; init; }

    /// <summary>
    /// Gets or sets the structured content returned by the MCP server.
    /// </summary>

    public JsonNode? StructuredContent { get; init; }

    /// <summary>
    /// Gets or sets MCP response metadata returned in the <c>_meta</c> field.
    /// </summary>

    public JsonNode? Meta { get; init; }
}
