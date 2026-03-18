using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the contract for an agent tool.
/// </summary>

public interface IAgentTool<TContext>
{
    /// <summary>Gets the tool name.</summary>
    string Name { get; }

    /// <summary>Gets optional tool description.</summary>
    string? Description { get; }

    /// <summary>Gets whether this tool requires approval before execution.</summary>
    bool RequiresApproval { get; }

    /// <summary>Gets JSON input schema for model/tool-calling.</summary>
    JsonNode? InputSchema { get; }

    /// <summary>Gets tool metadata for tracing and logging.</summary>
    IReadOnlyDictionary<string, object?> Metadata { get; }

    /// <summary>Gets guardrails that evaluate tool inputs.</summary>
    IReadOnlyList<IToolInputGuardrail<TContext>> InputGuardrails { get; }

    /// <summary>Gets guardrails that evaluate tool outputs.</summary>
    IReadOnlyList<IToolOutputGuardrail<TContext>> OutputGuardrails { get; }

    /// <summary>Evaluates whether the tool is enabled in current context.</summary>
    ValueTask<bool> IsEnabledAsync(AgentToolAvailabilityContext<TContext> context, CancellationToken cancellationToken);

    /// <summary>Executes the tool with provided invocation context.</summary>
    ValueTask<AgentToolResult> ExecuteAsync(AgentToolInvocation<TContext> invocation, CancellationToken cancellationToken);
}
