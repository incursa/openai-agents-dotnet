using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for the agent runner execution loop.</summary>
public sealed class AgentRunnerTests
{
    /// <summary>Completed runs persist the final output back into the session conversation.</summary>
    /// <intent>Protect the persisted run transcript contract for resumable execution.</intent>
    /// <scenario>LIB-EXEC-RUNNER-001</scenario>
    /// <behavior>Completed runs return a final output and save the final-output item into the stored conversation.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    public async Task RunAsync_CompletesWithFinalOutputAndPersistsConversation()
    {
        InMemoryAgentSessionStore store = new();
        AgentRunner runner = new(store);
        Agent<TestContext> agent = CreateAgent();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("done"),
                ResponseId = "resp-1",
                Items =
                [
                    new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", "primary") { Text = "done" },
                ],
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-1"), executor);

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Equal("done", result.FinalOutput?.Text);
        Assert.Contains(result.Items, item => item.ItemType == AgentItemTypes.FinalOutput);

        AgentSession? loaded = await store.LoadAsync("session-1");
        Assert.NotNull(loaded);
        Assert.Contains(loaded!.Conversation, item => item.ItemType == AgentItemTypes.FinalOutput);
    }

    /// <summary>Tool calls execute and feed their outputs into the continuing run.</summary>
    /// <intent>Protect the basic tool-execution loop in the runner.</intent>
    /// <scenario>LIB-EXEC-TOOLS-001</scenario>
    /// <behavior>Tool calls invoke the configured tool and append tool output items before the next turn completes.</behavior>
    [Fact]
    public async Task RunAsync_ExecutesToolCallAndContinues()
    {
        var toolCalls = 0;
        AgentTool<TestContext> tool = new()
        {
            Name = "lookup_customer",
            Description = "Look up a customer",
            ExecuteAsync = (_, _) =>
            {
                toolCalls++;
                return ValueTask.FromResult(AgentToolResult.FromText("customer-42"));
            },
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "lookup_customer", new JsonObject { ["customer_id"] = "42" }),
                ],
                ResponseId = "resp-1",
            },
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("finished"),
                ResponseId = "resp-2",
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-2"), executor);

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Equal(1, toolCalls);
        Assert.Contains(result.Items, item => item.ItemType == AgentItemTypes.ToolOutput && item.Text == "customer-42");
    }

    /// <summary>Agent-backed tool metadata flows onto tool outputs and nested result items.</summary>
    /// <intent>Protect runner-side origin inference for tools that represent delegated agents.</intent>
    /// <scenario>LIB-EXEC-TOOLS-001</scenario>
    /// <behavior>Agent-as-tool metadata stamps the tool output and fills in missing origins on nested result items without overwriting explicit origins.</behavior>
    [Fact]
    public async Task RunAsync_InfersAgentAsToolOriginAndNormalizesNestedToolItems()
    {
        AgentTool<TestContext> tool = new()
        {
            Name = "delegate_lookup",
            Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["agent_name"] = "delegate-agent",
                ["agent_tool_name"] = "lookup_customer",
            },
            ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText(
                "delegated",
                [
                    new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", "delegate-agent") { Text = "nested item" },
                    new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", "delegate-agent")
                    {
                        Text = "pretagged",
                        ToolOrigin = new ToolOrigin(ToolOriginType.Function),
                    },
                ])),
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                ToolCalls =
                [
                    new AgentToolCall<TestContext>("call-1", "delegate_lookup", new JsonObject { ["customer_id"] = "42" }),
                ],
                ResponseId = "resp-1",
            },
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("finished"),
                ResponseId = "resp-2",
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(agent, "delegate this", new TestContext(), "session-agent-tool"),
            executor);

        AgentRunItem toolOutput = Assert.Single(result.Items, item => item.ItemType == AgentItemTypes.ToolOutput && item.ToolCallId == "call-1");
        Assert.Equal(ToolOriginType.AgentAsTool, toolOutput.ToolOrigin?.Type);
        Assert.Equal("delegate-agent", toolOutput.ToolOrigin?.AgentName);
        Assert.Equal("lookup_customer", toolOutput.ToolOrigin?.AgentToolName);

        AgentRunItem normalizedNestedItem = Assert.Single(result.Items, item => item.Text == "nested item");
        Assert.Equal(ToolOriginType.AgentAsTool, normalizedNestedItem.ToolOrigin?.Type);
        Assert.Equal("delegate-agent", normalizedNestedItem.ToolOrigin?.AgentName);
        Assert.Equal("lookup_customer", normalizedNestedItem.ToolOrigin?.AgentToolName);

        AgentRunItem preservedNestedItem = Assert.Single(result.Items, item => item.Text == "pretagged");
        Assert.Equal(ToolOriginType.Function, preservedNestedItem.ToolOrigin?.Type);
    }

    /// <summary>Handoffs switch execution to the delegated agent.</summary>
    /// <intent>Protect runtime routing between cooperating agents.</intent>
    /// <scenario>LIB-EXEC-HANDOFF-001</scenario>
    /// <behavior>When a handoff is requested the runner invokes the handoff and reports the delegated agent as the final agent.</behavior>
    [Fact]
    public async Task RunAsync_ExecutesHandoffAndSwitchesAgent()
    {
        var handoffInvoked = false;
        Agent<TestContext> specialist = new()
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "handle mail",
        };

        Agent<TestContext> triage = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "route",
            Handoffs =
            [
                new AgentHandoff<TestContext>
                {
                    Name = "mail",
                    TargetAgent = specialist,
                    OnHandoffAsync = (_, _) =>
                    {
                        handoffInvoked = true;
                        return ValueTask.CompletedTask;
                    },
                },
            ],
        };

        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                Handoffs =
                [
                    new AgentHandoffRequest<TestContext>("mail", specialist) { Reason = "route to mail" },
                ],
                ResponseId = "resp-1",
            },
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("handled by mail specialist"),
                ResponseId = "resp-2",
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(triage, "check inbox", new TestContext(), "session-3"), executor);

        Assert.True(handoffInvoked);
        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Equal("mail specialist", result.FinalAgent.Name);
        Assert.Contains(result.Items, item => item.ItemType == AgentItemTypes.HandoffOccurred);
    }

    /// <summary>Configured max turns stop the run before unbounded looping.</summary>
    /// <intent>Protect callers from runaway tool-call or model loops.</intent>
    /// <scenario>LIB-EXEC-MAXTURNS-001</scenario>
    /// <behavior>Runs return MaxTurnsExceeded once the configured turn budget is exhausted.</behavior>
    [Fact]
    public async Task RunAsync_EnforcesMaxTurns()
    {
        AgentTool<TestContext> tool = new()
        {
            Name = "loop",
            ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("again")),
        };

        Agent<TestContext> agent = CreateAgent(tool);
        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext> { ToolCalls = [new AgentToolCall<TestContext>("call-1", "loop")], ResponseId = "resp-1" },
            new AgentTurnResponse<TestContext> { ToolCalls = [new AgentToolCall<TestContext>("call-2", "loop")], ResponseId = "resp-2" });

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-4", 2), executor);

        Assert.Equal(AgentRunStatus.MaxTurnsExceeded, result.Status);
        Assert.Equal(2, result.TurnsExecuted);
    }

    /// <summary>Streaming runs emit run items as they are generated.</summary>
    /// <intent>Protect the streaming contract used by interactive callers.</intent>
    /// <scenario>LIB-EXEC-STREAM-001</scenario>
    /// <behavior>Streaming execution emits the initial user input, generated message items, and the final output item in-order.</behavior>
    [Fact]
    public async Task RunStreamingAsync_EmitsItemsAsTheyAreGenerated()
    {
        Agent<TestContext> agent = CreateAgent();
        AgentRunner runner = new();
        SequenceTurnExecutor<TestContext> executor = new(
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("done"),
                Items =
                [
                    new AgentRunItem(AgentItemTypes.MessageOutput, "assistant", "primary") { Text = "done" },
                ],
                ResponseId = "resp-1",
            });

        List<AgentStreamEvent> events = new();
        await foreach (AgentStreamEvent item in runner.RunStreamingAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-stream"), executor))
        {
            events.Add(item);
        }

        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.UserInput);
        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.MessageOutput);
        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.FinalOutput);
    }

    /// <summary>Streaming failures propagate to the enumerating caller.</summary>
    /// <intent>Protect interactive callers from streams that silently stop after a background failure.</intent>
    /// <scenario>LIB-EXEC-STREAM-001</scenario>
    /// <behavior>A streaming executor exception is surfaced by the async enumerable instead of hanging the caller.</behavior>
    [Fact]
    public async Task RunStreamingAsync_PropagatesExecutorFailure()
    {
        Agent<TestContext> agent = CreateAgent();
        AgentRunner runner = new();
        FailingStreamingTurnExecutor<TestContext> executor = new();
        List<AgentStreamEvent> events = [];

        async Task EnumerateAsync()
        {
            await foreach (AgentStreamEvent item in runner.RunStreamingAsync(
                AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-stream-failure"),
                executor))
            {
                events.Add(item);
            }
        }

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EnumerateAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal("stream failed", ex.Message);
        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.UserInput);
        Assert.Contains(events, item => item.Message == "partial event");
    }

    /// <summary>Streaming cancellation wakes the event reader.</summary>
    /// <intent>Protect interactive callers from cancellation paths that leave the stream reader blocked.</intent>
    /// <scenario>LIB-EXEC-STREAM-001</scenario>
    /// <behavior>When the caller cancels a streaming run, enumeration exits with cancellation instead of waiting indefinitely.</behavior>
    [Fact]
    public async Task RunStreamingAsync_CancellationStopsWaitingReader()
    {
        Agent<TestContext> agent = CreateAgent();
        AgentRunner runner = new();
        HangingStreamingTurnExecutor<TestContext> executor = new();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        async Task EnumerateAsync()
        {
            await foreach (AgentStreamEvent _ in runner.RunStreamingAsync(
                AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-stream-cancel"),
                executor,
                cancellation.Token))
            {
            }
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => EnumerateAsync().WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static Agent<TestContext> CreateAgent(IAgentTool<TestContext>? tool = null)
        => new()
        {
            Name = "primary",
            Model = "gpt-5.4",
            Instructions = "answer",
            Tools = tool is null ? [] : [tool],
        };

    private sealed class SequenceTurnExecutor<TContext> : IAgentTurnExecutor<TContext>
    {
        private readonly Queue<AgentTurnResponse<TContext>> responses;

        public SequenceTurnExecutor(params AgentTurnResponse<TContext>[] responses)
        {
            this.responses = new Queue<AgentTurnResponse<TContext>>(responses);
        }

        public ValueTask<AgentTurnResponse<TContext>> ExecuteTurnAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
        {
            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No more responses configured.");
            }

            return ValueTask.FromResult(responses.Dequeue());
        }
    }

    private sealed class FailingStreamingTurnExecutor<TContext> : IStreamingAgentTurnExecutor<TContext>
    {
        public ValueTask<AgentTurnResponse<TContext>> ExecuteTurnAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("stream failed");

        public async ValueTask<AgentTurnResponse<TContext>> ExecuteStreamingTurnAsync(
            AgentTurnRequest<TContext> request,
            Func<AgentStreamEvent, ValueTask> emitAsync,
            CancellationToken cancellationToken)
        {
            await emitAsync(new AgentStreamEvent(AgentStreamEventTypes.RawModelEvent, request.Agent.Name)
            {
                Message = "partial event",
                TimestampUtc = DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);
            throw new InvalidOperationException("stream failed");
        }
    }

    private sealed class HangingStreamingTurnExecutor<TContext> : IStreamingAgentTurnExecutor<TContext>
    {
        public ValueTask<AgentTurnResponse<TContext>> ExecuteTurnAsync(AgentTurnRequest<TContext> request, CancellationToken cancellationToken)
            => new(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(
                static _ => AgentTurnResponse<TContext>.Empty,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default));

        public async ValueTask<AgentTurnResponse<TContext>> ExecuteStreamingTurnAsync(
            AgentTurnRequest<TContext> request,
            Func<AgentStreamEvent, ValueTask> emitAsync,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return AgentTurnResponse<TContext>.Empty;
        }
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
