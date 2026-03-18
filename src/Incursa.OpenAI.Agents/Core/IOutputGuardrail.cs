namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for output guardrail evaluation.
/// </summary>

public interface IOutputGuardrail<TContext>
{
    /// <summary>Evaluates output guardrails for the supplied context.</summary>
    ValueTask<GuardrailResult> EvaluateAsync(OutputGuardrailContext<TContext> context, CancellationToken cancellationToken);
}
