using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Describes a transfer from one agent to another.
/// </summary>

public sealed class AgentHandoff<TContext>
{
    /// <summary>Gets or sets the handoff tool name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the target agent.</summary>
    public required Agent<TContext> TargetAgent { get; init; }

    /// <summary>Gets or sets optional handoff description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets the tool-name override for generated function call.</summary>
    public string? ToolNameOverride { get; init; }

    /// <summary>Gets or sets optional input schema for handoff arguments.</summary>
    public JsonNode? InputSchema { get; init; }

    /// <summary>Gets or sets optional async handoff availability check.</summary>
    public Func<AgentHandoffContext<TContext>, CancellationToken, ValueTask<bool>>? IsEnabledAsync { get; init; }

    /// <summary>Gets or sets optional callback when handoff occurs.</summary>
    public Func<AgentHandoffInvocation<TContext>, CancellationToken, ValueTask>? OnHandoffAsync { get; init; }

    /// <summary>Gets or sets optional conversation history transform.</summary>
    public Func<AgentHandoffHistoryContext<TContext>, CancellationToken, ValueTask<IReadOnlyList<AgentConversationItem>>>? HistoryTransformerAsync { get; init; }
}
