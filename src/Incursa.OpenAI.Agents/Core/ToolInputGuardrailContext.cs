using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by tool input guardrail.
/// </summary>

public sealed record ToolInputGuardrailContext<TContext>
{
    /// <summary>Creates a tool-input guardrail evaluation context.</summary>
    public ToolInputGuardrailContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        string toolName,
        string toolCallId,
        JsonNode? arguments,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        ToolName = toolName;
        ToolCallId = toolCallId;
        Arguments = arguments;
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

    /// <summary>Gets conversation history for this guardrail.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
