using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Incursa.OpenAI.Agents;

/// <summary>Represents a pending tool approval stored in a persisted session.</summary>
public sealed record AgentSessionPendingApproval
{
    /// <summary>Creates a pending approval entry with minimal fields.</summary>
    [JsonConstructor]
    public AgentSessionPendingApproval(string toolName, string toolCallId)
        : this(toolName, toolCallId, null, null, "function", new ToolOrigin(ToolOriginType.Function))
    {
    }

    /// <summary>Creates a pending approval entry with arguments.</summary>
    public AgentSessionPendingApproval(string toolName, string toolCallId, JsonNode? arguments)
        : this(toolName, toolCallId, arguments, null, "function", new ToolOrigin(ToolOriginType.Function))
    {
    }

    /// <summary>Creates a pending approval entry with reason and arguments.</summary>
    public AgentSessionPendingApproval(string toolName, string toolCallId, JsonNode? arguments, string? reason)
        : this(toolName, toolCallId, arguments, reason, "function", new ToolOrigin(ToolOriginType.Function))
    {
    }

    /// <summary>Creates a pending approval entry with explicit tool type metadata.</summary>
    public AgentSessionPendingApproval(string toolName, string toolCallId, JsonNode? arguments, string? reason, string toolType)
        : this(toolName, toolCallId, arguments, reason, toolType, ToolOriginMetadata.FromToolType(toolType))
    {
    }

    /// <summary>Creates a pending approval entry with full metadata.</summary>
    public AgentSessionPendingApproval(string toolName, string toolCallId, JsonNode? arguments, string? reason, string toolType, ToolOrigin? toolOrigin = null)
    {
        ToolName = toolName;
        ToolCallId = toolCallId;
        Arguments = arguments;
        Reason = reason;
        ToolType = toolType;
        ToolOrigin = toolOrigin ?? ToolOriginMetadata.FromToolType(toolType);
    }

    /// <summary>Gets the tool name.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets the pending tool call id.</summary>
    public string ToolCallId { get; init; }

    /// <summary>Gets tool arguments.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets approval reason if provided.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the type of tool being approved.</summary>
    public string ToolType { get; init; }

    /// <summary>Gets the origin metadata for the approved tool.</summary>
    [JsonPropertyName("tool_origin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolOrigin? ToolOrigin { get; init; }
}
