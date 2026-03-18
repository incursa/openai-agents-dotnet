namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for approving or rejecting risky actions.
/// </summary>

public interface IAgentApprovalService
{
    /// <summary>Evaluates whether a pending tool call requires approval.</summary>
    ValueTask<ApprovalDecision> EvaluateAsync<TContext>(AgentApprovalContext<TContext> context, CancellationToken cancellationToken);
}
