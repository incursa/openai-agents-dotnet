using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Storage.Azure;

internal static class AzureSessionStoreBehavior
{
    internal static AgentSession PrepareForSave(AgentSession source, AgentSession? existing, AgentSessionStoreOptions options, DateTimeOffset now)
    {
        AgentSession clone = TrimSession(source, options);
        clone.CreatedUtc = existing?.CreatedUtc != default ? existing!.CreatedUtc : (source.CreatedUtc == default ? now : source.CreatedUtc);
        clone.UpdatedUtc = now;
        clone.Version = (existing?.Version ?? 0) + 1;
        clone.ExpiresUtc = CalculateExpiry(clone, options, now);
        ApplySavedState(source, clone);
        return clone;
    }

    internal static AgentSession TrimSession(AgentSession session, AgentSessionStoreOptions options)
    {
        AgentSession clone = session.Clone();
        if (options.CompactionMode == Sessions.None)
        {
            return clone;
        }

        var originalCount = clone.Conversation.Count;
        var startIndex = 0;

        if (options.MaxTurns is int maxTurns && maxTurns > 0)
        {
            var userInputIndexes = clone.Conversation
                .Select((item, index) => (item, index))
                .Where(pair => pair.item.ItemType == AgentItemTypes.UserInput)
                .Select(pair => pair.index)
                .ToArray();

            if (userInputIndexes.Length > maxTurns)
            {
                startIndex = Math.Max(startIndex, userInputIndexes[^maxTurns]);
            }
        }

        if (startIndex > 0)
        {
            clone.Conversation = clone.Conversation.Skip(startIndex).ToList();
        }

        if (options.MaxConversationItems is int maxConversationItems
            && maxConversationItems > 0
            && clone.Conversation.Count > maxConversationItems)
        {
            clone.Conversation = clone.Conversation.Skip(clone.Conversation.Count - maxConversationItems).ToList();
        }

        clone.TrimmedItemCount += Math.Max(0, originalCount - clone.Conversation.Count);
        return clone;
    }

    internal static void ApplySavedState(AgentSession target, AgentSession saved)
    {
        target.Conversation = saved.Conversation.ToList();
        target.CurrentAgentName = saved.CurrentAgentName;
        target.LastResponseId = saved.LastResponseId;
        target.TurnsExecuted = saved.TurnsExecuted;
        target.PendingApprovals = saved.PendingApprovals.Select(item => item with { Arguments = item.Arguments?.DeepClone() }).ToList();
        target.CreatedUtc = saved.CreatedUtc;
        target.UpdatedUtc = saved.UpdatedUtc;
        target.ExpiresUtc = saved.ExpiresUtc;
        target.TrimmedItemCount = saved.TrimmedItemCount;
        target.Version = saved.Version;
    }

    internal static DateTimeOffset? CalculateExpiry(AgentSession session, AgentSessionStoreOptions options, DateTimeOffset now)
    {
        DateTimeOffset? absolute = null;
        if (options.AbsoluteLifetime is TimeSpan absoluteLifetime)
        {
            absolute = session.CreatedUtc + absoluteLifetime;
        }

        DateTimeOffset? sliding = null;
        if (options.SlidingExpiration is TimeSpan slidingExpiration)
        {
            sliding = now + slidingExpiration;
        }

        return absolute switch
        {
            null => sliding,
            _ when sliding is null => absolute,
            _ => absolute < sliding ? absolute : sliding,
        };
    }

    internal static bool IsExpired(AgentSession session, DateTimeOffset now)
    {
        if (session.ExpiresUtc is not null && session.ExpiresUtc <= now)
        {
            return true;
        }

        return false;
    }
}
