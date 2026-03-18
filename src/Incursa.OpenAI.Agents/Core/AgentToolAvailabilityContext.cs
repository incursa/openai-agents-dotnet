namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by agent tool availability.
/// </summary>

public sealed record AgentToolAvailabilityContext<TContext>
{
    /// <summary>Creates a tool availability context.</summary>
    public AgentToolAvailabilityContext(
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

    /// <summary>Gets the current agent for availability checks.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the context associated with this run.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets the active session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the current conversation history.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
