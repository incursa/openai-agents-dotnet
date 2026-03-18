using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Containers;
using Incursa.OpenAI.Agents.Storage.S3;
using Testcontainers.Minio;

namespace Incursa.OpenAI.Agents.Storage.S3.IntegrationTests;

/// <summary>Integration tests for the S3-backed agent session store.</summary>
/// <intent>Verify the adapter against a real MinIO-backed S3-compatible endpoint.</intent>
/// <scenario>LIB-STORAGE-S3-INT-001</scenario>
/// <behavior>The store saves, loads, trims, and rejects stale writes against a live backing bucket.</behavior>
public sealed class S3AgentSessionStoreIntegrationTests : IAsyncLifetime
{
    private const string BucketName = "incursa-agent-sessions";
    private const string Image = "minio/minio:RELEASE.2023-01-31T02-24-19Z";
    private const string AccessKey = "minioadmin";
    private const string SecretKey = "minioadmin";

    private readonly IContainer minio = new MinioBuilder(Image)
        // The MinIO root credentials are injected explicitly so the AWS SDK client can
        // authenticate without relying on external environment state.
        .WithEnvironment("MINIO_ROOT_USER", AccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
        .Build();

    private IAmazonS3 client = null!;

    /// <summary>Starts the MinIO container and prepares the S3 client.</summary>
    public async Task InitializeAsync()
    {
        await minio.StartAsync();
        // MinIO is used as a real S3-compatible endpoint, so the client is built from
        // the container's mapped port rather than a mocked transport.
        client = CreateClient();
    }

    /// <summary>Stops and disposes the MinIO container used by the integration tests.</summary>
    public async Task DisposeAsync()
        => await minio.DisposeAsync();

    /// <summary>The S3 session store round-trips the persisted session state through MinIO.</summary>
    /// <intent>Protect the live S3 serialization path rather than only the in-memory contract.</intent>
    /// <scenario>LIB-STORAGE-S3-INT-001-ROUNDTRIP</scenario>
    /// <behavior>Saving then loading a session preserves the key runtime fields and conversation content.</behavior>
    [Trait("Category", "Integration")]
    [Trait("RequiresDocker", "true")]
    [Fact]
    public async Task SaveAndLoadAsync_PreservesSessionState()
    {
        S3AgentSessionStore store = CreateStore();
        AgentSession session = CreateSession("s3-roundtrip", "hello");
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

    /// <summary>The S3 session store rejects stale writes after a newer version is persisted.</summary>
    /// <intent>Protect optimistic concurrency for resumable session state.</intent>
    /// <scenario>LIB-STORAGE-S3-INT-001-VERSION</scenario>
    /// <behavior>A stale copy of the same session key fails with a version mismatch after a newer save succeeds.</behavior>
    [Trait("Category", "Integration")]
    [Trait("RequiresDocker", "true")]
    [Fact]
    public async Task SaveAsync_RejectsStaleVersionWrites()
    {
        S3AgentSessionStore store = CreateStore();
        AgentSession session = CreateSession("s3-version", "hello");

        await store.SaveAsync(session);
        AgentSession fresh = (await store.LoadAsync(session.SessionKey))!;
        AgentSession stale = (await store.LoadAsync(session.SessionKey))!;

        fresh.LastResponseId = "resp-fresh";
        await store.SaveAsync(fresh);

        stale.LastResponseId = "resp-stale";
        await Assert.ThrowsAsync<AgentSessionVersionMismatchException>(() => store.SaveAsync(stale).AsTask());
    }

    /// <summary>The S3 session store removes expired sessions during explicit cleanup.</summary>
    /// <intent>Protect cleanup behavior against real bucket listing and deletion semantics.</intent>
    /// <scenario>LIB-STORAGE-S3-INT-001-CLEANUP</scenario>
    /// <behavior>Expired sessions are deleted and no longer load after cleanup runs.</behavior>
    [Trait("Category", "Integration")]
    [Trait("RequiresDocker", "true")]
    [Fact]
    public async Task CleanupExpiredSessionsAsync_RemovesExpiredSessions()
    {
        S3AgentSessionStore store = CreateStore(options =>
        {
            options.SessionOptions = new AgentSessionStoreOptions
            {
                AbsoluteLifetime = TimeSpan.Zero,
                CleanupExpiredOnSave = false,
                CleanupExpiredOnLoad = true,
            };
        });

        AgentSession session = CreateSession("s3-expired", "hello");

        await store.SaveAsync(session);
        // The cleanup pass needs the session to be observably expired in the backing
        // store, so the test waits for the zero-lifetime timestamp to elapse.
        await Task.Delay(25);

        int removed = await store.CleanupExpiredSessionsAsync();
        AgentSession? loaded = await store.LoadAsync(session.SessionKey);

        Assert.Equal(1, removed);
        Assert.Null(loaded);
    }

    private S3AgentSessionStore CreateStore(Action<S3AgentSessionStoreOptions>? configure = null)
    {
        S3AgentSessionStoreOptions options = new()
        {
            Client = client,
            BucketName = BucketName,
            CreateBucketIfMissing = true,
        };

        configure?.Invoke(options);
        return new S3AgentSessionStore(client, options);
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

    private IAmazonS3 CreateClient()
        => new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                // Path-style requests keep the local MinIO endpoint compatible with the
                // bucket naming scheme used by the adapter.
                ForcePathStyle = true,
                ServiceURL = $"http://{minio.Hostname}:{minio.GetMappedPublicPort(9000)}",
                AuthenticationRegion = "us-east-1",
            });
}
