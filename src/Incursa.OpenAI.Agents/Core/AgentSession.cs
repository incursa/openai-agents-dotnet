namespace Incursa.OpenAI.Agents;

/// <summary>Represents persisted state for a running conversation session.</summary>
public sealed class AgentSession
{
    /// <summary>Gets or sets the unique session key.</summary>
    public required string SessionKey { get; init; }

    /// <summary>Gets or sets the persisted conversation items.</summary>
    public List<AgentConversationItem> Conversation { get; set; } = [];

    /// <summary>Gets or sets the last active agent name.</summary>
    public string? CurrentAgentName { get; set; }

    /// <summary>Gets or sets the last model response identifier.</summary>
    public string? LastResponseId { get; set; }

    /// <summary>Gets or sets turn count already executed.</summary>
    public int TurnsExecuted { get; set; }

    /// <summary>Gets or sets pending approvals for resumed runs.</summary>
    public List<AgentSessionPendingApproval> PendingApprovals { get; set; } = [];

    /// <summary>Gets or sets session creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets session update time.</summary>
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets session expiration timestamp.</summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>Gets or sets number of trimmed items from compaction.</summary>
    public int TrimmedItemCount { get; set; }

    /// <summary>Gets or sets optimistic concurrency version.</summary>
    public long Version { get; set; }

    /// <summary>Creates a shallow copy of session state for immutable operations.</summary>
    /// <returns>A cloned session instance.</returns>
    public AgentSession Clone() => new()
    {
        SessionKey = SessionKey,
        Conversation = Conversation.ToList(),
        CurrentAgentName = CurrentAgentName,
        LastResponseId = LastResponseId,
        TurnsExecuted = TurnsExecuted,
        PendingApprovals = PendingApprovals.Select(item => item with { Arguments = item.Arguments?.DeepClone() }).ToList(),
        CreatedUtc = CreatedUtc,
        UpdatedUtc = UpdatedUtc,
        ExpiresUtc = ExpiresUtc,
        TrimmedItemCount = TrimmedItemCount,
        Version = Version,
    };
}
