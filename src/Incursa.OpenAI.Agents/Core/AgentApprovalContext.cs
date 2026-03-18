namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the context used by agent approval.
/// </summary>

public sealed record AgentApprovalContext<TContext>
{
    /// <summary>Creates an approval context for a pending tool call.</summary>
    public AgentApprovalContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        AgentPendingApproval<TContext> pendingApproval,
        IAgentTool<TContext> tool,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        PendingApproval = pendingApproval;
        Tool = tool;
        Conversation = conversation;
    }

    /// <summary>Gets the agent requesting execution.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets request context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets session key for this approval decision.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the pending approval associated with the tool.</summary>
    public AgentPendingApproval<TContext> PendingApproval { get; init; }

    /// <summary>Gets the underlying tool being approved.</summary>
    public IAgentTool<TContext> Tool { get; init; }

    /// <summary>Gets full conversation for this decision.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
