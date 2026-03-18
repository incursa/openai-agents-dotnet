using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Incursa.OpenAI.Agents.Tests;

public sealed class DependencyInjectionTests
{
    /// <summary>Verifies the extensions package wires the runnable default stack for DI consumers.</summary>
    /// <intent>Protect the public DI registration surface in Incursa.OpenAI.Agents.Extensions.</intent>
    /// <scenario>LIB-EXT-DI-001</scenario>
    /// <behavior>The container resolves runnable services and persists sessions through the file-backed store.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AddIncursaAgents_AndOpenAiResponses_ResolveRunnableServices()
    {
        var directory = CreateTempDirectory();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddIncursaAgents();
            services.AddFileAgentSessions(directory, options =>
            {
                options.MaxConversationItems = 20;
                options.SlidingExpiration = TimeSpan.FromMinutes(10);
            });
            services.AddOpenAiResponses(options =>
            {
                options.EnableMcpLoggingObserver = false;
            });
            services.AddSingleton<IOpenAiResponsesClient, FakeResponsesClient>();

            await using ServiceProvider provider = services.BuildServiceProvider();
            OpenAiResponsesRunner runner = provider.GetRequiredService<OpenAiResponsesRunner>();
            IAgentSessionStore sessionStore = provider.GetRequiredService<IAgentSessionStore>();
            IVersionedAgentSessionStore versionedStore = provider.GetRequiredService<IVersionedAgentSessionStore>();

            Assert.IsType<FileAgentSessionStore>(sessionStore);
            Assert.Same(sessionStore, versionedStore);

            var agent = new Agent<TestContext>
            {
                Name = "triage",
                Model = "gpt-5.4",
                Instructions = "answer briefly",
            };

            AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "di-session"));

            Assert.Equal(AgentRunStatus.Completed, result.Status);
            Assert.Equal("done", result.FinalOutput?.Text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies composite extension observers fan out each runtime observation to all registered sinks.</summary>
    /// <intent>Protect the extensions observability surface from dropping observations when multiple sinks are registered.</intent>
    /// <scenario>LIB-EXT-OBS-001</scenario>
    /// <behavior>Each sink receives the same observation exactly once.</behavior>
    [Fact]
    public async Task CompositeAgentRuntimeObserver_FansOutObservationsToAllSinks()
    {
        var first = new RecordingRuntimeSink();
        var second = new RecordingRuntimeSink();
        var observer = new CompositeAgentRuntimeObserver([first, second]);
        var observation = new AgentRuntimeObservation(AgentRuntimeEventNames.RunCompleted, "session-1", "triage")
        {
            Status = AgentRunStatus.Completed,
        };

        await observer.ObserveAsync(observation);

        Assert.Single(first.Observations);
        Assert.Single(second.Observations);
        Assert.Equal(observation, first.Observations[0]);
        Assert.Equal(observation, second.Observations[0]);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "incursa-agents-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeResponsesClient : IOpenAiResponsesClient
    {
        public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new OpenAiResponsesResponse("resp-1", new JsonObject
            {
                ["id"] = "resp-1",
                ["output"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "message",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "output_text",
                                ["text"] = "done",
                            },
                        },
                    },
                },
            }));

        public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingRuntimeSink : IAgentRuntimeObservationSink
    {
        public List<AgentRuntimeObservation> Observations { get; } = [];

        public ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
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
