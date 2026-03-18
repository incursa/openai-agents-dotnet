namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by output guardrail.
/// </summary>

public sealed record OutputGuardrailContext<TContext>
{
    /// <summary>Creates an output guardrail evaluation context.</summary>
    public OutputGuardrailContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        AgentFinalOutput output,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        Output = output;
        Conversation = conversation;
    }

    /// <summary>Gets agent associated with guardrail evaluation.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets request context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets final output candidate.</summary>
    public AgentFinalOutput Output { get; init; }

    /// <summary>Gets conversation history for output check.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
