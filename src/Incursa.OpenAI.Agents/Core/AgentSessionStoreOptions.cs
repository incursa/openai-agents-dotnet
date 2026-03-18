namespace Incursa.OpenAI.Agents;

/// <summary>Options used to configure session storage and expiration behavior.</summary>
public sealed class AgentSessionStoreOptions
{
    /// <summary>Maximum number of conversation items to retain.</summary>
    public int? MaxConversationItems { get; init; }

    /// <summary>Maximum number of turns to retain.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>How to compact history when limits are reached.</summary>
    public Sessions CompactionMode { get; init; } = Sessions.KeepLatestWindow;

    /// <summary>Absolute lifetime duration before a session expires.</summary>
    public TimeSpan? AbsoluteLifetime { get; init; }

    /// <summary>Sliding expiration duration from last save operation.</summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>Whether expired sessions are removed during load.</summary>
    public bool CleanupExpiredOnLoad { get; init; } = true;

    /// <summary>Whether expired sessions are removed during save.</summary>
    public bool CleanupExpiredOnSave { get; init; } = true;
}
