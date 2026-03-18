using Azure.Storage.Blobs;
using DotNet.Testcontainers.Containers;
using Incursa.OpenAI.Agents.Storage.Azure;
using Testcontainers.Azurite;

namespace Incursa.OpenAI.Agents.Storage.Azure.IntegrationTests;

/// <summary>Integration tests for the Azure Blob-backed agent session store.</summary>
/// <intent>Verify the adapter against a real Azurite-backed storage endpoint.</intent>
/// <scenario>LIB-STORAGE-AZURE-INT-001</scenario>
/// <behavior>The store saves, loads, trims, and rejects stale writes against a live backing container.</behavior>
public sealed class AzureAgentSessionStoreIntegrationTests : IAsyncLifetime
{
    private const string ContainerName = "incursa-agent-sessions";
    private const string Image = "mcr.microsoft.com/azure-storage/azurite:3.33.0";

    private readonly AzuriteContainer azurite = new AzuriteBuilder(Image)
        // Azurite refuses the newer API version unless the check is skipped, so the
        // test container is pinned to the same compatibility mode the adapter uses in CI.
        .WithCommand("--skipApiVersionCheck")
        .Build();

    private BlobContainerClient containerClient = null!;

    public async Task InitializeAsync()
    {
        await azurite.StartAsync();
        // The integration test talks to the live container endpoint instead of a mock
        // client so we exercise the actual blob SDK request path.
        containerClient = new BlobContainerClient(GetConnectionString(), ContainerName);
    }

    public async Task DisposeAsync()
        => await azurite.DisposeAsync();

    /// <summary>The Azure session store round-trips the persisted session state through Azurite.</summary>
    /// <intent>Protect the live blob serialization path rather than only the in-memory contract.</intent>
    /// <scenario>LIB-STORAGE-AZURE-INT-001-ROUNDTRIP</scenario>
    /// <behavior>Saving then loading a session preserves the key runtime fields and conversation content.</behavior>
    [Trait("Category", "Integration")]
    [Trait("RequiresDocker", "true")]
    [Fact]
    public async Task SaveAndLoadAsync_PreservesSessionState()
    {
        AzureAgentSessionStore store = CreateStore();
        AgentSession session = CreateSession("azure-roundtrip", "hello");
        session.CurrentAgentName = "triage";
        session.LastResponseId = "resp-1";

        await store.SaveAsync(session);
        AgentSession? loaded = await store.LoadAsync(session.SessionKey);

        Assert.NotNull(loaded);
        Assert.Equal("triage", loaded!.CurrentAgentName);
        Assert.Equal("resp-1", loaded.LastResponseId);
        Assert.Single(loaded.Conversation);
        Assert.Equal("hello", loaded.Conversation[0].Text);
        Assert.Equal(1, loaded.Version);
    }

    /// <summary>The Azure session store rejects stale writes after a newer version is persisted.</summary>
    /// <intent>Protect optimistic concurrency for resumable session state.</intent>
    /// <scenario>LIB-STORAGE-AZURE-INT-001-VERSION</scenario>
    /// <behavior>A stale copy of the same session key fails with a version mismatch after a newer save succeeds.</behavior>
    [Trait("Category", "Integration")]
    [Trait("RequiresDocker", "true")]
    [Fact]
    public async Task SaveAsync_RejectsStaleVersionWrites()
    {
        AzureAgentSessionStore store = CreateStore();
        AgentSession session = CreateSession("azure-version", "hello");

        await store.SaveAsync(session);
        AgentSession fresh = (await store.LoadAsync(session.SessionKey))!;
        AgentSession stale = (await store.LoadAsync(session.SessionKey))!;

        fresh.LastResponseId = "resp-fresh";
        await store.SaveAsync(fresh);

        stale.LastResponseId = "resp-stale";
        await Assert.ThrowsAsync<AgentSessionVersionMismatchException>(() => store.SaveAsync(stale).AsTask());
    }

    /// <summary>The Azure session store removes expired sessions during explicit cleanup.</summary>
    /// <intent>Protect cleanup behavior against real blob listing and deletion semantics.</intent>
    /// <scenario>LIB-STORAGE-AZURE-INT-001-CLEANUP</scenario>
    /// <behavior>Expired sessions are deleted and no longer load after cleanup runs.</behavior>
    [Trait("Category", "Integration")]
    [Trait("RequiresDocker", "true")]
    [Fact]
    public async Task CleanupExpiredSessionsAsync_RemovesExpiredSessions()
    {
        AzureAgentSessionStore store = CreateStore(options =>
        {
            options.SessionOptions = new AgentSessionStoreOptions
            {
                AbsoluteLifetime = TimeSpan.Zero,
                CleanupExpiredOnSave = false,
                CleanupExpiredOnLoad = true,
            };
        });

        AgentSession session = CreateSession("azure-expired", "hello");

        await store.SaveAsync(session);
        // Give the expiry clock enough time to move past the zero-length lifetime before
        // cleanup walks the container.
        await Task.Delay(25);

        int removed = await store.CleanupExpiredSessionsAsync();
        AgentSession? loaded = await store.LoadAsync(session.SessionKey);

        Assert.Equal(1, removed);
        Assert.Null(loaded);
    }

    private AzureAgentSessionStore CreateStore(Action<AzureAgentSessionStoreOptions>? configure = null)
    {
        AzureAgentSessionStoreOptions options = new()
        {
            CreateContainerIfMissing = true,
        };

        configure?.Invoke(options);
        return new AzureAgentSessionStore(containerClient, options);
    }

    private static AgentSession CreateSession(string sessionKey, string userText)
        => new()
        {
            SessionKey = sessionKey,
            Conversation =
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = userText },
            ],
        };

    private string GetConnectionString()
        => azurite.GetConnectionString();
}
