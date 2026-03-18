namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for executing a single agent turn.
/// </summary>

public interface IAgentTurnExecutor<TContext>
{
    /// <summary>Executes one agent turn.</summary>
    ValueTask<AgentTurnResponse<TContext>> ExecuteTurnAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken);
}
