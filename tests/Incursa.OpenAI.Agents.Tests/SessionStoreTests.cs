using Incursa.OpenAI.Agents;

namespace Incursa.OpenAI.Agents.Tests;

public sealed class SessionStoreTests
{
    /// <summary>The in-memory session store returns the saved conversation on later loads.</summary>
    /// <intent>Protect the default session persistence contract.</intent>
    /// <scenario>LIB-EXEC-SESSION-001-INMEMORY</scenario>
    /// <behavior>Saving a session to the in-memory store preserves its conversation for later retrieval.</behavior>
    [Fact]
    public async Task InMemoryStore_PersistsConversationAcrossLoads()
    {
        InMemoryAgentSessionStore store = new();
        AgentSession session = new()
        {
            SessionKey = "session-1",
            Conversation =
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
            ],
        };

        await store.SaveAsync(session);
        AgentSession? loaded = await store.LoadAsync("session-1");

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Conversation);
        Assert.Equal("hello", loaded.Conversation[0].Text);
    }

    /// <summary>The file session store preserves agent, response, and conversation state across loads.</summary>
    /// <intent>Protect durable local session persistence.</intent>
    /// <scenario>LIB-EXEC-SESSION-001-FILE</scenario>
    /// <behavior>Saving and loading a file-backed session preserves core run-state fields and conversation items.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    public async Task FileStore_PersistsSessionsAcrossLoads()
    {
        var directory = CreateTempDirectory();
        try
        {
            FileAgentSessionStore store = new(directory);
            AgentSession session = new()
            {
                SessionKey = "session-file-1",
                CurrentAgentName = "triage",
                LastResponseId = "resp-1",
                Conversation =
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
                    new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "triage") { Text = "hi" },
                ],
            };

            await store.SaveAsync(session);
            AgentSession? loaded = await store.LoadAsync("session-file-1");

            Assert.NotNull(loaded);
            Assert.Equal("triage", loaded!.CurrentAgentName);
            Assert.Equal("resp-1", loaded.LastResponseId);
            Assert.Equal(2, loaded.Conversation.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The file session store trims the conversation according to the configured retention window.</summary>
    /// <intent>Protect bounded session growth for file-backed persistence.</intent>
    /// <scenario>LIB-EXEC-SESSION-002-TRIM</scenario>
    /// <behavior>Configured limits trim older conversation items and record the trimmed item count.</behavior>
    [Fact]
    public async Task FileStore_TrimsToLatestWindowWhenConfigured()
    {
        var directory = CreateTempDirectory();
        try
        {
            FileAgentSessionStore store = new(
                directory,
                new AgentSessionStoreOptions
                {
                    MaxConversationItems = 3,
                    MaxTurns = 1,
                });

            AgentSession session = new()
            {
                SessionKey = "session-trim-1",
                Conversation =
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "one" },
                    new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "triage") { Text = "first" },
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "two" },
                    new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "triage") { Text = "second" },
                    new AgentConversationItem(AgentItemTypes.FinalOutput, "assistant", "triage") { Text = "done" },
                ],
            };

            await store.SaveAsync(session);
            AgentSession? loaded = await store.LoadAsync("session-trim-1");

            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.Conversation.Count);
            Assert.Equal("two", loaded.Conversation[0].Text);
            Assert.Equal(2, loaded.TrimmedItemCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The file session store replaces persisted content atomically.</summary>
    /// <intent>Protect durability without leaving partially written temp files behind.</intent>
    /// <scenario>LIB-EXEC-SESSION-002-ATOMIC</scenario>
    /// <behavior>Repeated saves leave only the final session file and no lingering temp files.</behavior>
    [Fact]
    public async Task FileStore_UsesAtomicReplaceWithoutLeavingTempFiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            FileAgentSessionStore store = new(directory);
            AgentSession session = new()
            {
                SessionKey = "session-atomic-1",
                Conversation =
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
                ],
            };

            await store.SaveAsync(session);
            session.LastResponseId = "resp-2";
            await store.SaveAsync(session);

            Assert.Single(Directory.GetFiles(directory, "*.json"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The in-memory versioned session store rejects stale writes.</summary>
    /// <intent>Protect optimistic concurrency for resumable sessions.</intent>
    /// <scenario>LIB-EXEC-SESSION-001-VERSION</scenario>
    /// <behavior>Saving an older session version throws a version mismatch exception after a newer write succeeds.</behavior>
    [Fact]
    public async Task InMemoryStore_RejectsStaleVersionWrites()
    {
        InMemoryAgentSessionStore store = new();
        AgentSession session = new()
        {
            SessionKey = "session-version-1",
            Conversation =
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
            ],
        };

        await store.SaveAsync(session);
        AgentSession? fresh = await store.LoadAsync(session.SessionKey);
        AgentSession? stale = await store.LoadAsync(session.SessionKey);

        Assert.NotNull(fresh);
        Assert.NotNull(stale);

        fresh!.LastResponseId = "resp-2";
        await store.SaveAsync(fresh);

        stale!.LastResponseId = "resp-stale";
        await Assert.ThrowsAsync<AgentSessionVersionMismatchException>(() => store.SaveAsync(stale).AsTask());
    }

    /// <summary>The file session store can clean up expired sessions.</summary>
    /// <intent>Protect cleanup behavior for time-limited durable session storage.</intent>
    /// <scenario>LIB-EXEC-SESSION-002-CLEANUP</scenario>
    /// <behavior>Expired file-backed sessions are removed and no longer load after cleanup.</behavior>
    [Fact]
    public async Task FileStore_CleansUpExpiredSessions()
    {
        var directory = CreateTempDirectory();
        try
        {
            FileAgentSessionStore store = new(
                directory,
                new AgentSessionStoreOptions
                {
                    AbsoluteLifetime = TimeSpan.Zero,
                    CleanupExpiredOnLoad = true,
                    CleanupExpiredOnSave = false,
                });

            AgentSession session = new()
            {
                SessionKey = "session-expired-1",
                Conversation =
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
                ],
            };

            await store.SaveAsync(session);
            await Task.Delay(20);

            var removed = await store.CleanupExpiredSessionsAsync();
            AgentSession? loaded = await store.LoadAsync(session.SessionKey);

            Assert.Equal(1, removed);
            Assert.Null(loaded);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "incursa-agents-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
