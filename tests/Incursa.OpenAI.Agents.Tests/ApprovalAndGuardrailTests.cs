using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for approval and guardrail behavior during agent execution.</summary>
public sealed class ApprovalAndGuardrailTests
{
    /// <summary>Approval-required tool calls pause execution until the caller resumes the run.</summary>
    /// <intent>Protect the approval workflow for sensitive tool invocations.</intent>
    /// <scenario>LIB-EXEC-APPROVAL-001</scenario>
    /// <behavior>Approval-required tool calls return ApprovalRequired first and only execute after an approval response is supplied.</behavior>
    [Fact]
    public async Task RunAsync_ReturnsApprovalRequiredAndCanResume()
    {
        var toolExecuted = 0;
        AgentTool<TestContext> tool = new()
        {
            Name = "send_mail",
            RequiresApproval = true,
            Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tool_origin"] = new ToolOrigin(ToolOriginType.Mcp, "mail", null, null),
            },
            ExecuteAsync = (_, _) =>
            {
                toolExecuted++;
                return ValueTask.FromResult(AgentToolResult.FromText("mail sent"));
            },
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new(new RequireApprovalService<TestContext>());
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "send_mail", new JsonObject { ["subject"] = "hi" }, true),
                ],
                ResponseId = "resp-1",
            },
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("all set"),
                ResponseId = "resp-2",
            });

        AgentRunResult<TestContext> first = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "send it", new TestContext(), "session-approval"), executor);
        Assert.Equal(AgentRunStatus.ApprovalRequired, first.Status);
        Assert.NotNull(first.State);
        Assert.Equal(0, toolExecuted);
        Assert.Equal(ToolOriginType.Mcp, first.ApprovalRequest?.ToolOrigin?.Type);
        Assert.Equal("mail", first.ApprovalRequest?.ToolOrigin?.McpServerName);

        AgentRunResult<TestContext> resumed = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromState(
                first.State!,
                new TestContext(),
                [new AgentApprovalResponse("call-1", true)]),
            executor);

        Assert.Equal(AgentRunStatus.Completed, resumed.Status);
        Assert.Equal(1, toolExecuted);
        Assert.Contains(resumed.Items, item => item.ItemType == AgentItemTypes.ToolOutput && item.Text == "mail sent" && item.ToolOrigin?.Type == ToolOriginType.Mcp);
    }

    /// <summary>Rejected approvals use the configured tool error formatter.</summary>
    /// <intent>Protect the caller-facing rejection message contract.</intent>
    /// <scenario>LIB-EXEC-APPROVAL-002</scenario>
    /// <behavior>Rejected approval items use the supplied tool error formatter instead of invoking the tool.</behavior>
    [Fact]
    public async Task RunAsync_UsesToolErrorFormatterForRejectedApprovals()
    {
        AgentTool<TestContext> tool = new()
        {
            Name = "delete_mail",
            RequiresApproval = true,
            ExecuteAsync = (_, _) => throw new InvalidOperationException("should not execute"),
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new(new RequireApprovalService<TestContext>());
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "delete_mail", new JsonObject(), true),
                ],
                ResponseId = "resp-1",
            },
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("fallback"),
                ResponseId = "resp-2",
            });

        AgentRunResult<TestContext> first = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(
                agent,
                "delete it",
                new TestContext(),
                "session-reject",
                8,
                options: new AgentRunOptions<TestContext> { ToolErrorFormatter = ctx => $"blocked:{ctx.ToolName}" }),
            executor);

        AgentRunResult<TestContext> resumed = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromState(
                first.State!,
                new TestContext(),
                [new AgentApprovalResponse("call-1", false, "nope")],
                null,
                options: new AgentRunOptions<TestContext> { ToolErrorFormatter = ctx => $"blocked:{ctx.ToolName}" }),
            executor);

        Assert.Equal(AgentRunStatus.Completed, resumed.Status);
        Assert.Contains(resumed.Items, item => item.ItemType == AgentItemTypes.ApprovalRejected && item.Text == "blocked:delete_mail");
    }

    /// <summary>Tool guardrail tripwires stop execution before the tool runs.</summary>
    /// <intent>Protect enforcement of per-tool input guardrails.</intent>
    /// <scenario>LIB-EXEC-GUARDRAIL-001</scenario>
    /// <behavior>Tripwired tool input guardrails return GuardrailTriggered and surface the guardrail message on the result.</behavior>
    [Fact]
    public async Task RunAsync_StopsOnToolGuardrailTripwire()
    {
        AgentTool<TestContext> tool = new()
        {
            Name = "send_mail",
            InputGuardrails =
            [
                new DelegateToolInputGuardrail<TestContext>((_, _) => ValueTask.FromResult(GuardrailResult.Tripwire("blocked tool"))),
            ],
            ExecuteAsync = (_, _) => throw new InvalidOperationException("should not execute"),
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "send_mail", new JsonObject()),
                ],
                ResponseId = "resp-1",
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "send", new TestContext(), "session-guardrail"), executor);

        Assert.Equal(AgentRunStatus.GuardrailTriggered, result.Status);
        Assert.Equal("blocked tool", result.GuardrailMessage);
    }

    /// <summary>Run-level input guardrails execute before the first turn.</summary>
    /// <intent>Protect run-wide preflight validation semantics.</intent>
    /// <scenario>LIB-EXEC-GUARDRAIL-001-RUN</scenario>
    /// <behavior>Run-level input guardrails can stop execution before any turn executor output is accepted.</behavior>
    [Fact]
    public async Task RunAsync_UsesRunLevelInputGuardrailsOnFirstTurn()
    {
        Agent<TestContext> agent = CreateAgent(new AgentTool<TestContext>
        {
            Name = "noop",
            ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("ok")),
        });

        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext> { FinalOutput = new AgentFinalOutput("should not happen") });

        AgentRunResult<TestContext> result = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(
                agent,
                "block me",
                new TestContext(),
                "session-run-guardrail",
                8,
                options: new AgentRunOptions<TestContext>
                {
                    InputGuardrails =
                    [
                        new DelegateInputGuardrail<TestContext>((_, _) => ValueTask.FromResult(GuardrailResult.Tripwire("run blocked"))),
                    ],
                }),
            executor);

        Assert.Equal(AgentRunStatus.GuardrailTriggered, result.Status);
        Assert.Equal("run blocked", result.GuardrailMessage);
    }

    /// <summary>Persisted approval state can be resumed from a stored session snapshot.</summary>
    /// <intent>Protect durable approval workflows that span process boundaries.</intent>
    /// <scenario>LIB-EXEC-APPROVAL-001-PERSISTED</scenario>
    /// <behavior>Stored sessions expose pending approval state that can be resumed and completed later.</behavior>
    [Fact]
    public async Task RunAsync_CanResumeApprovalFromPersistedSession()
    {
        var directory = CreateTempDirectory();
        try
        {
            var toolExecuted = 0;
            AgentTool<TestContext> tool = new()
            {
                Name = "send_mail",
                RequiresApproval = true,
                Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["tool_origin"] = new ToolOrigin(ToolOriginType.Mcp, "mail", null, null),
                },
                ExecuteAsync = (_, _) =>
                {
                    toolExecuted++;
                    return ValueTask.FromResult(AgentToolResult.FromText("mail sent"));
                },
            };

            Agent<TestContext> agent = CreateAgent(tool);
            FileAgentSessionStore store = new(directory);
            AgentRunner runner = new(store, new RequireApprovalService<TestContext>());
            SequenceTurnExecutor<TestContext> executor = new(
                new AgentTurnResponse<TestContext>
                {
                    ToolCalls =
                    [
                        new AgentToolCall<TestContext>("call-1", "send_mail", new JsonObject { ["subject"] = "hi" }, true),
                    ],
                    ResponseId = "resp-1",
                },
                new AgentTurnResponse<TestContext>
                {
                    FinalOutput = new AgentFinalOutput("all set"),
                    ResponseId = "resp-2",
                });

            AgentRunResult<TestContext> first = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "send it", new TestContext(), "session-file-approval"), executor);
            AgentSession? persisted = await store.LoadAsync("session-file-approval");

            Assert.Equal(AgentRunStatus.ApprovalRequired, first.Status);
            Assert.NotNull(persisted);
            Assert.True(persisted!.RequiresApproval());
            Assert.Single(persisted.PendingApprovals);
            Assert.Equal(ToolOriginType.Mcp, persisted.PendingApprovals[0].ToolOrigin?.Type);
            Assert.Equal("mail", persisted.PendingApprovals[0].ToolOrigin?.McpServerName);

            AgentRunResult<TestContext> resumed = await runner.RunAsync(
                persisted.ResumeApproved(agent, new TestContext(), first.ApprovalRequest!.ToolCallId),
                executor);

            Assert.Equal(AgentRunStatus.Completed, resumed.Status);
            Assert.Equal(1, toolExecuted);
            Assert.Contains(resumed.Items, item => item.ItemType == AgentItemTypes.ToolOutput && item.ToolOrigin?.Type == ToolOriginType.Mcp);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Approval-required agent tools preserve agent-as-tool metadata in pending state.</summary>
    /// <intent>Protect approval state reconstruction for delegated tools that are exposed as agent-backed tools.</intent>
    /// <scenario>LIB-EXEC-APPROVAL-001</scenario>
    /// <behavior>Agent-as-tool approvals preserve their origin metadata and derived `toolType` through the pending approval state used for resume.</behavior>
    [Fact]
    public async Task RunAsync_PreservesAgentAsToolMetadataInPendingApprovalState()
    {
        var toolExecuted = 0;
        AgentTool<TestContext> tool = new()
        {
            Name = "delegate_lookup",
            RequiresApproval = true,
            Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["agent_name"] = "delegate-agent",
                ["agent_tool_name"] = "lookup_customer",
            },
            ExecuteAsync = (_, _) =>
            {
                toolExecuted++;
                return ValueTask.FromResult(AgentToolResult.FromText("delegated"));
            },
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new(new RequireApprovalService<TestContext>());
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "delegate_lookup", new JsonObject { ["customer_id"] = "42" }, true),
                ],
                ResponseId = "resp-1",
            },
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("all set"),
                ResponseId = "resp-2",
            });

        AgentRunResult<TestContext> first = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(agent, "delegate it", new TestContext(), "session-agent-approval"),
            executor);

        Assert.Equal(AgentRunStatus.ApprovalRequired, first.Status);
        Assert.NotNull(first.State);
        Assert.Equal(0, toolExecuted);
        Assert.Equal(ToolOriginType.AgentAsTool, first.ApprovalRequest?.ToolOrigin?.Type);
        Assert.Equal("delegate-agent", first.ApprovalRequest?.ToolOrigin?.AgentName);
        Assert.Equal("lookup_customer", first.ApprovalRequest?.ToolOrigin?.AgentToolName);
        Assert.Single(first.State!.PendingApprovals);
        Assert.Equal("agent_as_tool", first.State.PendingApprovals[0].ToolType);
        Assert.Equal(ToolOriginType.AgentAsTool, first.State.PendingApprovals[0].ToolOrigin?.Type);

        AgentRunResult<TestContext> resumed = await runner.RunAsync(
            AgentRunRequest<TestContext>.ResumeApproved(first.State, new TestContext(), "call-1"),
            executor);

        Assert.Equal(AgentRunStatus.Completed, resumed.Status);
        Assert.Equal(1, toolExecuted);
        Assert.Contains(resumed.Items, item => item.ItemType == AgentItemTypes.ToolOutput && item.ToolOrigin?.Type == ToolOriginType.AgentAsTool);
    }

    /// <summary>Output guardrail tripwires preserve MCP tool origin inferred from metadata.</summary>
    /// <intent>Protect runner-side origin inference when an MCP-backed tool trips an output guardrail.</intent>
    /// <scenario>LIB-EXEC-GUARDRAIL-001</scenario>
    /// <behavior>MCP server metadata stamps guardrail tripwire items with MCP origin details even when the originating tool call carries no explicit origin.</behavior>
    [Fact]
    public async Task RunAsync_UsesMcpMetadataForOutputGuardrailTripwireItems()
    {
        AgentTool<TestContext> tool = new()
        {
            Name = "search_mail",
            Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mcp_server"] = "mail",
            },
            OutputGuardrails =
            [
                new DelegateToolOutputGuardrail<TestContext>((_, _) => ValueTask.FromResult(GuardrailResult.Tripwire("blocked output"))),
            ],
            ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("mail results")),
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "search_mail", new JsonObject { ["query"] = "invoice" }),
                ],
                ResponseId = "resp-1",
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(agent, "search", new TestContext(), "session-output-guardrail"),
            executor);

        Assert.Equal(AgentRunStatus.GuardrailTriggered, result.Status);
        Assert.Equal("blocked output", result.GuardrailMessage);
        Assert.Contains(
            result.Items,
            item => item.ItemType == AgentItemTypes.GuardrailTripwire
                && item.ToolOrigin?.Type == ToolOriginType.Mcp
                && item.ToolOrigin?.McpServerName == "mail");
    }

    /// <summary>Legacy tool-type overloads continue to infer non-function origins.</summary>
    /// <intent>Protect compatibility for callers that already construct approval and tool-call records with explicit tool types.</intent>
    /// <scenario>LIB-EXEC-APPROVAL-001</scenario>
    /// <behavior>Legacy constructors that only receive `toolType` still infer MCP and agent-as-tool origins instead of downgrading them to plain function tools.</behavior>
    [Fact]
    public void ToolMetadata_ConstructorsInferOriginFromLegacyToolTypeOverloads()
    {
        Agent<TestContext> agent = CreateAgent(new AgentTool<TestContext>
        {
            Name = "noop",
            ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("ok")),
        });

        AgentToolCall<TestContext> mcpToolCall = new("call-1", "search_mail", new JsonObject(), false, null, "mcp");
        AgentPendingApproval<TestContext> agentApproval = new(agent, "delegate", "call-2", new JsonObject(), null, "agent_as_tool");
        AgentSessionPendingApproval sessionApproval = new("search_mail", "call-3", new JsonObject(), null, "mcp");
        AgentToolErrorContext errorContext = new("approval_rejected", "agent_as_tool", "delegate", "call-4", "blocked");

        Assert.Equal(ToolOriginType.Mcp, mcpToolCall.ToolOrigin?.Type);
        Assert.Equal(ToolOriginType.AgentAsTool, agentApproval.ToolOrigin?.Type);
        Assert.Equal(ToolOriginType.Mcp, sessionApproval.ToolOrigin?.Type);
        Assert.Equal(ToolOriginType.AgentAsTool, errorContext.ToolOrigin?.Type);
    }

    private static Agent<TestContext> CreateAgent(IAgentTool<TestContext> tool)
        => new()
        {
            Name = "primary",
            Model = "gpt-5.4",
            Instructions = "answer",
            Tools = [tool],
        };

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "incursa-agents-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class SequenceTurnExecutor<TContext> : IAgentTurnExecutor<TContext>
    {
        private readonly Queue<AgentTurnResponse<TContext>> responses;

        public SequenceTurnExecutor(params AgentTurnResponse<TContext>[] responses)
        {
            this.responses = new Queue<AgentTurnResponse<TContext>>(responses);
        }

        public ValueTask<AgentTurnResponse<TContext>> ExecuteTurnAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
            => ValueTask.FromResult(responses.Dequeue());
    }

    private sealed class RequireApprovalService<TContext> : IAgentApprovalService
    {
        public ValueTask<ApprovalDecision> EvaluateAsync<T>(AgentApprovalContext<T> context, CancellationToken cancellationToken)
            => ValueTask.FromResult(ApprovalDecision.Require("review required"));
    }

    private sealed class DelegateToolInputGuardrail<TContext> : IToolInputGuardrail<TContext>
    {
        private readonly Func<ToolInputGuardrailContext<TContext>, CancellationToken, ValueTask<GuardrailResult>> handler;

        public DelegateToolInputGuardrail(Func<ToolInputGuardrailContext<TContext>, CancellationToken, ValueTask<GuardrailResult>> handler)
        {
            this.handler = handler;
        }

        public ValueTask<GuardrailResult> EvaluateAsync(ToolInputGuardrailContext<TContext> context, CancellationToken cancellationToken)
            => handler(context, cancellationToken);
    }

    private sealed class DelegateToolOutputGuardrail<TContext> : IToolOutputGuardrail<TContext>
    {
        private readonly Func<ToolOutputGuardrailContext<TContext>, CancellationToken, ValueTask<GuardrailResult>> handler;

        public DelegateToolOutputGuardrail(Func<ToolOutputGuardrailContext<TContext>, CancellationToken, ValueTask<GuardrailResult>> handler)
        {
            this.handler = handler;
        }

        public ValueTask<GuardrailResult> EvaluateAsync(ToolOutputGuardrailContext<TContext> context, CancellationToken cancellationToken)
            => handler(context, cancellationToken);
    }

    private sealed class DelegateInputGuardrail<TContext> : IInputGuardrail<TContext>
    {
        private readonly Func<GuardrailContext<TContext>, CancellationToken, ValueTask<GuardrailResult>> handler;

        public DelegateInputGuardrail(Func<GuardrailContext<TContext>, CancellationToken, ValueTask<GuardrailResult>> handler)
        {
            this.handler = handler;
        }

        public ValueTask<GuardrailResult> EvaluateAsync(GuardrailContext<TContext> context, CancellationToken cancellationToken)
            => handler(context, cancellationToken);
    }

    private sealed record TestContext
    {
        public TestContext()
            : this("user-1", "tenant-1")
        {
        }

        public TestContext(string userId, string tenantId)
        {
            UserId = userId;
            TenantId = tenantId;
        }

        public string UserId { get; init; }

        public string TenantId { get; init; }
    }
}
