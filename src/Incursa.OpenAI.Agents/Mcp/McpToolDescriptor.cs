using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Describes an MCP tool exposed by a server.
/// </summary>

public sealed record McpToolDescriptor
{
    /// <summary>
    /// Creates a descriptor with only the tool name.
    /// </summary>

    public McpToolDescriptor(string name)
        : this(name, null, null, false)
    {
    }

    /// <summary>
    /// Creates a descriptor with explicit metadata.
    /// </summary>

    public McpToolDescriptor(string name, string? description, JsonNode? inputSchema, bool requiresApproval)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        RequiresApproval = requiresApproval;
    }

    /// <summary>
    /// Gets or sets the tool name exposed by the MCP server.
    /// </summary>

    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the human-readable tool description.
    /// </summary>

    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the JSON schema expected by the tool.
    /// </summary>

    public JsonNode? InputSchema { get; init; }

    /// <summary>
    /// Gets or sets whether the tool requires approval before execution.
    /// </summary>

    public bool RequiresApproval { get; init; }
}
