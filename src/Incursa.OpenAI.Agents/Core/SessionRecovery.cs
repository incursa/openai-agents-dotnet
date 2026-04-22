namespace Incursa.OpenAI.Agents;

/// <summary>Helper methods for resuming runs from persisted session state.</summary>
public static class AgentSessionRecovery
{
    /// <summary>Gets whether the session has any pending tool approvals.</summary>
    /// <param name="session">Session to inspect.</param>
    /// <returns><see langword="true"/> when there are pending approvals.</returns>
    public static bool RequiresApproval(this AgentSession session)
        => session.PendingApprovals.Count > 0;

    /// <summary>
    /// Maps a persisted session into a runnable state object for the current agent.
    /// </summary>
    /// <param name="session">The stored session.</param>
    /// <param name="currentAgent">Agent to resume execution with.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>An <see cref="AgentRunState{TContext}"/> that represents this session state.</returns>
    public static AgentRunState<TContext> ToRunState<TContext>(this AgentSession session, Agent<TContext> currentAgent)
        => new(
            session.SessionKey,
            currentAgent,
            session.Conversation,
            session.TurnsExecuted,
            session.LastResponseId,
            session.PendingApprovals.Select(item => new AgentPendingApproval<TContext>(
                currentAgent,
                item.ToolName,
                item.ToolCallId,
                item.Arguments?.DeepClone(),
                item.Reason,
                item.ToolType,
                item.ToolOrigin)).ToArray());

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was approved.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed approval.</returns>
    public static AgentRunRequest<TContext> ResumeApproved<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId)
        => ResumeApproved(session, currentAgent, context, toolCallId, null, null);

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was approved.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <param name="maxTurns">Optional override for maximum turns in the resumed run.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed approval.</returns>
    public static AgentRunRequest<TContext> ResumeApproved<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId,
        int? maxTurns)
        => ResumeApproved(session, currentAgent, context, toolCallId, maxTurns, null);

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was approved.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <param name="maxTurns">Optional override for maximum turns in the resumed run.</param>
    /// <param name="options">Optional run options.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed approval.</returns>
    public static AgentRunRequest<TContext> ResumeApproved<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId,
        int? maxTurns,
        AgentRunOptions<TContext>? options)
        => AgentRunRequest<TContext>
            .ResumeApproved(session.ToRunState(currentAgent), context, toolCallId, maxTurns, options)
            .WithExpectedSessionVersion(session.Version);

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was rejected.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed rejection.</returns>
    public static AgentRunRequest<TContext> ResumeRejected<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId)
        => ResumeRejected(session, currentAgent, context, toolCallId, null, null, null);

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was rejected.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <param name="reason">Optional rejection reason.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed rejection.</returns>
    public static AgentRunRequest<TContext> ResumeRejected<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId,
        string? reason)
        => ResumeRejected(session, currentAgent, context, toolCallId, reason, null, null);

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was rejected.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <param name="reason">Optional rejection reason.</param>
    /// <param name="maxTurns">Optional override for maximum turns in the resumed run.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed rejection.</returns>
    public static AgentRunRequest<TContext> ResumeRejected<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId,
        string? reason,
        int? maxTurns)
        => ResumeRejected(session, currentAgent, context, toolCallId, reason, maxTurns, null);

    /// <summary>
    /// Creates a resume request that indicates a previously blocked tool call was rejected.
    /// </summary>
    /// <param name="session">Session containing pending approval data.</param>
    /// <param name="currentAgent">Agent handling the resumed run.</param>
    /// <param name="context">Runtime context.</param>
    /// <param name="toolCallId">Tool call identifier to resume.</param>
    /// <param name="reason">Optional rejection reason.</param>
    /// <param name="maxTurns">Optional override for maximum turns in the resumed run.</param>
    /// <param name="options">Optional run options.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>A run request prepared for the resumed rejection.</returns>
    public static AgentRunRequest<TContext> ResumeRejected<TContext>(
        this AgentSession session,
        Agent<TContext> currentAgent,
        TContext context,
        string toolCallId,
        string? reason,
        int? maxTurns,
        AgentRunOptions<TContext>? options)
        => AgentRunRequest<TContext>
            .ResumeRejected(session.ToRunState(currentAgent), context, toolCallId, reason, maxTurns, options)
            .WithExpectedSessionVersion(session.Version);
}
