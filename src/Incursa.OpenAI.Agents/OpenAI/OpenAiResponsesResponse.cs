using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Wraps the raw JSON payload returned by the OpenAI Responses API.
/// </summary>

public sealed record OpenAiResponsesResponse
{
    /// <summary>Creates a parsed responses payload with response identifier.</summary>
    public OpenAiResponsesResponse(string id, JsonObject raw)
    {
        Id = id;
        Raw = raw;
    }

    /// <summary>Gets or sets the response identifier.</summary>
    public string Id { get; init; }

    /// <summary>Gets or sets the raw JSON payload returned by OpenAI.</summary>
    public JsonObject Raw { get; init; }
}
