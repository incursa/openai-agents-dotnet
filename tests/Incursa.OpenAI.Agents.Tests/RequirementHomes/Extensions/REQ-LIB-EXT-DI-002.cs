#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Dependency injection extension defaults preserve the public runtime wiring contract.</summary>
public sealed class REQ_LIB_EXT_DI_002
{
    /// <summary>Default file-backed sessions wire the versioned store and keep the retention defaults intact.</summary>
    /// <intent>Protect the default file-session overload and its retention baseline.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddFileAgentSessions(string)` registers the file store as both session interfaces and leaves the default retention options unchanged.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddFileAgentSessions_DefaultOverloadRegistersVersionedStoreAndRetentionDefaults()
    {
        string directory = CreateTempDirectory();
        try
        {
            ServiceCollection services = new();
            services.AddFileAgentSessions(directory);

            using ServiceProvider provider = services.BuildServiceProvider();
            FileAgentSessionStore fileStore = provider.GetRequiredService<FileAgentSessionStore>();
            AgentSessionRetentionOptions retentionOptions = provider.GetRequiredService<IOptions<AgentSessionRetentionOptions>>().Value;

            Assert.Same(fileStore, provider.GetRequiredService<IAgentSessionStore>());
            Assert.Same(fileStore, provider.GetRequiredService<IVersionedAgentSessionStore>());
            Assert.Null(retentionOptions.MaxConversationItems);
            Assert.Null(retentionOptions.MaxTurns);
            Assert.Equal(Sessions.KeepLatestWindow, retentionOptions.CompactionMode);
            Assert.Null(retentionOptions.AbsoluteLifetime);
            Assert.Null(retentionOptions.SlidingExpiration);
            Assert.True(retentionOptions.CleanupExpiredOnLoad);
            Assert.True(retentionOptions.CleanupExpiredOnSave);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Factory-based session-store replacement removes the versioned registration and uses the supplied store.</summary>
    /// <intent>Protect the non-versioned session-store override hook.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddAgentSessionStore(Func&lt;IServiceProvider, IAgentSessionStore&gt;)` resolves the supplied store and removes the versioned-store registration.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddAgentSessionStore_FactoryOverloadResolvesSuppliedStoreAndRemovesVersionedRegistration()
    {
        ServiceCollection services = new();
        services.AddIncursaAgents();

        RecordingSessionStore sessionStore = new();
        services.AddAgentSessionStore(_ => sessionStore);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(sessionStore, provider.GetRequiredService<IAgentSessionStore>());
        Assert.Null(provider.GetService<IVersionedAgentSessionStore>());
    }

    /// <summary>Factory-based versioned-store replacement keeps the session and versioned interfaces aligned.</summary>
    /// <intent>Protect the versioned session-store override hook.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddVersionedAgentSessionStore(Func&lt;IServiceProvider, IVersionedAgentSessionStore&gt;)` resolves the supplied versioned store and wires `IAgentSessionStore` to it.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddVersionedAgentSessionStore_FactoryOverloadResolvesSuppliedVersionedStore()
    {
        ServiceCollection services = new();
        services.AddIncursaAgents();

        RecordingVersionedSessionStore sessionStore = new();
        services.AddVersionedAgentSessionStore(_ => sessionStore);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(sessionStore, provider.GetRequiredService<IAgentSessionStore>());
        Assert.Same(sessionStore, provider.GetRequiredService<IVersionedAgentSessionStore>());
    }

    /// <summary>Default OpenAI Responses registration exposes the named clients and default option shape without configuration.</summary>
    /// <intent>Protect the default OpenAI Responses DI surface.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddOpenAiResponses()` registers the named clients without requiring a configuration callback.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddOpenAiResponses_DefaultOverloadRegistersNamedClientWithoutConfiguration()
    {
        ServiceCollection services = new();
        services.AddOpenAiResponses();

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<OpenAiResponsesOptions> options = provider.GetRequiredService<IOptions<OpenAiResponsesOptions>>();
        IHttpClientFactory clientFactory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal("openai", options.Value.HttpClientName);
        Assert.Equal("incursa-agents-mcp", options.Value.McpHttpClientName);
        Assert.Equal("v1/responses", options.Value.ResponsesPath);
        Assert.Null(options.Value.BaseAddress);
        Assert.NotNull(clientFactory.CreateClient(options.Value.HttpClientName));
        Assert.NotNull(clientFactory.CreateClient(options.Value.McpHttpClientName));
    }

    /// <summary>Configured OpenAI Responses registration applies the base address to the named client.</summary>
    /// <intent>Protect the HttpClient configuration path used by OpenAI Responses.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddOpenAiResponses(Action&lt;OpenAiResponsesOptions&gt;)` flows the configured base address to the named OpenAI client.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddOpenAiResponses_ConfiguredBaseAddressFlowsToNamedHttpClient()
    {
        Uri baseAddress = new("https://example.test/openai/");

        ServiceCollection services = new();
        services.AddOpenAiResponses(options =>
        {
            options.BaseAddress = baseAddress;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        OpenAiResponsesOptions options = provider.GetRequiredService<IOptions<OpenAiResponsesOptions>>().Value;
        HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(options.HttpClientName);

        Assert.Equal(baseAddress, client.BaseAddress);
    }

    /// <summary>Default OpenAI audio registration exposes the named client without requiring configuration.</summary>
    /// <intent>Protect the default OpenAI audio DI surface.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddOpenAiAudio()` registers the named client without requiring a configuration callback.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddOpenAiAudio_DefaultOverloadRegistersNamedClientWithoutConfiguration()
    {
        ServiceCollection services = new();
        services.AddOpenAiAudio();

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<OpenAiAudioOptions> options = provider.GetRequiredService<IOptions<OpenAiAudioOptions>>();
        IHttpClientFactory clientFactory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal("openai-audio", options.Value.HttpClientName);
        Assert.Null(options.Value.BaseAddress);
        Assert.NotNull(clientFactory.CreateClient(options.Value.HttpClientName));
    }

    /// <summary>Configured OpenAI audio registration applies the base address to the named client.</summary>
    /// <intent>Protect the HttpClient configuration path used by OpenAI audio.</intent>
    /// <scenario>LIB-EXT-DI-002</scenario>
    /// <behavior>`AddOpenAiAudio(Action&lt;OpenAiAudioOptions&gt;)` flows the configured base address to the named OpenAI audio client.</behavior>
    [Fact]
    [Trait("Category", "Smoke")]
    [CoverageType(RequirementCoverageType.Positive)]
    public void AddOpenAiAudio_ConfiguredBaseAddressFlowsToNamedHttpClient()
    {
        Uri baseAddress = new("https://example.test/audio/");

        ServiceCollection services = new();
        services.AddOpenAiAudio(options =>
        {
            options.BaseAddress = baseAddress;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        OpenAiAudioOptions options = provider.GetRequiredService<IOptions<OpenAiAudioOptions>>().Value;
        HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(options.HttpClientName);

        Assert.Equal(baseAddress, client.BaseAddress);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "incursa-agents-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class RecordingSessionStore : IAgentSessionStore
    {
        public ValueTask<AgentSession?> LoadAsync(string sessionKey, CancellationToken cancellationToken)
            => ValueTask.FromResult<AgentSession?>(null);

        public ValueTask SaveAsync(AgentSession session, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class RecordingVersionedSessionStore : IVersionedAgentSessionStore
    {
        public ValueTask<AgentSession?> LoadAsync(string sessionKey, CancellationToken cancellationToken)
            => ValueTask.FromResult<AgentSession?>(null);

        public ValueTask SaveAsync(AgentSession session, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(0);
    }
}
