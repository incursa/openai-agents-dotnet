namespace Incursa.OpenAI.Agents;

/// <summary>Convenience extensions for session stores.</summary>
public static class AgentSessionStoreExtensions
{
    /// <summary>Loads a session without specifying cancellation.</summary>
    public static ValueTask<AgentSession?> LoadAsync(this IAgentSessionStore store, string sessionKey)
        => store.LoadAsync(sessionKey, CancellationToken.None);

    /// <summary>Saves a session without specifying cancellation.</summary>
    public static ValueTask SaveAsync(this IAgentSessionStore store, AgentSession session)
        => store.SaveAsync(session, CancellationToken.None);

    /// <summary>Removes expired sessions without specifying cancellation.</summary>
    public static ValueTask<int> CleanupExpiredSessionsAsync(this IVersionedAgentSessionStore store)
        => store.CleanupExpiredSessionsAsync(CancellationToken.None);
}
