using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;

namespace Incursa.OpenAI.Agents.Storage.S3;

/// <summary>AWS S3-backed <see cref="IVersionedAgentSessionStore"/> implementation.</summary>
public sealed class S3AgentSessionStore : IVersionedAgentSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IAmazonS3 client;
    private readonly S3AgentSessionStoreOptions options;

    /// <summary>Creates a new S3-backed session store.</summary>
    public S3AgentSessionStore(IAmazonS3 client, S3AgentSessionStoreOptions? options = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? new S3AgentSessionStoreOptions();
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

        if (S3SessionStoreBehavior.IsExpired(stored.Session, DateTimeOffset.UtcNow))
        {
            if (options.SessionOptions.CleanupExpiredOnLoad)
            {
                await TryDeleteAsync(BuildObjectKey(sessionKey), cancellationToken).ConfigureAwait(false);
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
        await EnsureBucketAsync(cancellationToken).ConfigureAwait(false);

        if (options.SessionOptions.CleanupExpiredOnSave)
        {
            await CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);
        }

        string key = BuildObjectKey(session.SessionKey);
        StoredSession? existing = await TryLoadStoredSessionByObjectKeyAsync(key, cancellationToken).ConfigureAwait(false);

        if (existing is not null && existing.Session.Version != session.Version)
        {
            throw new AgentSessionVersionMismatchException(session.SessionKey, session.Version, existing.Session.Version);
        }

        AgentSession prepared = S3SessionStoreBehavior.PrepareForSave(session, existing?.Session, options.SessionOptions, DateTimeOffset.UtcNow);

        await using MemoryStream content = new();
        await JsonSerializer.SerializeAsync(content, prepared, SerializerOptions, cancellationToken).ConfigureAwait(false);
        content.Position = 0;

        PutObjectRequest request = new()
        {
            BucketName = options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = "application/json",
            IfMatch = existing?.ETag,
            IfNoneMatch = existing is null ? "*" : null,
            AutoResetStreamPosition = true,
            UseChunkEncoding = false,
        };

        try
        {
            await client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (IsConcurrencyConflict(exception))
        {
            StoredSession? current = await TryLoadStoredSessionByObjectKeyAsync(key, cancellationToken).ConfigureAwait(false);
            long actualVersion = current?.Session.Version ?? existing?.Session.Version ?? 0;
            throw new AgentSessionVersionMismatchException(session.SessionKey, session.Version, actualVersion);
        }

        S3SessionStoreBehavior.ApplySavedState(session, prepared);
    }

    /// <summary>Removes expired sessions without specifying cancellation.</summary>
    public ValueTask<int> CleanupExpiredSessionsAsync()
        => CleanupExpiredSessionsAsync(CancellationToken.None);

    /// <summary>Removes expired sessions.</summary>
    public async ValueTask<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            return 0;
        }

        var removed = 0;
        string? continuationToken = null;

        do
        {
            // S3 listings are paged, so cleanup walks the bucket a page at a time and
            // only examines objects under the adapter's configured prefix.
            ListObjectsV2Response response = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = options.BucketName,
                Prefix = GetPrefix(),
                ContinuationToken = continuationToken,
            }, cancellationToken).ConfigureAwait(false);

            foreach (S3Object item in response.S3Objects ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                StoredSession? stored = await TryLoadStoredSessionByObjectKeyAsync(item.Key, cancellationToken).ConfigureAwait(false);
                if (stored is null || !S3SessionStoreBehavior.IsExpired(stored.Session, DateTimeOffset.UtcNow))
                {
                    continue;
                }

                if (await TryDeleteAsync(item.Key, cancellationToken).ConfigureAwait(false))
                {
                    removed++;
                }
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));

        return removed;
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (!options.CreateBucketIfMissing || string.IsNullOrWhiteSpace(options.BucketName))
        {
            return;
        }

        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = options.BucketName }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    private string BuildObjectKey(string sessionKey)
    {
        // The hashed object key keeps the storage layout stable and avoids exposing
        // user-provided session identifiers directly in the bucket.
        string prefix = GetPrefix();
        string name = $"{HashSessionKey(sessionKey)}.json";
        return string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix.TrimEnd('/')}/{name}";
    }

    private string GetPrefix()
        => options.Prefix?.Trim() ?? string.Empty;

    private async Task<StoredSession?> TryLoadStoredSessionAsync(string sessionKey, CancellationToken cancellationToken)
        => await TryLoadStoredSessionByObjectKeyAsync(BuildObjectKey(sessionKey), cancellationToken).ConfigureAwait(false);

    private async Task<StoredSession?> TryLoadStoredSessionByObjectKeyAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            // The object key already encodes the hash, so this path must read the object
            // exactly as named rather than hashing again.
            GetObjectResponse response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = options.BucketName,
                Key = objectKey,
            }, cancellationToken).ConfigureAwait(false);

            await using Stream content = response.ResponseStream;
            AgentSession? session = await JsonSerializer.DeserializeAsync<AgentSession>(content, SerializerOptions, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return null;
            }

            return new StoredSession(session, response.ETag);
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return null;
        }
    }

    private async Task<bool> TryDeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = options.BucketName,
                Key = objectKey,
            }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception) || IsConcurrencyConflict(exception))
        {
            return false;
        }
    }

    private static bool IsNotFound(AmazonS3Exception exception)
        => exception.StatusCode == HttpStatusCode.NotFound || exception.ErrorCode is "NoSuchKey" or "NotFound";

    private static bool IsConcurrencyConflict(AmazonS3Exception exception)
        => exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict;

    private static string HashSessionKey(string sessionKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record StoredSession(AgentSession Session, string ETag);
}
