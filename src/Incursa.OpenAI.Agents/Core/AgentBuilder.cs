using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Provides a fluent API for composing agents.
/// </summary>

public static class AgentBuilder
{
    /// <summary>Creates an agent builder for a specified agent name.</summary>
    public static AgentBuilder<TContext> Create<TContext>(string name) => new(name);
}

/// <summary>
/// Provides a fluent API for composing agents.
/// </summary>

public sealed class AgentBuilder<TContext>
{
    private readonly string name;
    private readonly List<IAgentTool<TContext>> tools = [];
    private readonly List<AgentHandoff<TContext>> handoffs = [];
    private readonly List<HostedMcpToolDefinition> hostedMcpTools = [];
    private readonly List<StreamableHttpMcpServerDefinition> streamableMcpServers = [];
    private readonly List<IInputGuardrail<TContext>> inputGuardrails = [];
    private readonly List<IOutputGuardrail<TContext>> outputGuardrails = [];
    private readonly Dictionary<string, object?> metadata = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> modelSettings = new(StringComparer.Ordinal);

    private string? model;
    private AgentInstructions<TContext>? instructions;
    private string? handoffDescription;
    private AgentOutputContract? outputContract;

    /// <summary>Creates a new builder with the given agent name.</summary>
    public AgentBuilder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An agent name is required.", nameof(name));
        }

        this.name = name;
    }

    /// <summary>Sets the model identifier on the agent.</summary>
    public AgentBuilder<TContext> WithModel(string model)
    {
        this.model = model;
        return this;
    }

    /// <summary>Sets instructions from plain text.</summary>
    public AgentBuilder<TContext> WithInstructions(string instructions)
        => WithInstructions(AgentInstructions<TContext>.FromText(instructions));

    /// <summary>Sets instructions from a pre-built instruction provider.</summary>
    public AgentBuilder<TContext> WithInstructions(AgentInstructions<TContext> instructions)
    {
        this.instructions = instructions;
        return this;
    }

    /// <summary>Sets the handoff description for this agent.</summary>
    public AgentBuilder<TContext> WithHandoffDescription(string handoffDescription)
    {
        this.handoffDescription = handoffDescription;
        return this;
    }

    /// <summary>Adds a pre-built tool to the agent.</summary>
    public AgentBuilder<TContext> WithTool(IAgentTool<TContext> tool)
    {
        tools.Add(tool);
        return this;
    }

    /// <summary>Adds a tool and infers default behavior.</summary>
    public AgentBuilder<TContext> AddTool(
        string name,
        Func<AgentToolInvocation<TContext>, CancellationToken, ValueTask<AgentToolResult>> executeAsync)
        => AddTool(name, executeAsync, null, null, false);

    /// <summary>Adds a tool with a description.</summary>
    public AgentBuilder<TContext> AddTool(
        string name,
        Func<AgentToolInvocation<TContext>, CancellationToken, ValueTask<AgentToolResult>> executeAsync,
        string? description)
        => AddTool(name, executeAsync, description, null, false);

    public AgentBuilder<TContext> AddTool(
        string name,
        Func<AgentToolInvocation<TContext>, CancellationToken, ValueTask<AgentToolResult>> executeAsync,
        string? description,
        bool requiresApproval)
        => AddTool(name, executeAsync, description, null, requiresApproval);

    /// <summary>Adds a tool with description and input schema.</summary>
    public AgentBuilder<TContext> AddTool(
        string name,
        Func<AgentToolInvocation<TContext>, CancellationToken, ValueTask<AgentToolResult>> executeAsync,
        string? description,
        JsonNode? inputSchema)
        => AddTool(name, executeAsync, description, inputSchema, false);

    /// <summary>Adds a fully configured tool.</summary>
    public AgentBuilder<TContext> AddTool(
        string name,
        Func<AgentToolInvocation<TContext>, CancellationToken, ValueTask<AgentToolResult>> executeAsync,
        string? description,
        JsonNode? inputSchema,
        bool requiresApproval)
    {
        tools.Add(new AgentTool<TContext>
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
            RequiresApproval = requiresApproval,
            ExecuteAsync = executeAsync,
        });
        return this;
    }

    /// <summary>Adds a handoff definition to this builder.</summary>
    public AgentBuilder<TContext> WithHandoff(AgentHandoff<TContext> handoff)
    {
        handoffs.Add(handoff);
        return this;
    }

    /// <summary>Adds a handoff by name and target agent.</summary>
    public AgentBuilder<TContext> AddHandoff(string name, Agent<TContext> targetAgent)
        => AddHandoff(name, targetAgent, null, null, null);

    /// <summary>Adds a handoff by name, target agent, and description.</summary>
    public AgentBuilder<TContext> AddHandoff(string name, Agent<TContext> targetAgent, string? description)
        => AddHandoff(name, targetAgent, description, null, null);

    /// <summary>Adds a handoff with optional tool name override.</summary>
    public AgentBuilder<TContext> AddHandoff(string name, Agent<TContext> targetAgent, string? description, string? toolNameOverride)
        => AddHandoff(name, targetAgent, description, toolNameOverride, null);

    /// <summary>Adds a handoff with full configuration.</summary>
    public AgentBuilder<TContext> AddHandoff(string name, Agent<TContext> targetAgent, string? description, string? toolNameOverride, JsonNode? inputSchema)
    {
        handoffs.Add(new AgentHandoff<TContext>
        {
            Name = name,
            TargetAgent = targetAgent,
            Description = description,
            ToolNameOverride = toolNameOverride,
            InputSchema = inputSchema,
        });
        return this;
    }

    /// <summary>Adds hosted MCP tools to the agent.</summary>
    public AgentBuilder<TContext> WithHostedMcpTool(HostedMcpToolDefinition tool)
    {
        hostedMcpTools.Add(tool);
        return this;
    }

    /// <summary>Adds a streamable MCP server definition to the agent.</summary>
    public AgentBuilder<TContext> WithStreamableMcpServer(StreamableHttpMcpServerDefinition server)
    {
        streamableMcpServers.Add(server);
        return this;
    }

    /// <summary>Adds metadata key-value pair for the agent.</summary>
    public AgentBuilder<TContext> WithMetadata(string key, object? value)
    {
        metadata[key] = value;
        return this;
    }

    /// <summary>Sets a model setting used in request payloads.</summary>
    public AgentBuilder<TContext> WithModelSetting(string key, object? value)
    {
        modelSettings[key] = value;
        return this;
    }

    /// <summary>Adds an input guardrail.</summary>
    public AgentBuilder<TContext> WithInputGuardrail(IInputGuardrail<TContext> guardrail)
    {
        inputGuardrails.Add(guardrail);
        return this;
    }

    /// <summary>Adds an output guardrail.</summary>
    public AgentBuilder<TContext> WithOutputGuardrail(IOutputGuardrail<TContext> guardrail)
    {
        outputGuardrails.Add(guardrail);
        return this;
    }

    /// <summary>Assigns an output contract for final tool output formatting.</summary>
    public AgentBuilder<TContext> WithOutputContract(AgentOutputContract outputContract)
    {
        this.outputContract = outputContract;
        return this;
    }

    /// <summary>Builds the configured <see cref=\"Agent{TContext}\"/> instance.</summary>
    public Agent<TContext> Build()
        => new()
        {
            Name = name,
            Model = model,
            Instructions = instructions,
            HandoffDescription = handoffDescription,
            Tools = tools,
            Handoffs = handoffs,
            HostedMcpTools = hostedMcpTools,
            StreamableMcpServers = streamableMcpServers,
            Metadata = metadata,
            ModelSettings = modelSettings,
            InputGuardrails = inputGuardrails,
            OutputGuardrails = outputGuardrails,
            OutputContract = outputContract,
        };
}
