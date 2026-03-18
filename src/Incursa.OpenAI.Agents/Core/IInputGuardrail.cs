namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for input guardrail evaluation.
/// </summary>

public interface IInputGuardrail<TContext>
{
    /// <summary>Evaluates input guardrails for the supplied context.</summary>
    ValueTask<GuardrailResult> EvaluateAsync(GuardrailContext<TContext> context, CancellationToken cancellationToken);
}
