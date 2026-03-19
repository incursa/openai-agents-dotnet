#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Provides helpers for inspecting streamed OpenAI Responses events.
/// </summary>

public static class OpenAiResponsesStreaming
{
    /// <summary>Checks whether an event is a raw model event.</summary>
    public static bool IsRawModelEvent(AgentStreamEvent streamEvent)
        => string.Equals(streamEvent.EventType, AgentStreamEventTypes.RawModelEvent, StringComparison.Ordinal);

    /// <summary>Gets the underlying raw response type from a stream event when available.</summary>
    public static bool TryGetRawEventType(AgentStreamEvent streamEvent, out string? eventType)
    {
        eventType = streamEvent.Data?["type"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(eventType);
    }

    /// <summary>Gets the response ID from a streaming response event when available.</summary>
    public static bool TryGetResponseId(OpenAiResponsesStreamEvent streamEvent, out string? responseId)
    {
        responseId = streamEvent.Update switch
        {
            StreamingResponseCreatedUpdate created => created.Response?.Id,
            StreamingResponseInProgressUpdate inProgress => inProgress.Response?.Id,
            StreamingResponseCompletedUpdate completed => completed.Response?.Id,
            StreamingResponseIncompleteUpdate incomplete => incomplete.Response?.Id,
            StreamingResponseFailedUpdate failed => failed.Response?.Id,
            StreamingResponseQueuedUpdate queued => queued.Response?.Id,
            _ => streamEvent.Data["response"]?["id"]?.GetValue<string>(),
        };
        return !string.IsNullOrWhiteSpace(responseId);
    }

    /// <summary>Gets the output item payload for a streaming event when present.</summary>
    public static bool TryGetOutputItem(OpenAiResponsesStreamEvent streamEvent, out JsonObject? item)
    {
        item = streamEvent.Update switch
        {
            StreamingResponseOutputItemAddedUpdate added => OpenAiSdkSerialization.ToJsonObject(added.Item),
            StreamingResponseOutputItemDoneUpdate done => OpenAiSdkSerialization.ToJsonObject(done.Item),
            _ => streamEvent.Data["item"] as JsonObject,
        };
        return item is not null;
    }
}
