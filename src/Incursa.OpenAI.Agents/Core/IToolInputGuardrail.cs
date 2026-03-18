namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for tool input guardrail evaluation.
/// </summary>

public interface IToolInputGuardrail<TContext>
{
    /// <summary>Evaluates tool input guardrails for the supplied context.</summary>
    ValueTask<GuardrailResult> EvaluateAsync(ToolInputGuardrailContext<TContext> context, CancellationToken cancellationToken);
}
