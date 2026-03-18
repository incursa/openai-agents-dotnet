namespace Incursa.OpenAI.Agents;

/// <summary>Stores and retrieves session state for agent runs.</summary>
public interface IAgentSessionStore
{
    /// <summary>Loads a session by key.</summary>
    /// <param name="sessionKey">Session key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<AgentSession?> LoadAsync(string sessionKey, CancellationToken cancellationToken);

    /// <summary>Saves a session.</summary>
    /// <param name="session">Session to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SaveAsync(AgentSession session, CancellationToken cancellationToken);
}
