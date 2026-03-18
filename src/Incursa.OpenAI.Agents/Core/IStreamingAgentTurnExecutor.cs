namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for turn executors that can stream runtime events.
/// </summary>

public interface IStreamingAgentTurnExecutor<TContext> : IAgentTurnExecutor<TContext>
{
    /// <summary>Executes one agent turn with streaming event emission.</summary>
    ValueTask<AgentTurnResponse<TContext>> ExecuteStreamingTurnAsync(
        AgentTurnRequest<TContext> request,
        Func<AgentStreamEvent, ValueTask> emitAsync,
        CancellationToken cancellationToken);
}
