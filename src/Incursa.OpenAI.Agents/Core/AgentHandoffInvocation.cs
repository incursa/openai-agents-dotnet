using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the data contract for AgentHandoffInvocation.
/// </summary>

public sealed record AgentHandoffInvocation<TContext>
{
    /// <summary>Creates an agent handoff invocation context.</summary>
    public AgentHandoffInvocation(
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

    /// <summary>Gets the source agent.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the target agent for this handoff.</summary>
    public Agent<TContext> TargetAgent { get; init; }

    /// <summary>Gets the context value.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets the session key for this handoff.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets serialized tool arguments if available.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets conversation items carried with the handoff.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}
