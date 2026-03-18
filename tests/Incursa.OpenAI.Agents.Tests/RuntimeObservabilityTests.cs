using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Tests;

public sealed class RuntimeObservabilityTests
{
    /// <summary>Verifies the runtime observer receives the lifecycle events emitted by a successful run.</summary>
    /// <intent>Protect the runtime observability contract that downstream sinks rely on.</intent>
    /// <scenario>LIB-EXEC-OBS-001</scenario>
    /// <behavior>The observer receives the run and turn lifecycle events in the emitted observation stream.</behavior>
    [Fact]
    public async Task RunAsync_EmitsLifecycleObservations()
    {
        var observer = new RecordingObserver();
        var runner = new AgentRunner(observer);
        var agent = new Agent<TestContext>
        {
            Name = "primary",
            Model = "gpt-5.4",
            Instructions = "answer",
        };
        var executor = new SequenceTurnExecutor<TestContext>(
            new AgentTurnResponse<TestContext>
            {
                FinalOutput = new AgentFinalOutput("done"),
                ResponseId = "resp-1",
            });

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-observed"), executor);

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Contains(observer.Observations, item => item.EventName == AgentRuntimeEventNames.RunStarted);
        Assert.Contains(observer.Observations, item => item.EventName == AgentRuntimeEventNames.TurnStarted);
        Assert.Contains(observer.Observations, item => item.EventName == AgentRuntimeEventNames.TurnCompleted);
        Assert.Contains(observer.Observations, item => item.EventName == AgentRuntimeEventNames.RunCompleted);
    }

    /// <summary>Verifies the composite MCP observer fans out each observation to all registered sinks.</summary>
    /// <intent>Protect the extensions MCP observability surface from partial sink delivery.</intent>
    /// <scenario>LIB-EXT-OBS-001</scenario>
    /// <behavior>Each sink receives the same observation exactly once.</behavior>
    [Fact]
    public async Task CompositeMcpClientObserver_FansOutObservationsToAllSinks()
    {
        var first = new RecordingMcpSink();
        var second = new RecordingMcpSink();
        var observer = new CompositeMcpClientObserver([first, second]);
        var observation = new McpClientObservation("local", "tools/list", "lookup", 1, TimeSpan.FromMilliseconds(12), McpCallOutcome.Success, null, null);

        await observer.ObserveAsync(observation);

        Assert.Single(first.Observations);
        Assert.Single(second.Observations);
        Assert.Equal(observation, first.Observations[0]);
        Assert.Equal(observation, second.Observations[0]);
    }

    private sealed class RecordingObserver : IAgentRuntimeObserver
    {
        public List<AgentRuntimeObservation> Observations { get; } = [];

        public ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingMcpSink : IMcpObservationSink
    {
        public List<McpClientObservation> Observations { get; } = [];

        public ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
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

    private sealed record TestContext
    {
        public TestContext()
            : this("user-1")
        {
        }

        public TestContext(string userId)
        {
            UserId = userId;
        }

        public string UserId { get; init; }
    }
}
