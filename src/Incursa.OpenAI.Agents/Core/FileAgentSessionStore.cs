using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Incursa.OpenAI.Agents;

/// <summary>File-backed <see cref="IAgentSessionStore"/> implementation.</summary>
public sealed class FileAgentSessionStore : IVersionedAgentSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string directoryPath;
    private readonly AgentSessionStoreOptions options;

    /// <summary>Creates a file-backed session store for a directory.</summary>
    /// <param name="directoryPath">Directory where session files are stored.</param>
    public FileAgentSessionStore(string directoryPath)
        : this(directoryPath, null)
    {
    }

    /// <summary>Creates a file-backed session store.</summary>
    /// <param name="directoryPath">Directory where session files are stored.</param>
    /// <param name="options">Store options.</param>
    public FileAgentSessionStore(string directoryPath, AgentSessionStoreOptions? options)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A session store directory path is required.", nameof(directoryPath));
        }

        this.directoryPath = directoryPath;
        this.options = options ?? new AgentSessionStoreOptions();
    }

    /// <summary>Loads a session without specifying cancellation.</summary>
    /// <param name="sessionKey">Session key.</param>
    public ValueTask<AgentSession?> LoadAsync(string sessionKey)
        => LoadAsync(sessionKey, CancellationToken.None);

    /// <summary>Loads a session.</summary>
    /// <param name="sessionKey">Session key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<AgentSession?> LoadAsync(string sessionKey, CancellationToken cancellationToken)
    {
        var path = GetSessionPath(sessionKey);
        if (!File.Exists(path))
        {
            return null;
        }

        AgentSession? session = await LoadSessionFromPathAsync(path, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        if (InMemoryAgentSessionStore.IsExpired(session, DateTimeOffset.UtcNow))
        {
            if (options.CleanupExpiredOnLoad)
            {
                File.Delete(path);
            }

            return null;
        }

        return session.Clone();
    }

    /// <summary>Saves a session without specifying cancellation.</summary>
    /// <param name="session">Session to save.</param>
    public ValueTask SaveAsync(AgentSession session)
        => SaveAsync(session, CancellationToken.None);

    /// <summary>Saves a session.</summary>
    /// <param name="session">Session to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask SaveAsync(AgentSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directoryPath);

        if (options.CleanupExpiredOnSave)
        {
            await CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);
        }

        var finalPath = GetSessionPath(session.SessionKey);
        AgentSession? existing = File.Exists(finalPath)
            ? await LoadSessionFromPathAsync(finalPath, cancellationToken).ConfigureAwait(false)
            : null;

        if (existing is not null && existing.Version != session.Version)
        {
            throw new AgentSessionVersionMismatchException(session.SessionKey, session.Version, existing.Version);
        }

        AgentSession prepared = InMemoryAgentSessionStore.PrepareForSave(session, existing, options, DateTimeOffset.UtcNow);
        var tempPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";

        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, prepared, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, finalPath, overwrite: true);
        InMemoryAgentSessionStore.ApplySavedState(session, prepared);
    }

    /// <summary>Removes expired sessions without specifying cancellation.</summary>
    public ValueTask<int> CleanupExpiredSessionsAsync()
        => CleanupExpiredSessionsAsync(CancellationToken.None);

    /// <summary>Removes expired sessions.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AgentSession? session = await LoadSessionFromPathAsync(file, cancellationToken).ConfigureAwait(false);
            if (session is null || !InMemoryAgentSessionStore.IsExpired(session, DateTimeOffset.UtcNow))
            {
                continue;
            }

            File.Delete(file);
            removed++;
        }

        return removed;
    }

    private async Task<AgentSession?> LoadSessionFromPathAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AgentSession>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private string GetSessionPath(string sessionKey)
        => Path.Combine(directoryPath, $"{HashSessionKey(sessionKey)}.json");

    private static string HashSessionKey(string sessionKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
