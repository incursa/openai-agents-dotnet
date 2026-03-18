namespace Incursa.OpenAI.Agents;

/// <summary>
/// Coordinates agent turns, approvals, guardrails, and session persistence.
/// </summary>

public sealed partial class AgentRunner
{
    private static async Task SeedInputAsync<TContext>(
        AgentRunRequest<TContext> request,
        Agent<TContext> currentAgent,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        if (request.InputItems is { Count: > 0 })
        {
            foreach (AgentConversationItem item in request.InputItems)
            {
                await AppendItemAsync(item.ToRunItem(), conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(request.UserInput))
        {
            return;
        }

        await AppendItemAsync(
            new AgentRunItem(AgentItemTypes.UserInput, "user", currentAgent.Name) { Text = request.UserInput, TimestampUtc = DateTimeOffset.UtcNow },
            conversation,
            items,
            emitAsync,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> RunInputGuardrailsAsync<TContext>(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        string userInput,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        AgentRunOptions<TContext> options,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        foreach (IInputGuardrail<TContext> guardrail in GetInputGuardrails(agent, options))
        {
            GuardrailResult result = await guardrail.EvaluateAsync(new GuardrailContext<TContext>(agent, context, sessionKey, userInput, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            if (result.TripwireTriggered)
            {
                var message = result.Reason ?? "Input guardrail triggered.";
                await AppendItemAsync(new AgentRunItem(AgentItemTypes.GuardrailTripwire, "system", agent.Name) { Text = message, TimestampUtc = DateTimeOffset.UtcNow }, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
                return message;
            }
        }

        return null;
    }

    private static async Task<string?> RunOutputGuardrailsAsync<TContext>(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        AgentFinalOutput output,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        AgentRunOptions<TContext> options,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        foreach (IOutputGuardrail<TContext> guardrail in GetOutputGuardrails(agent, options))
        {
            GuardrailResult result = await guardrail.EvaluateAsync(new OutputGuardrailContext<TContext>(agent, context, sessionKey, output, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            if (result.TripwireTriggered)
            {
                var message = result.Reason ?? "Output guardrail triggered.";
                await AppendItemAsync(new AgentRunItem(AgentItemTypes.GuardrailTripwire, "system", agent.Name) { Text = message, TimestampUtc = DateTimeOffset.UtcNow }, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
                return message;
            }
        }

        return null;
    }

    private async Task<Agent<TContext>> ProcessHandoffAsync<TContext>(
        Agent<TContext> currentAgent,
        TContext context,
        string sessionKey,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        AgentHandoffRequest<TContext> request,
        int turns,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        AgentHandoff<TContext> handoff = currentAgent.Handoffs.FirstOrDefault(item => string.Equals(item.Name, request.HandoffName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Handoff '{request.HandoffName}' was requested but not found on agent '{currentAgent.Name}'.");

        await AppendItemAsync(
            new AgentRunItem(AgentItemTypes.HandoffRequested, "assistant", currentAgent.Name, handoff.Name, request.Reason, null, request.Arguments, null, DateTimeOffset.UtcNow),
            conversation,
            items,
            emitAsync,
            cancellationToken).ConfigureAwait(false);

        if (handoff.OnHandoffAsync is not null)
        {
            await handoff.OnHandoffAsync(new AgentHandoffInvocation<TContext>(currentAgent, handoff.TargetAgent, context, sessionKey, request.Arguments, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
        }

        if (handoff.HistoryTransformerAsync is not null)
        {
            IReadOnlyList<AgentConversationItem> transformed = await handoff.HistoryTransformerAsync(new AgentHandoffHistoryContext<TContext>(currentAgent, handoff.TargetAgent, context, sessionKey, request.Arguments, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            conversation.Clear();
            conversation.AddRange(transformed);
        }

        await AppendItemAsync(
            new AgentRunItem(AgentItemTypes.HandoffOccurred, "system", handoff.TargetAgent.Name, handoff.Name, request.Reason, null, request.Arguments, null, DateTimeOffset.UtcNow),
            conversation,
            items,
            emitAsync,
            cancellationToken).ConfigureAwait(false);

        if (emitAsync is not null)
        {
            await emitAsync(new AgentStreamEvent(AgentStreamEventTypes.AgentUpdated, handoff.TargetAgent.Name) { Message = $"Switched to {handoff.TargetAgent.Name}.", TimestampUtc = DateTimeOffset.UtcNow }).ConfigureAwait(false);
        }

        await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.HandoffApplied, sessionKey, handoff.TargetAgent.Name)
        {
            TurnNumber = turns,
            FromAgentName = currentAgent.Name,
            ToAgentName = handoff.TargetAgent.Name,
            Detail = handoff.Name,
        },
            cancellationToken).ConfigureAwait(false);

        return handoff.TargetAgent;
    }

    private async Task<AgentRunResult<TContext>?> ProcessToolCallsAsync<TContext>(
        TContext context,
        Agent<TContext> agent,
        string sessionKey,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        IReadOnlyList<AgentToolCall<TContext>> toolCalls,
        AgentRunOptions<TContext> options,
        int turns,
        string? responseId,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        foreach (AgentToolCall<TContext> toolCall in toolCalls)
        {
            IAgentTool<TContext> tool = await FindToolAsync(agent, context, sessionKey, conversation, toolCall.ToolName, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Tool '{toolCall.ToolName}' was requested but not found on agent '{agent.Name}'.");

            await AppendItemAsync(
                new AgentRunItem(AgentItemTypes.ToolCall, "assistant", agent.Name) { Name = toolCall.ToolName, ToolCallId = toolCall.CallId, Data = toolCall.Arguments, TimestampUtc = DateTimeOffset.UtcNow },
                conversation,
                items,
                emitAsync,
                cancellationToken).ConfigureAwait(false);

            AgentPendingApproval<TContext> pendingApproval = new(agent, toolCall.ToolName, toolCall.CallId, toolCall.Arguments, toolCall.ApprovalReason, toolCall.ToolType);
            ApprovalDecision approvalDecision = tool.RequiresApproval || toolCall.RequiresApproval
                ? await approvalService.EvaluateAsync(new AgentApprovalContext<TContext>(agent, context, sessionKey, pendingApproval, tool, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false)
                : ApprovalDecision.Allow();

            if (approvalDecision.RequiresApproval)
            {
                await AppendItemAsync(
                    new AgentRunItem(AgentItemTypes.ApprovalRequired, "system", agent.Name, toolCall.ToolName, approvalDecision.Reason ?? toolCall.ApprovalReason, toolCall.CallId, toolCall.Arguments, null, DateTimeOffset.UtcNow),
                    conversation,
                    items,
                    emitAsync,
                    cancellationToken).ConfigureAwait(false);

                return BuildResult(
                    AgentRunStatus.ApprovalRequired,
                    sessionKey,
                    agent,
                    items,
                    conversation,
                    turns,
                    responseId,
                    approvalRequest: new AgentApprovalRequest(agent.Name, toolCall.ToolName, toolCall.CallId, approvalDecision.Reason ?? toolCall.ApprovalReason, toolCall.Arguments),
                    state: new AgentRunState<TContext>(
                        sessionKey,
                        agent,
                        conversation.AsReadOnly(),
                        turns,
                        responseId,
                        [pendingApproval]));
            }

            var inputGuardrail = await RunToolInputGuardrailsAsync(agent, context, sessionKey, toolCall, tool, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
            if (inputGuardrail is not null)
            {
                return BuildResult(AgentRunStatus.GuardrailTriggered, sessionKey, agent, items, conversation, turns, responseId, guardrailMessage: inputGuardrail);
            }

            AgentToolInvocation<TContext> invocation = new(agent, context, sessionKey, toolCall.CallId, toolCall.ToolName, toolCall.Arguments, conversation.AsReadOnly());
            AgentToolResult result = await tool.ExecuteAsync(invocation, cancellationToken).ConfigureAwait(false);

            var outputGuardrail = await RunToolOutputGuardrailsAsync(agent, context, sessionKey, toolCall, tool, result, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
            if (outputGuardrail is not null)
            {
                return BuildResult(AgentRunStatus.GuardrailTriggered, sessionKey, agent, items, conversation, turns, responseId, guardrailMessage: outputGuardrail);
            }

            await AppendItemAsync(
                new AgentRunItem(AgentItemTypes.ToolOutput, "tool", agent.Name, toolCall.ToolName, result.Text, toolCall.CallId, result.StructuredValue, null, DateTimeOffset.UtcNow),
                conversation,
                items,
                emitAsync,
                cancellationToken).ConfigureAwait(false);

            if (result.Items is not null)
            {
                foreach (AgentRunItem item in result.Items)
                {
                    await AppendItemAsync(item, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return null;
    }

    private async Task<AgentRunResult<TContext>?> ResumePendingApprovalsAsync<TContext>(
        AgentRunRequest<TContext> request,
        Agent<TContext> currentAgent,
        string sessionKey,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        List<AgentPendingApproval<TContext>> pendingApprovals,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        if (request.ApprovalResponses is null || request.ApprovalResponses.Count == 0)
        {
            AgentPendingApproval<TContext> pending = pendingApprovals[0];
            return BuildResult(
                AgentRunStatus.ApprovalRequired,
                sessionKey,
                currentAgent,
                items,
                conversation,
                request.ResumeState?.TurnsExecuted ?? 0,
                request.ResumeState?.LastResponseId,
                approvalRequest: new AgentApprovalRequest(currentAgent.Name, pending.ToolName, pending.ToolCallId, pending.Reason, pending.Arguments),
                state: new AgentRunState<TContext>(
                    sessionKey,
                    currentAgent,
                    conversation.AsReadOnly(),
                    request.ResumeState?.TurnsExecuted ?? 0,
                    request.ResumeState?.LastResponseId,
                    pendingApprovals.ToList()));
        }

        Dictionary<string, AgentApprovalResponse> responseMap = request.ApprovalResponses.ToDictionary(item => item.ToolCallId, StringComparer.Ordinal);
        foreach (AgentPendingApproval<TContext> pending in pendingApprovals)
        {
            if (!responseMap.TryGetValue(pending.ToolCallId, out AgentApprovalResponse? response))
            {
                return BuildResult(
                    AgentRunStatus.ApprovalRequired,
                    sessionKey,
                    currentAgent,
                    items,
                    conversation,
                    request.ResumeState?.TurnsExecuted ?? 0,
                    request.ResumeState?.LastResponseId,
                    approvalRequest: new AgentApprovalRequest(currentAgent.Name, pending.ToolName, pending.ToolCallId, pending.Reason, pending.Arguments),
                    state: new AgentRunState<TContext>(
                        sessionKey,
                        currentAgent,
                        conversation.AsReadOnly(),
                        request.ResumeState?.TurnsExecuted ?? 0,
                        request.ResumeState?.LastResponseId,
                        pendingApprovals.ToList()));
            }

            if (!response.Approved)
            {
                var rejection = FormatToolError(
                    request.Options ?? new AgentRunOptions<TContext>(),
                    new AgentToolErrorContext("approval_rejected", pending.ToolType, pending.ToolName, pending.ToolCallId, response.Reason ?? "Tool call rejected during approval."));
                await AppendItemAsync(
                    new AgentRunItem(AgentItemTypes.ApprovalRejected, "system", currentAgent.Name, pending.ToolName, rejection, pending.ToolCallId, pending.Arguments, null, DateTimeOffset.UtcNow),
                    conversation,
                    items,
                    emitAsync,
                    cancellationToken).ConfigureAwait(false);
                await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.ApprovalRejected, sessionKey, currentAgent.Name)
                {
                    TurnNumber = request.ResumeState?.TurnsExecuted ?? 0,
                    ToolName = pending.ToolName,
                    ToolCallId = pending.ToolCallId,
                    Detail = response.Reason ?? rejection,
                },
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            await ObserveAsync(new AgentRuntimeObservation(AgentRuntimeEventNames.ApprovalResumed, sessionKey, currentAgent.Name)
            {
                TurnNumber = request.ResumeState?.TurnsExecuted ?? 0,
                ToolName = pending.ToolName,
                ToolCallId = pending.ToolCallId,
                Detail = response.Reason,
            },
                cancellationToken).ConfigureAwait(false);

            IAgentTool<TContext> tool = await FindToolAsync(currentAgent, request.Context, sessionKey, conversation, pending.ToolName, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Approved tool '{pending.ToolName}' no longer exists on agent '{currentAgent.Name}'.");

            AgentToolCall<TContext> call = new(pending.ToolCallId, pending.ToolName, pending.Arguments) { ToolType = pending.ToolType };
            var inputGuardrail = await RunToolInputGuardrailsAsync(currentAgent, request.Context, sessionKey, call, tool, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
            if (inputGuardrail is not null)
            {
                return BuildResult(AgentRunStatus.GuardrailTriggered, sessionKey, currentAgent, items, conversation, request.ResumeState?.TurnsExecuted ?? 0, request.ResumeState?.LastResponseId, guardrailMessage: inputGuardrail);
            }

            AgentToolInvocation<TContext> invocation = new(currentAgent, request.Context, sessionKey, pending.ToolCallId, pending.ToolName, pending.Arguments, conversation.AsReadOnly());
            AgentToolResult result = await tool.ExecuteAsync(invocation, cancellationToken).ConfigureAwait(false);
            var outputGuardrail = await RunToolOutputGuardrailsAsync(currentAgent, request.Context, sessionKey, call, tool, result, conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
            if (outputGuardrail is not null)
            {
                return BuildResult(AgentRunStatus.GuardrailTriggered, sessionKey, currentAgent, items, conversation, request.ResumeState?.TurnsExecuted ?? 0, request.ResumeState?.LastResponseId, guardrailMessage: outputGuardrail);
            }

            await AppendItemAsync(
                new AgentRunItem(AgentItemTypes.ToolOutput, "tool", currentAgent.Name, pending.ToolName, result.Text, pending.ToolCallId, result.StructuredValue, null, DateTimeOffset.UtcNow),
                conversation,
                items,
                emitAsync,
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<string?> RunToolInputGuardrailsAsync<TContext>(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        AgentToolCall<TContext> toolCall,
        IAgentTool<TContext> tool,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        foreach (IToolInputGuardrail<TContext> guardrail in tool.InputGuardrails)
        {
            GuardrailResult result = await guardrail.EvaluateAsync(new ToolInputGuardrailContext<TContext>(agent, context, sessionKey, toolCall.ToolName, toolCall.CallId, toolCall.Arguments, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            if (result.TripwireTriggered)
            {
                var message = result.Reason ?? "Tool input guardrail triggered.";
                await AppendItemAsync(new AgentRunItem(AgentItemTypes.GuardrailTripwire, "system", agent.Name, toolCall.ToolName, message, toolCall.CallId, toolCall.Arguments, null, DateTimeOffset.UtcNow), conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
                return message;
            }
        }

        return null;
    }

    private static async Task<string?> RunToolOutputGuardrailsAsync<TContext>(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        AgentToolCall<TContext> toolCall,
        IAgentTool<TContext> tool,
        AgentToolResult result,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        foreach (IToolOutputGuardrail<TContext> guardrail in tool.OutputGuardrails)
        {
            GuardrailResult output = await guardrail.EvaluateAsync(new ToolOutputGuardrailContext<TContext>(agent, context, sessionKey, toolCall.ToolName, toolCall.CallId, toolCall.Arguments, result, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            if (output.TripwireTriggered)
            {
                var message = output.Reason ?? "Tool output guardrail triggered.";
                await AppendItemAsync(new AgentRunItem(AgentItemTypes.GuardrailTripwire, "system", agent.Name, toolCall.ToolName, message, toolCall.CallId, toolCall.Arguments, null, DateTimeOffset.UtcNow), conversation, items, emitAsync, cancellationToken).ConfigureAwait(false);
                return message;
            }
        }

        return null;
    }

    private static string FormatToolError<TContext>(AgentRunOptions<TContext> options, AgentToolErrorContext context)
        => options.ToolErrorFormatter?.Invoke(context) ?? context.DefaultMessage;

    private static IReadOnlyList<IInputGuardrail<TContext>> GetInputGuardrails<TContext>(Agent<TContext> agent, AgentRunOptions<TContext> options)
    {
        if (options.InputGuardrails is not { Count: > 0 })
        {
            return agent.InputGuardrails;
        }

        return [.. agent.InputGuardrails, .. options.InputGuardrails];
    }

    private static IReadOnlyList<IOutputGuardrail<TContext>> GetOutputGuardrails<TContext>(Agent<TContext> agent, AgentRunOptions<TContext> options)
    {
        if (options.OutputGuardrails is not { Count: > 0 })
        {
            return agent.OutputGuardrails;
        }

        return [.. agent.OutputGuardrails, .. options.OutputGuardrails];
    }

    private static async ValueTask AppendItemAsync(
        AgentRunItem item,
        List<AgentConversationItem> conversation,
        List<AgentRunItem> items,
        Func<AgentStreamEvent, ValueTask>? emitAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AgentRunItem normalized = item with { TimestampUtc = item.TimestampUtc ?? DateTimeOffset.UtcNow };
        items.Add(normalized);
        conversation.Add(normalized.ToConversationItem());

        if (emitAsync is not null)
        {
            await emitAsync(new AgentStreamEvent(AgentStreamEventTypes.RunItem, normalized.AgentName, normalized, null, null, normalized.TimestampUtc)).ConfigureAwait(false);
        }
    }

    private static async Task<IAgentTool<TContext>?> FindToolAsync<TContext>(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        List<AgentConversationItem> conversation,
        string toolName,
        CancellationToken cancellationToken)
    {
        foreach (IAgentTool<TContext> tool in agent.Tools)
        {
            if (!string.Equals(tool.Name, toolName, StringComparison.Ordinal))
            {
                continue;
            }

            var isEnabled = await tool.IsEnabledAsync(new AgentToolAvailabilityContext<TContext>(agent, context, sessionKey, conversation.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            if (isEnabled)
            {
                return tool;
            }
        }

        return null;
    }
}
