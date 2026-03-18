namespace Incursa.OpenAI.Agents;

/// <summary>Store interface that can also remove expired sessions.</summary>
public interface IVersionedAgentSessionStore : IAgentSessionStore
{
    /// <summary>Removes expired sessions.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken);
}
