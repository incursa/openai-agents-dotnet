#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents one streamed event from the OpenAI Responses API.
/// </summary>

public sealed record OpenAiResponsesStreamEvent
{
    /// <summary>Creates a streamed event entry with response event type and payload.</summary>
    public OpenAiResponsesStreamEvent(string type, JsonObject data)
    {
        Type = type;
        Data = data;
    }

    internal OpenAiResponsesStreamEvent(StreamingResponseUpdate update)
    {
        Update = update;
        Data = OpenAiSdkSerialization.ToJsonObject(update);
        Type = Data["type"]?.GetValue<string>() ?? "unknown";
    }

    /// <summary>Gets or sets the event type value.</summary>
    public string Type { get; init; }

    /// <summary>Gets or sets the streamed event payload.</summary>
    public JsonObject Data { get; init; }

    internal StreamingResponseUpdate? Update { get; init; }
}
