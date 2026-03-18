using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by tool output guardrail.
/// </summary>

public sealed record ToolOutputGuardrailContext<TContext>
{
    /// <summary>Creates a tool-output guardrail evaluation context.</summary>
    public ToolOutputGuardrailContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        string toolName,
        string toolCallId,
        JsonNode? arguments,
        AgentToolResult result,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        ToolName = toolName;
        ToolCallId = toolCallId;
        Arguments = arguments;
        Result = result;
        Conversation = conversation;
    }

    /// <summary>Gets agent associated with tool call.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets request context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets tool name.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets tool call identifier.</summary>
    public string ToolCallId { get; init; }

    /// <summary>Gets tool arguments.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets tool execution result.</summary>
    public AgentToolResult Result { get; init; }

    /// <summary>Gets conversation history for this evaluation.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
