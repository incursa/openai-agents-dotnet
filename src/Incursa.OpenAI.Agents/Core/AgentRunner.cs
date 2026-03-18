using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Coordinates agent turns, approvals, guardrails, and session persistence.
/// </summary>

public sealed partial class AgentRunner
{
    private readonly IAgentSessionStore sessionStore;
    private readonly IAgentApprovalService approvalService;
    private readonly IAgentRuntimeObserver observer;

    /// <summary>Creates a runner with default session store, approval service, and observer.</summary>
    public AgentRunner()
        : this(null, null, null)
    {
    }

    /// <summary>Creates a runner using a specified session store.</summary>
    public AgentRunner(IAgentSessionStore? sessionStore)
        : this(sessionStore, null, null)
    {
    }

    /// <summary>Creates a runner using a specified approval service.</summary>
    public AgentRunner(IAgentApprovalService? approvalService)
        : this(null, approvalService, null)
    {
    }

    /// <summary>Creates a runner using a specified runtime observer.</summary>
    public AgentRunner(IAgentRuntimeObserver? observer)
        : this(null, null, observer)
    {
    }

    /// <summary>Creates a runner with session store and approval service.</summary>
    public AgentRunner(IAgentSessionStore? sessionStore, IAgentApprovalService? approvalService)
        : this(sessionStore, approvalService, null)
    {
    }

    /// <summary>Creates a runner with full dependency configuration.</summary>
    public AgentRunner(IAgentSessionStore? sessionStore, IAgentApprovalService? approvalService, IAgentRuntimeObserver? observer)
    {
        this.sessionStore = sessionStore ?? new InMemoryAgentSessionStore();
        this.approvalService = approvalService ?? new AllowAllAgentApprovalService();
        this.observer = observer ?? new NullAgentRuntimeObserver();
    }

    /// <summary>Runs a request using default cancellation token.</summary>
    public Task<AgentRunResult<TContext>> RunAsync<TContext>(
        AgentRunRequest<TContext> request,
        IAgentTurnExecutor<TContext> turnExecutor)
        => RunAsync(request, turnExecutor, CancellationToken.None);

    /// <summary>Runs a request with cancellation support.</summary>
    public Task<AgentRunResult<TContext>> RunAsync<TContext>(
        AgentRunRequest<TContext> request,
        IAgentTurnExecutor<TContext> turnExecutor,
        CancellationToken cancellationToken)
        => RunCoreAsync(request, turnExecutor, null, cancellationToken);

    /// <summary>Runs a request and returns a streaming event sequence.</summary>
    public IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync<TContext>(
        AgentRunRequest<TContext> request,
        IAgentTurnExecutor<TContext> turnExecutor)
        => RunStreamingAsync(request, turnExecutor, CancellationToken.None);

