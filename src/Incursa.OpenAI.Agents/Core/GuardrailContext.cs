namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by guardrail.
/// </summary>

public sealed record GuardrailContext<TContext>
{
    /// <summary>Creates an input guardrail evaluation context.</summary>
    public GuardrailContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        string userInput,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        UserInput = userInput;
        Conversation = conversation;
    }

    /// <summary>Gets agent associated with guardrail evaluation.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets user-provided context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets user input text.</summary>
    public string UserInput { get; init; }

    /// <summary>Gets current conversation snapshot.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
