namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for tool output guardrail evaluation.
/// </summary>

public interface IToolOutputGuardrail<TContext>
{
    /// <summary>Evaluates tool output guardrails for the supplied context.</summary>
    ValueTask<GuardrailResult> EvaluateAsync(ToolOutputGuardrailContext<TContext> context, CancellationToken cancellationToken);
}
