#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests the raw streaming helpers exposed for OpenAI Responses events.</summary>
public sealed class OpenAiResponsesStreamingTests
{
    /// <summary>Raw-model helpers only activate for raw model stream events.</summary>
    /// <intent>Protect stream consumers that narrow mixed event streams down to raw model events first.</intent>
    /// <scenario>LIB-OAI-STREAM-HELPER-001</scenario>
    /// <behavior>Raw-model detection returns true only for raw model events and extracts the nested raw event type when present.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void RawModelHelpers_RecognizeRawEventsAndExtractNestedType()
    {
        AgentStreamEvent rawEvent = new(
            AgentStreamEventTypes.RawModelEvent,
            "triage",
            null,
            new JsonObject
            {
                ["type"] = "response.output_item.added",
            },
            null,
            null);
        AgentStreamEvent otherEvent = new(AgentStreamEventTypes.AgentUpdated, "triage");

        Assert.True(OpenAiResponsesStreaming.IsRawModelEvent(rawEvent));
        Assert.False(OpenAiResponsesStreaming.IsRawModelEvent(otherEvent));
        Assert.True(OpenAiResponsesStreaming.TryGetRawEventType(rawEvent, out string? eventType));
        Assert.Equal("response.output_item.added", eventType);
        Assert.False(OpenAiResponsesStreaming.TryGetRawEventType(otherEvent, out string? missingType));
        Assert.Null(missingType);
    }

    /// <summary>Response IDs fall back to the raw JSON payload when no typed update is attached.</summary>
    /// <intent>Protect helper callers that work with persisted or rehydrated raw stream events.</intent>
    /// <scenario>LIB-OAI-STREAM-HELPER-002</scenario>
    /// <behavior>The helper reads the response ID from the raw payload and reports failure when it is absent.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void TryGetResponseId_ReadsRawPayloadWhenTypedUpdateIsMissing()
    {
        OpenAiResponsesStreamEvent withResponse = new("response.completed", new JsonObject
        {
            ["response"] = new JsonObject
            {
                ["id"] = "resp-123",
            },
        });
        OpenAiResponsesStreamEvent withoutResponse = new("response.completed", new JsonObject());

        Assert.True(OpenAiResponsesStreaming.TryGetResponseId(withResponse, out string? responseId));
        Assert.Equal("resp-123", responseId);
        Assert.False(OpenAiResponsesStreaming.TryGetResponseId(withoutResponse, out string? missingResponseId));
        Assert.Null(missingResponseId);
    }

    /// <summary>Output-item helpers read raw JSON payloads when typed updates are unavailable.</summary>
    /// <intent>Protect downstream stream consumers that inspect persisted event JSON instead of SDK update instances.</intent>
    /// <scenario>LIB-OAI-STREAM-HELPER-003</scenario>
    /// <behavior>The helper returns the raw item payload and reports failure when no item is present.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void TryGetOutputItem_ReadsRawPayloadWhenTypedUpdateIsMissing()
    {
        JsonObject rawItem = new()
        {
            ["type"] = "message",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = "hello",
                },
            },
        };

        OpenAiResponsesStreamEvent withItem = new("response.output_item.done", new JsonObject
        {
            ["item"] = rawItem.DeepClone(),
        });
        OpenAiResponsesStreamEvent withoutItem = new("response.output_item.done", new JsonObject());

        Assert.True(OpenAiResponsesStreaming.TryGetOutputItem(withItem, out JsonObject? item));
        Assert.Equal(rawItem.ToJsonString(), item?.ToJsonString());
        Assert.False(OpenAiResponsesStreaming.TryGetOutputItem(withoutItem, out JsonObject? missingItem));
        Assert.Null(missingItem);
    }

    /// <summary>Typed output-item updates serialize their SDK item payload when possible.</summary>
    /// <intent>Protect the helper path used by live streamed SDK updates.</intent>
    /// <scenario>LIB-OAI-STREAM-HELPER-004</scenario>
    /// <behavior>Typed `response.output_item.added` updates expose the serialized output item payload through the helper.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void TryGetOutputItem_ReadsTypedAddedUpdates()
    {
        JsonObject payload = new()
        {
            ["type"] = "response.output_item.added",
            ["output_index"] = 0,
            ["item"] = new JsonObject
            {
                ["type"] = "message",
                ["id"] = "msg_1",
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "output_text",
                        ["text"] = "hello",
                    },
                },
            },
        };

        StreamingResponseUpdate update = OpenAiSdkSerialization.ReadModel<StreamingResponseUpdate>(payload);
        OpenAiResponsesStreamEvent streamEvent = new("response.output_item.added", payload)
        {
            Update = update,
        };

        Assert.True(OpenAiResponsesStreaming.TryGetOutputItem(streamEvent, out JsonObject? item));
        Assert.Equal("message", item?["type"]?.GetValue<string>());
        Assert.Equal("hello", item?["content"]?[0]?["text"]?.GetValue<string>());
    }

    /// <summary>Typed output-item updates fail closed when the SDK item cannot be serialized back to JSON.</summary>
    /// <intent>Protect stream consumers from serializer crashes on malformed typed SDK output items.</intent>
    /// <scenario>LIB-OAI-STREAM-HELPER-005</scenario>
    /// <behavior>When typed item serialization fails, the helper reports failure and returns no item.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryGetOutputItem_ReturnsFalseWhenTypedItemSerializationFails()
    {
        JsonObject payload = new()
        {
            ["type"] = "response.output_item.done",
            ["output_index"] = 0,
            ["item"] = new JsonObject
            {
                ["type"] = "mcp_list_tools",
                ["server_label"] = "mail",
                ["tools"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "search_mail",
                    },
                },
            },
        };

        StreamingResponseUpdate update = OpenAiSdkSerialization.ReadModel<StreamingResponseUpdate>(payload);
        OpenAiResponsesStreamEvent streamEvent = new("response.output_item.done", payload)
        {
            Update = update,
        };

        Assert.False(OpenAiResponsesStreaming.TryGetOutputItem(streamEvent, out JsonObject? item));
        Assert.Null(item);
    }
}
