using System.Text.Json.Nodes;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Azure;
using Azure.Storage.Blobs;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;
using Incursa.OpenAI.Agents.Storage.Azure;
using Incursa.OpenAI.Agents.Storage.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for dependency injection registration helpers.</summary>
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
        string directory = CreateTempDirectory();
        try
        {
            ServiceCollection services = new();
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
            FileAgentSessionStore fileSessionStore = provider.GetRequiredService<FileAgentSessionStore>();

            Assert.IsType<FileAgentSessionStore>(sessionStore);
            Assert.Same(sessionStore, fileSessionStore);
            Assert.Same(sessionStore, versionedStore);

            Agent<TestContext> agent = new()
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

    /// <summary>Verifies custom session stores can replace the default runtime store without being versioned.</summary>
    /// <intent>Protect the extension hook used by future non-file backends.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>A custom `IAgentSessionStore` is used by the runner and does not require cleanup support.</behavior>
    [Fact]
    public async Task AddAgentSessionStore_RegistersCustomStore()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddIncursaAgents();

        RecordingSessionStore sessionStore = new();
        services.AddAgentSessionStore(sessionStore);
        services.AddOpenAiResponses(options =>
        {
            options.EnableMcpLoggingObserver = false;
        });
        services.AddSingleton<IOpenAiResponsesClient, FakeResponsesClient>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        OpenAiResponsesRunner runner = provider.GetRequiredService<OpenAiResponsesRunner>();

        Assert.Same(sessionStore, provider.GetRequiredService<IAgentSessionStore>());
        Assert.Null(provider.GetService<IVersionedAgentSessionStore>());

        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "answer briefly",
        };

        AgentRunResult<TestContext> result = await runner.RunAsync(AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "custom-session"));

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Equal("done", result.FinalOutput?.Text);
        Assert.NotEmpty(sessionStore.Saves);
    }

    /// <summary>Verifies the Azure adapter registers the same runtime session store contracts as the file backend.</summary>
    /// <intent>Protect the Azure package DI surface and ensure it can replace the core store cleanly.</intent>
    /// <scenario>LIB-STORAGE-AZURE-001</scenario>
    /// <behavior>`AddAzureAgentSessions` resolves a concrete Azure store and the generic session interfaces to the same instance.</behavior>
    [Fact]
    public void AddAzureAgentSessions_RegistersAzureStore()
    {
        ServiceCollection services = new();
        BlobContainerClient containerClient = new(new Uri("https://example.com/container"), new AzureSasCredential("sig"));

        services.AddAzureAgentSessions(options =>
        {
            options.ContainerClient = containerClient;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        AzureAgentSessionStore store = provider.GetRequiredService<AzureAgentSessionStore>();

        Assert.Same(containerClient, provider.GetRequiredService<IOptions<AzureAgentSessionStoreOptions>>().Value.ContainerClient);
        Assert.Same(store, provider.GetRequiredService<IAgentSessionStore>());
        Assert.Same(store, provider.GetRequiredService<IVersionedAgentSessionStore>());
        Assert.Same(store, provider.GetRequiredService<AzureAgentSessionStore>());
    }

    /// <summary>Verifies the S3 adapter registers the same runtime session store contracts as the file backend.</summary>
    /// <intent>Protect the S3 package DI surface and ensure it can replace the core store cleanly.</intent>
    /// <scenario>LIB-STORAGE-S3-001</scenario>
    /// <behavior>`AddS3AgentSessions` resolves a concrete S3 store and the generic session interfaces to the same instance.</behavior>
    [Fact]
    public void AddS3AgentSessions_RegistersS3Store()
    {
        ServiceCollection services = new();
        AmazonS3Client client = new(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);

        services.AddS3AgentSessions(options =>
        {
            options.Client = client;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        S3AgentSessionStore store = provider.GetRequiredService<S3AgentSessionStore>();

        Assert.Same(client, provider.GetRequiredService<IAmazonS3>());
        Assert.Same(store, provider.GetRequiredService<IAgentSessionStore>());
        Assert.Same(store, provider.GetRequiredService<IVersionedAgentSessionStore>());
        Assert.Same(store, provider.GetRequiredService<S3AgentSessionStore>());
    }

    /// <summary>Verifies the configured API key is applied to the named OpenAI HttpClient.</summary>
    /// <intent>Protect direct credential configuration without requiring environment variables.</intent>
    /// <scenario>LIB-EXT-DI-003</scenario>
    /// <behavior>`OpenAiResponsesOptions.ApiKey` sets the bearer token on the configured OpenAI client.</behavior>
    [Fact]
    public void AddOpenAiResponses_AppliesConfiguredApiKeyToHttpClient()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddIncursaAgents();
        services.AddOpenAiResponses(options =>
        {
            options.ApiKey = "test-api-key";
            options.EnableMcpLoggingObserver = false;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory clientFactory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient client = clientFactory.CreateClient("openai");

        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("test-api-key", client.DefaultRequestHeaders.Authorization?.Parameter);
    }

    /// <summary>Verifies the configured API key is applied to the named OpenAI audio HttpClient.</summary>
    /// <intent>Protect direct credential configuration for the audio DI surface without requiring environment variables.</intent>
    /// <scenario>LIB-EXT-DI-AUDIO-001</scenario>
    /// <behavior>`OpenAiAudioOptions.ApiKey` sets the bearer token on the configured audio client.</behavior>
    [Fact]
    public void AddOpenAiAudio_AppliesConfiguredApiKeyToHttpClient()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddIncursaAgents();
        services.AddOpenAiAudio(options =>
        {
            options.ApiKey = "test-audio-api-key";
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory clientFactory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient client = clientFactory.CreateClient("openai-audio");

        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("test-audio-api-key", client.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.IsType<OpenAiAudioClient>(provider.GetRequiredService<IOpenAiAudioClient>());
    }

    /// <summary>Verifies composite extension observers fan out each runtime observation to all registered sinks.</summary>
    /// <intent>Protect the extensions observability surface from dropping observations when multiple sinks are registered.</intent>
    /// <scenario>LIB-EXT-OBS-001</scenario>
    /// <behavior>Each sink receives the same observation exactly once.</behavior>
    [Fact]
    public async Task CompositeAgentRuntimeObserver_FansOutObservationsToAllSinks()
    {
        RecordingRuntimeSink first = new();
        RecordingRuntimeSink second = new();
        CompositeAgentRuntimeObserver observer = new([first, second]);
        AgentRuntimeObservation observation = new(AgentRuntimeEventNames.RunCompleted, "session-1", "triage")
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
        string directory = Path.Combine(Path.GetTempPath(), "incursa-agents-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class RecordingSessionStore : IAgentSessionStore
    {
        private readonly Dictionary<string, AgentSession> sessions = new(StringComparer.Ordinal);

        public List<AgentSession> Saves { get; } = [];

        public ValueTask<AgentSession?> LoadAsync(string sessionKey, CancellationToken cancellationToken)
        {
            if (sessions.TryGetValue(sessionKey, out AgentSession? session))
            {
                return ValueTask.FromResult<AgentSession?>(session.Clone());
            }

            return ValueTask.FromResult<AgentSession?>(null);
        }

        public ValueTask SaveAsync(AgentSession session, CancellationToken cancellationToken)
        {
            AgentSession clone = session.Clone();
            sessions[session.SessionKey] = clone;
            Saves.Add(clone);
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
