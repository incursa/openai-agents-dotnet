using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the data contract for AgentToolInvocation.
/// </summary>

public sealed record AgentToolInvocation<TContext>
{
    /// <summary>Creates a tool invocation record.</summary>
    public AgentToolInvocation(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        string toolCallId,
        string toolName,
        JsonNode? arguments,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        ToolCallId = toolCallId;
        ToolName = toolName;
        Arguments = arguments;
        Conversation = conversation;
    }

    /// <summary>Gets the agent that owns the invocation.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the request context associated with this tool call.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets the session key for this invocation.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the tool call identifier.</summary>
    public string ToolCallId { get; init; }

    /// <summary>Gets the invoked tool name.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets raw arguments for execution.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets the conversation context at call time.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
