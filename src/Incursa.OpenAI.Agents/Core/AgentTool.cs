using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents a configurable tool that an agent can invoke.
/// </summary>

public sealed class AgentTool<TContext> : IAgentTool<TContext>
{
    /// <summary>Gets or sets tool display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets optional tool description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets whether this tool requires approval.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>Gets or sets input schema consumed by the language model.</summary>
    public JsonNode? InputSchema { get; init; }

    /// <summary>Gets or sets static metadata for this tool.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Gets or sets input guardrails.</summary>
    public IReadOnlyList<IToolInputGuardrail<TContext>> InputGuardrails { get; init; } = [];

    /// <summary>Gets or sets output guardrails.</summary>
    public IReadOnlyList<IToolOutputGuardrail<TContext>> OutputGuardrails { get; init; } = [];

    /// <summary>Gets or sets optional async enabled evaluator.</summary>
    public Func<AgentToolAvailabilityContext<TContext>, CancellationToken, ValueTask<bool>>? IsEnabledAsync { get; init; }

    /// <summary>Gets or sets required execute function.</summary>
    public required Func<AgentToolInvocation<TContext>, CancellationToken, ValueTask<AgentToolResult>> ExecuteAsync { get; init; }

    /// <summary>Evaluates whether this tool is enabled.</summary>
    ValueTask<bool> IAgentTool<TContext>.IsEnabledAsync(AgentToolAvailabilityContext<TContext> context, CancellationToken cancellationToken)
        => IsEnabledAsync is null ? ValueTask.FromResult(true) : IsEnabledAsync(context, cancellationToken);

    /// <summary>Invokes the configured execution delegate.</summary>
    ValueTask<AgentToolResult> IAgentTool<TContext>.ExecuteAsync(AgentToolInvocation<TContext> invocation, CancellationToken cancellationToken)
        => ExecuteAsync(invocation, cancellationToken);
}
