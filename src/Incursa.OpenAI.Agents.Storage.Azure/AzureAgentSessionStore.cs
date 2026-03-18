using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Incursa.OpenAI.Agents.Storage.Azure;

/// <summary>Azure Blob-backed <see cref="IVersionedAgentSessionStore"/> implementation.</summary>
public sealed class AzureAgentSessionStore : IVersionedAgentSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly BlobContainerClient containerClient;
    private readonly AzureAgentSessionStoreOptions options;

    /// <summary>Creates a new Azure-backed session store.</summary>
    public AzureAgentSessionStore(BlobContainerClient containerClient, AzureAgentSessionStoreOptions? options = null)
    {
        this.containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        this.options = options ?? new AzureAgentSessionStoreOptions();
    }

    /// <summary>Loads a session without specifying cancellation.</summary>
    public ValueTask<AgentSession?> LoadAsync(string sessionKey)
        => LoadAsync(sessionKey, CancellationToken.None);

    /// <summary>Loads a session.</summary>
    public async ValueTask<AgentSession?> LoadAsync(string sessionKey, CancellationToken cancellationToken)
    {
        StoredSession? stored = await TryLoadStoredSessionAsync(sessionKey, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        if (AzureSessionStoreBehavior.IsExpired(stored.Session, DateTimeOffset.UtcNow))
        {
            if (options.SessionOptions.CleanupExpiredOnLoad)
            {
                await TryDeleteAsync(GetBlobClient(sessionKey), cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        return stored.Session.Clone();
    }

    /// <summary>Saves a session without specifying cancellation.</summary>
    public ValueTask SaveAsync(AgentSession session)
        => SaveAsync(session, CancellationToken.None);

    /// <summary>Saves a session.</summary>
    public async ValueTask SaveAsync(AgentSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

        if (options.SessionOptions.CleanupExpiredOnSave)
        {
            await CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);
        }

        BlobClient blobClient = GetBlobClient(session.SessionKey);
        StoredSession? existing = await TryLoadStoredSessionAsync(blobClient, session.SessionKey, cancellationToken).ConfigureAwait(false);

        if (existing is not null && existing.Session.Version != session.Version)
        {
            throw new AgentSessionVersionMismatchException(session.SessionKey, session.Version, existing.Session.Version);
        }

        AgentSession prepared = AzureSessionStoreBehavior.PrepareForSave(session, existing?.Session, options.SessionOptions, DateTimeOffset.UtcNow);

        await using MemoryStream content = new();
        await JsonSerializer.SerializeAsync(content, prepared, SerializerOptions, cancellationToken).ConfigureAwait(false);
        content.Position = 0;

        BlobUploadOptions uploadOptions = new()
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
            Conditions = existing is null
                ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                : new BlobRequestConditions { IfMatch = existing.ETag },
        };

        try
        {
            await blobClient.UploadAsync(content, uploadOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            StoredSession? current = await TryLoadStoredSessionAsync(blobClient, session.SessionKey, cancellationToken).ConfigureAwait(false);
            long actualVersion = current?.Session.Version ?? existing?.Session.Version ?? 0;
            throw new AgentSessionVersionMismatchException(session.SessionKey, session.Version, actualVersion);
        }

        AzureSessionStoreBehavior.ApplySavedState(session, prepared);
    }

    /// <summary>Removes expired sessions without specifying cancellation.</summary>
    public ValueTask<int> CleanupExpiredSessionsAsync()
        => CleanupExpiredSessionsAsync(CancellationToken.None);

    /// <summary>Removes expired sessions.</summary>
    public async ValueTask<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        if (!await containerClient.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var removed = 0;
        // Only blobs under the configured prefix belong to this store, so the cleanup pass
        // stays scoped to the adapter's namespace instead of scanning the whole container.
        await foreach (BlobItem blob in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, GetPrefix(), cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            BlobClient blobClient = containerClient.GetBlobClient(blob.Name);
            StoredSession? stored = await TryLoadStoredSessionAsync(blobClient, blob.Name, cancellationToken).ConfigureAwait(false);
            if (stored is null || !AzureSessionStoreBehavior.IsExpired(stored.Session, DateTimeOffset.UtcNow))
            {
                continue;
            }

            if (await TryDeleteAsync(blobClient, cancellationToken).ConfigureAwait(false))
            {
                removed++;
            }
        }

        return removed;
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (!options.CreateContainerIfMissing)
        {
            return;
        }

        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private BlobClient GetBlobClient(string sessionKey)
        => containerClient.GetBlobClient(BuildBlobName(sessionKey));

    private string BuildBlobName(string sessionKey)
    {
        // Session keys are hashed before they are used as blob names so user-facing
        // identifiers do not leak into storage paths and collisions stay predictable.
        string prefix = GetPrefix();
        string name = $"{HashSessionKey(sessionKey)}.json";
        return string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix.TrimEnd('/')}/{name}";
    }

    private string GetPrefix()
        => options.Prefix?.Trim() ?? string.Empty;

    private async Task<StoredSession?> TryLoadStoredSessionAsync(string sessionKey, CancellationToken cancellationToken)
        => await TryLoadStoredSessionAsync(GetBlobClient(sessionKey), sessionKey, cancellationToken).ConfigureAwait(false);

    private static async Task<bool> TryDeleteAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        try
        {
            return await blobClient.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                default,
                cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 409 or 412)
        {
            return false;
        }
    }

    private async Task<StoredSession?> TryLoadStoredSessionAsync(BlobClient blobClient, string sessionKey, CancellationToken cancellationToken)
    {
        try
        {
            // Blob properties give us the current ETag for optimistic concurrency while
            // the stream itself carries the serialized session payload.
            Response<BlobProperties> properties = await blobClient.GetPropertiesAsync(default, cancellationToken).ConfigureAwait(false);
            await using Stream content = await blobClient.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            AgentSession? session = await JsonSerializer.DeserializeAsync<AgentSession>(content, SerializerOptions, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return null;
            }

            return new StoredSession(session, properties.Value.ETag);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private static string HashSessionKey(string sessionKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record StoredSession(AgentSession Session, ETag ETag);
}
