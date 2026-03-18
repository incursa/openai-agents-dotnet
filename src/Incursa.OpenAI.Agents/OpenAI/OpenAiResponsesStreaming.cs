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
        // Pulls through the raw OpenAI event discriminator for branching callers.
        eventType = streamEvent.Data?["type"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(eventType);
    }

    /// <summary>Gets the response ID from a streaming response event when available.</summary>
    public static bool TryGetResponseId(OpenAiResponsesStreamEvent streamEvent, out string? responseId)
    {
        // Response ID is needed to stitch downstream events back to a completed turn.
        responseId = streamEvent.Data["response"]?["id"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(responseId);
    }

    /// <summary>Gets the output item payload for a streaming event when present.</summary>
    public static bool TryGetOutputItem(OpenAiResponsesStreamEvent streamEvent, out JsonObject? item)
    {
        // Exposes the provider item node only for item-oriented event handlers.
        item = streamEvent.Data["item"] as JsonObject;
        return item is not null;
    }
}
