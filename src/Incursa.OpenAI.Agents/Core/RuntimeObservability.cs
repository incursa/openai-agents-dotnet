namespace Incursa.OpenAI.Agents;

/// <summary>Canonical runtime event names used for agent execution observability.</summary>
public static class AgentRuntimeEventNames
{
    /// <summary>Raised when a run begins.</summary>
    public const string RunStarted = "run_started";

    /// <summary>Raised when a run completes.</summary>
    public const string RunCompleted = "run_completed";

    /// <summary>Raised when a run fails with an exception.</summary>
    public const string RunFailed = "run_failed";

    /// <summary>Raised when a turn begins.</summary>
    public const string TurnStarted = "turn_started";

    /// <summary>Raised when a turn completes.</summary>
    public const string TurnCompleted = "turn_completed";

    /// <summary>Raised when an agent handoff is applied.</summary>
    public const string HandoffApplied = "handoff_applied";

    /// <summary>Raised when tool output requires approval.</summary>
    public const string ApprovalRequired = "approval_required";

    /// <summary>Raised when approval resumes after a user response.</summary>
    public const string ApprovalResumed = "approval_resumed";

    /// <summary>Raised when approval is rejected.</summary>
    public const string ApprovalRejected = "approval_rejected";

    /// <summary>Raised when a guardrail tripwire is triggered.</summary>
    public const string GuardrailTriggered = "guardrail_triggered";

    /// <summary>Raised when the run exceeds the maximum turn limit.</summary>
    public const string MaxTurnsExceeded = "max_turns_exceeded";
}
