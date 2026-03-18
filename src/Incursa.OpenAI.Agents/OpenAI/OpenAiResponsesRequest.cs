using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Wraps the request body sent to the OpenAI Responses API.
/// </summary>

public sealed record OpenAiResponsesRequest
{
    /// <summary>Creates a request with streaming disabled.</summary>
    public OpenAiResponsesRequest(JsonObject body)
        : this(body, false)
    {
    }

    /// <summary>Creates a request with explicit JSON body and streaming flag.</summary>
    public OpenAiResponsesRequest(JsonObject body, bool stream)
    {
        Body = body;
        Stream = stream;
    }

    /// <summary>Gets or sets the JSON request body sent to the Responses API.</summary>
    public JsonObject Body { get; init; }

    /// <summary>Gets or sets whether streaming is requested for the call.</summary>
    public bool Stream { get; init; }
}
