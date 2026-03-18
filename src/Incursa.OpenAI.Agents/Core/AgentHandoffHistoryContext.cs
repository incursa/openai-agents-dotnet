using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by agent handoff history.
/// </summary>

public sealed record AgentHandoffHistoryContext<TContext>
{
    /// <summary>Creates a handoff history context.</summary>
    public AgentHandoffHistoryContext(
        Agent<TContext> agent,
        Agent<TContext> targetAgent,
        TContext context,
        string sessionKey,
        JsonNode? arguments,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        TargetAgent = targetAgent;
        Context = context;
        SessionKey = sessionKey;
        Arguments = arguments;
        Conversation = conversation;
    }

    /// <summary>Gets source agent.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets target agent.</summary>
    public Agent<TContext> TargetAgent { get; init; }

    /// <summary>Gets context for history transformation.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets current session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets handoff arguments.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets conversation history to transform.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
