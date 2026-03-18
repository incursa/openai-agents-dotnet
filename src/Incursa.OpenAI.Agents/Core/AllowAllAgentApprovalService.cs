namespace Incursa.OpenAI.Agents;

/// <summary>
/// Default approval service that approves every request.
/// </summary>

public sealed class AllowAllAgentApprovalService : IAgentApprovalService
{
    /// <summary>Always returns an allow decision.</summary>
    public ValueTask<ApprovalDecision> EvaluateAsync<TContext>(AgentApprovalContext<TContext> context, CancellationToken cancellationToken)
        => ValueTask.FromResult(ApprovalDecision.Allow());
}
