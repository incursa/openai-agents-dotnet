namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by agent handoff.
/// </summary>

public sealed record AgentHandoffContext<TContext>
{
    /// <summary>Creates a handoff context.</summary>
    public AgentHandoffContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        Conversation = conversation;
    }

    /// <summary>Gets the current agent.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the context value.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets the session key associated with this handoff.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the current conversation history.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
