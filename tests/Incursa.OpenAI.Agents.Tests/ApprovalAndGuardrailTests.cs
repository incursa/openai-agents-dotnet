using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;

namespace Incursa.OpenAI.Agents.Tests;

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

        AgentRunResult<TestContext> resumed = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromState(
                first.State!,
                new TestContext(),
                [new AgentApprovalResponse("call-1", true)]),
            executor);

        Assert.Equal(AgentRunStatus.Completed, resumed.Status);
        Assert.Equal(1, toolExecuted);
        Assert.Contains(resumed.Items, item => item.ItemType == AgentItemTypes.ToolOutput && item.Text == "mail sent");
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

            AgentRunResult<TestContext> resumed = await runner.RunAsync(
                persisted.ResumeApproved(agent, new TestContext(), first.ApprovalRequest!.ToolCallId),
                executor);

            Assert.Equal(AgentRunStatus.Completed, resumed.Status);
            Assert.Equal(1, toolExecuted);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
