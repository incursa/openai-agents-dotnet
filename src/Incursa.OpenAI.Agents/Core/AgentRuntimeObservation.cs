namespace Incursa.OpenAI.Agents;

/// <summary>Represents a single runtime observation emitted during execution.</summary>
public sealed record AgentRuntimeObservation
{
    /// <summary>Creates an observation with required metadata.</summary>
    public AgentRuntimeObservation(string eventName, string sessionKey, string agentName)
        : this(eventName, sessionKey, agentName, null, null, null, null, null, null, null, null, null, null)
    {
    }

    /// <summary>Creates a fully populated runtime observation.</summary>
    public AgentRuntimeObservation(
        string eventName,
        string sessionKey,
        string agentName,
        int? turnNumber,
        AgentRunStatus? status,
        string? toolName,
        string? toolCallId,
        string? fromAgentName,
        string? toAgentName,
        string? responseId,
        string? detail,
        TimeSpan? duration,
        Exception? exception)
    {
        EventName = eventName;
        SessionKey = sessionKey;
        AgentName = agentName;
        TurnNumber = turnNumber;
        Status = status;
        ToolName = toolName;
        ToolCallId = toolCallId;
        FromAgentName = fromAgentName;
        ToAgentName = toAgentName;
        ResponseId = responseId;
        Detail = detail;
        Duration = duration;
        Exception = exception;
    }

    /// <summary>Gets the event name.</summary>
    public string EventName { get; init; }

    /// <summary>Gets the session key associated with the observation.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the agent name associated with the observation.</summary>
    public string AgentName { get; init; }

    /// <summary>Gets the turn number, when provided.</summary>
    public int? TurnNumber { get; init; }

    /// <summary>Gets the run status, when provided.</summary>
    public AgentRunStatus? Status { get; init; }

    /// <summary>Gets the tool name for tool-related observations.</summary>
    public string? ToolName { get; init; }

    /// <summary>Gets the tool call id for tool-related observations.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets the source agent name during handoff transitions.</summary>
    public string? FromAgentName { get; init; }

    /// <summary>Gets the destination agent name during handoff transitions.</summary>
    public string? ToAgentName { get; init; }

    /// <summary>Gets the response id, when available.</summary>
    public string? ResponseId { get; init; }

    /// <summary>Gets optional detail message.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets elapsed duration for completed observations.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Gets exception details for failed observations.</summary>
    public Exception? Exception { get; init; }
}
