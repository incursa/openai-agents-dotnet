namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by agent instruction.
/// </summary>

public sealed record AgentInstructionContext<TContext>
{
    /// <summary>Creates an instruction context for resolving dynamic agent instructions.</summary>
    public AgentInstructionContext(
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

    /// <summary>Gets the agent associated with the instruction.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the user context passed through the run request.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets the active session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the current conversation history used for instruction resolution.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