    /// <summary>Runs a request and yields streaming events.</summary>
    public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync<TContext>(
        AgentRunRequest<TContext> request,
        IAgentTurnExecutor<TContext> turnExecutor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<AgentStreamEvent>();
        var runTask = Task.Run(async () =>
        {
            Exception? failure = null;
            try
            {
                await RunCoreAsync(request, turnExecutor, evt => channel.Writer.WriteAsync(evt, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                channel.Writer.TryComplete(failure);
            }
        }, cancellationToken);

        await foreach (AgentStreamEvent? item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        await runTask.ConfigureAwait(false);
    }

    private async Task<AgentRunResult<TContext>> RunCoreAsync<TContext>(
        AgentRunRequest<TContext> request,
        IAgentTurnExecutor<TContext> turnExecutor,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runStart = Stopwatch.StartNew();

        var sessionKey = request.ResumeState?.SessionKey
            ?? request.SessionKey
            ?? Guid.NewGuid().ToString("n");
        AgentRunOptions<TContext> options = request.Options ?? new AgentRunOptions<TContext>();
        AgentSession session = await sessionStore.LoadAsync(sessionKey, cancellationToken).ConfigureAwait(false)
            ?? new AgentSession { SessionKey = sessionKey };

        if (request.ExpectedSessionVersion is long expectedVersion && session.Version != expectedVersion)
        {
            throw new AgentSessionVersionMismatchException(sessionKey, expectedVersion, session.Version);
        }

        Agent<TContext> currentAgent = request.ResumeState?.CurrentAgent ?? request.StartingAgent;
        List<AgentConversationItem> conversation = request.ResumeState?.Conversation.ToList() ?? session.Conversation.ToList();
        var items = new List<AgentRunItem>();
        var turns = request.ResumeState?.TurnsExecuted ?? 0;
        var previousResponseId = request.ResumeState?.LastResponseId ?? options.PreviousResponseId ?? session.LastResponseId;
        List<AgentPendingApproval<TContext>> pendingApprovals = request.ResumeState?.PendingApprovals.ToList()
            ?? session.PendingApprovals.Select(item => new AgentPendingApproval<TContext>(
                currentAgent,
                item.ToolName,
                item.ToolCallId,
                item.Arguments?.DeepClone(),
                item.Reason,
                item.ToolType)).ToList();

        await ObserveAsync(new AgentRuntimeObservation(
            AgentRuntimeEventNames.RunStarted,
            sessionKey,
            currentAgent.Name),
            cancellationToken).ConfigureAwait(false);

        try
        {
            if (request.ResumeState is null)
            {
                await SeedInputAsync(request, currentAgent, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
                UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, pendingApprovals);
                await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(request.UserInput))
                {
                    var guardrailResult = await RunInputGuardrailsAsync(currentAgent, request.Context, sessionKey, request.UserInput!, conversation, items, options, emitAsync, cancellationToken).ConfigureAwait(false);
                    if (guardrailResult is not null)
                    {
                        UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, pendingApprovals);
                        await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.GuardrailTriggered, sessionKey, currentAgent.Name)
                        {
                            Status = AgentRunStatus.GuardrailTriggered,
                            Detail = guardrailResult,
                        },
                            cancellationToken).ConfigureAwait(false);
                        AgentRunResult<TContext> result = BuildResult(AgentRunStatus.GuardrailTriggered, sessionKey, currentAgent, items, conversation, turns, previousResponseId, guardrailMessage: guardrailResult);
                        await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, sessionKey, currentAgent.Name)
                        {
                            Status = result.Status,
                            Duration = runStart.Elapsed,
                        },
                            cancellationToken).ConfigureAwait(false);
                        return result;
                    }
                }
            }

            if (pendingApprovals.Count > 0)
            {
                AgentRunResult<TContext>? resumed = await ResumePendingApprovalsAsync(
                    request,
                    currentAgent,
                    sessionKey,
                    conversation,
                    items,
                    pendingApprovals,
                    emitAsync,
                    cancellationToken).ConfigureAwait(false);

                if (resumed is not null)
                {
                    UpdateSession(session, conversation, resumed.FinalAgent.Name, resumed.ResponseId, resumed.TurnsExecuted, resumed.State?.PendingApprovals ?? Array.Empty<AgentPendingApproval<TContext>>());
                    await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                    await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, sessionKey, resumed.FinalAgent.Name)
                    {
                        Status = resumed.Status,
                        Duration = runStart.Elapsed,
                    },
                        cancellationToken).ConfigureAwait(false);
                    return resumed;
                }

                UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, Array.Empty<AgentPendingApproval<TContext>>());
                await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                pendingApprovals.Clear();
            }

            while (turns < request.MaxTurns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                turns++;
                var turnStart = Stopwatch.StartNew();
                await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.TurnStarted, sessionKey, currentAgent.Name)
                {
                    TurnNumber = turns,
                },
                    cancellationToken).ConfigureAwait(false);

                var turnRequest = new AgentTurnRequest<TContext>(
                    currentAgent,
                    request.Context,
                    sessionKey,
                    turns,
                    conversation.AsReadOnly(),
                    request.UserInput,
                    previousResponseId,
                    options);

                var usedStreamingExecutor = false;
                AgentTurnResponse<TContext> turnResponse;
                if (emitAsync is not null && turnExecutor is IStreamingAgentTurnExecutor<TContext> streamingTurnExecutor)
                {
                    usedStreamingExecutor = true;
                    turnResponse = await streamingTurnExecutor.ExecuteStreamingTurnAsync(turnRequest, emitAsync, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    turnResponse = await turnExecutor.ExecuteTurnAsync(turnRequest, cancellationToken).ConfigureAwait(false);
                }
                currentAgent = turnResponse.EffectiveAgent ?? currentAgent;
                previousResponseId = turnResponse.ResponseId ?? previousResponseId;

                if (turnResponse.Items is not null)
                {
                    foreach (AgentRunItem item in turnResponse.Items)
                    {
                        await AppendItemAsync(item, conversation, items, usedStreamingExecutor ? null : emitAsync, cancellationToken).ConfigureAwait(false);
                    }
                }

                await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.TurnCompleted, sessionKey, currentAgent.Name)
                {
                    TurnNumber = turns,
                    ResponseId = turnResponse.ResponseId,
                    Duration = turnStart.Elapsed,
                },
                    cancellationToken).ConfigureAwait(false);

                if (turnResponse.Handoffs is { Count: > 0 })
                {
                    currentAgent = await ProcessHandoffAsync(
                        currentAgent,
                        request.Context,
                        sessionKey,
                        conversation,
                        items,
                        turnResponse.Handoffs[0],
                        turns,
                        emitAsync,
                        cancellationToken).ConfigureAwait(false);

                    UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, Array.Empty<AgentPendingApproval<TContext>>());
                    await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (turnResponse.ToolCalls is { Count: > 0 })
                {
                    AgentRunResult<TContext>? toolResult = await ProcessToolCallsAsync(
                        request.Context,
                        currentAgent,
                        sessionKey,
                        conversation,
                        items,
                        turnResponse.ToolCalls,
                        options,
                        turns,
                        previousResponseId,
                        emitAsync,
                        cancellationToken).ConfigureAwait(false);

                    if (toolResult is not null)
                    {
                        UpdateSession(session, conversation, currentAgent.Name, previousResponseId, toolResult.TurnsExecuted, toolResult.State?.PendingApprovals ?? Array.Empty<AgentPendingApproval<TContext>>());
                        await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        await ObserveAsync(new AgentRuntimeObservation(toolResult.Status == AgentRunStatus.ApprovalRequired ? AgentRuntimeEventNames.ApprovalRequired : AgentRuntimeEventNames.GuardrailTriggered, sessionKey, currentAgent.Name)
                        {
                            TurnNumber = turns,
                            Status = toolResult.Status,
                            ToolName = toolResult.ApprovalRequest?.ToolName,
                            ToolCallId = toolResult.ApprovalRequest?.ToolCallId,
                            Detail = toolResult.GuardrailMessage ?? toolResult.ApprovalRequest?.Reason,
                        },
                            cancellationToken).ConfigureAwait(false);
                        await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, sessionKey, currentAgent.Name)
                        {
                            Status = toolResult.Status,
                            Duration = runStart.Elapsed,
                        },
                            cancellationToken).ConfigureAwait(false);
                        return toolResult;
                    }

                    UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, Array.Empty<AgentPendingApproval<TContext>>());
                    await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (turnResponse.FinalOutput is not null)
                {
                    var guardrailMessage = await RunOutputGuardrailsAsync(currentAgent, request.Context, sessionKey, turnResponse.FinalOutput, conversation, items, options, emitAsync, cancellationToken).ConfigureAwait(false);
                    if (guardrailMessage is not null)
                    {
                        UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, Array.Empty<AgentPendingApproval<TContext>>());
                        await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.GuardrailTriggered, sessionKey, currentAgent.Name)
                        {
                            TurnNumber = turns,
                            Status = AgentRunStatus.GuardrailTriggered,
                            Detail = guardrailMessage,
                        },
                            cancellationToken).ConfigureAwait(false);
                        AgentRunResult<TContext> guardrailResult = BuildResult(AgentRunStatus.GuardrailTriggered, sessionKey, currentAgent, items, conversation, turns, previousResponseId, turnResponse.FinalOutput, guardrailMessage: guardrailMessage);
                        await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, sessionKey, currentAgent.Name)
                        {
                            Status = guardrailResult.Status,
                            Duration = runStart.Elapsed,
                        },
                            cancellationToken).ConfigureAwait(false);
                        return guardrailResult;
                    }

                    await AppendItemAsync(
                        new AgentRunItem(AgentItemTypes.FinalOutput, "assistant", currentAgent.Name)
                        {
                            Text = turnResponse.FinalOutput.Text,
                            Data = turnResponse.FinalOutput.StructuredValue,
                            TimestampUtc = DateTimeOffset.UtcNow,
                        },
                        conversation,
                        items,
                        emitAsync,
                        cancellationToken).ConfigureAwait(false);

                    UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, Array.Empty<AgentPendingApproval<TContext>>());
                    await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                    AgentRunResult<TContext> completed = BuildResult(AgentRunStatus.Completed, sessionKey, currentAgent, items, conversation, turns, previousResponseId, turnResponse.FinalOutput);
                    await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, sessionKey, currentAgent.Name)
                    {
                        Status = completed.Status,
                        Duration = runStart.Elapsed,
                        ResponseId = previousResponseId,
                    },
                        cancellationToken).ConfigureAwait(false);
                    return completed;
                }

                throw new InvalidOperationException($"Turn {turns} for agent '{currentAgent.Name}' produced neither a final output nor actionable tool calls/handoffs.");
            }

            UpdateSession(session, conversation, currentAgent.Name, previousResponseId, turns, Array.Empty<AgentPendingApproval<TContext>>());
            await sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
            await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.MaxTurnsExceeded, sessionKey, currentAgent.Name)
            {
                TurnNumber = turns,
                Status = AgentRunStatus.MaxTurnsExceeded,
                Duration = runStart.Elapsed,
            },
                cancellationToken).ConfigureAwait(false);
            AgentRunResult<TContext> exceeded = BuildResult(AgentRunStatus.MaxTurnsExceeded, sessionKey, currentAgent, items, conversation, turns, previousResponseId);
            await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, sessionKey, currentAgent.Name)
            {
                Status = exceeded.Status,
                Duration = runStart.Elapsed,
            },
                cancellationToken).ConfigureAwait(false);
            return exceeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.RunFailed, sessionKey, currentAgent.Name)
            {
                Duration = runStart.Elapsed,
                Exception = ex,
                Detail = ex.Message,
            },
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
        => observer.ObserveAsync(observation, cancellationToken);

    private static void UpdateSession<TContext>(
        AgentSession session,
        IReadOnlyList<AgentConversationItem> conversation,
        string currentAgentName,
        string? responseId,
        int turns,
        IReadOnlyList<AgentPendingApproval<TContext>> pendingApprovals)
    {
        session.Conversation = conversation.ToList();
        session.CurrentAgentName = currentAgentName;
        session.LastResponseId = responseId;
        session.TurnsExecuted = turns;
        session.PendingApprovals = pendingApprovals
            .Select(item => new AgentSessionPendingApproval(item.ToolName, item.ToolCallId, item.Arguments?.DeepClone(), item.Reason, item.ToolType))
            .ToList();
        session.UpdatedUtc = DateTimeOffset.UtcNow;
    }

    private static AgentRunResult<TContext> BuildResult<TContext>(
        AgentRunStatus status,
        string sessionKey,
        Agent<TContext> agent,
        IReadOnlyList<AgentRunItem> items,
        IReadOnlyList<AgentConversationItem> conversation,
        int turns,
        string? responseId,
        AgentFinalOutput? finalOutput = null,
        AgentApprovalRequest? approvalRequest = null,
        string? guardrailMessage = null,
        AgentRunState<TContext>? state = null)
    {
        state ??= new AgentRunState<TContext>(
            sessionKey,
            agent,
            conversation,
            turns,
            responseId,
            approvalRequest is null
                ? []
                : [new AgentPendingApproval<TContext>(agent, approvalRequest.ToolName, approvalRequest.ToolCallId, approvalRequest.Arguments, approvalRequest.Reason)]);

        return new AgentRunResult<TContext>(
            status,
            sessionKey,
            agent,
            items,
            conversation,
            turns,
            finalOutput,
            approvalRequest,
            guardrailMessage,
            responseId,
            state);
    }
}
