using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Describes the model, tools, handoffs, and guardrails that define an agent.
/// </summary>

public sealed class Agent<TContext>
{
    /// <summary>Gets or sets the unique agent name used in sessions and handoffs.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the model identifier used by this agent.</summary>
    public string? Model { get; init; }

    /// <summary>Gets or sets the instructions source for this agent.</summary>
    public AgentInstructions<TContext>? Instructions { get; init; }

    /// <summary>Gets or sets handoff description shown to downstream models.</summary>
    public string? HandoffDescription { get; init; }

    /// <summary>Gets or sets the static list of tools available on this agent.</summary>
    public IReadOnlyList<IAgentTool<TContext>> Tools { get; init; } = [];

    /// <summary>Gets or sets configured handoff targets.</summary>
    public IReadOnlyList<AgentHandoff<TContext>> Handoffs { get; init; } = [];

    /// <summary>Gets or sets hosted MCP tools that should be injected into this run.</summary>
    public IReadOnlyList<HostedMcpToolDefinition> HostedMcpTools { get; init; } = [];

    /// <summary>Gets or sets streamable MCP servers enabled for this agent.</summary>
    public IReadOnlyList<StreamableHttpMcpServerDefinition> StreamableMcpServers { get; init; } = [];

    /// <summary>Gets or sets custom metadata associated with this agent.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Gets or sets model settings included in request payload.</summary>
    public IReadOnlyDictionary<string, object?> ModelSettings { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Gets or sets input guardrails for user-facing model turns.</summary>
    public IReadOnlyList<IInputGuardrail<TContext>> InputGuardrails { get; init; } = [];

    /// <summary>Gets or sets output guardrails for final outputs.</summary>
    public IReadOnlyList<IOutputGuardrail<TContext>> OutputGuardrails { get; init; } = [];

    /// <summary>Gets or sets the optional contract controlling final output schema.</summary>
    public AgentOutputContract? OutputContract { get; init; }

    /// <summary>Clones the agent with an overridden tool collection.</summary>
    public Agent<TContext> CloneWith(IReadOnlyList<IAgentTool<TContext>>? tools)
        => CloneWith(tools, null, null);

    /// <summary>Clones the agent with overridden tools and hosted MCP tools.</summary>
    public Agent<TContext> CloneWith(
        IReadOnlyList<IAgentTool<TContext>>? tools,
        IReadOnlyList<HostedMcpToolDefinition>? hostedMcpTools)
        => CloneWith(tools, hostedMcpTools, null);

    /// <summary>Clones the agent with overridden tools, hosted MCP tools, and streamable servers.</summary>
    public Agent<TContext> CloneWith(
        IReadOnlyList<IAgentTool<TContext>>? tools,
        IReadOnlyList<HostedMcpToolDefinition>? hostedMcpTools,
        IReadOnlyList<StreamableHttpMcpServerDefinition>? streamableMcpServers)
        => new()
        {
            Name = Name,
            Model = Model,
            Instructions = Instructions,
            HandoffDescription = HandoffDescription,
            Tools = tools ?? Tools,
            Handoffs = Handoffs,
            HostedMcpTools = hostedMcpTools ?? HostedMcpTools,
            StreamableMcpServers = streamableMcpServers ?? StreamableMcpServers,
            Metadata = Metadata,
            ModelSettings = ModelSettings,
            InputGuardrails = InputGuardrails,
            OutputGuardrails = OutputGuardrails,
            OutputContract = OutputContract,
        };
}
