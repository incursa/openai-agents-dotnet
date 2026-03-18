using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents the outcome of a guardrail evaluation.
/// </summary>

public sealed record GuardrailResult
{
    /// <summary>Creates a guardrail result with required trigger flag.</summary>
    public GuardrailResult(bool tripwireTriggered)
        : this(tripwireTriggered, null, null, null)
    {
    }

    /// <summary>Creates a guardrail result with optional reason.</summary>
    public GuardrailResult(bool tripwireTriggered, string? reason)
        : this(tripwireTriggered, reason, null, null)
    {
    }

    /// <summary>Creates a guardrail result with optional reason and metadata.</summary>
    public GuardrailResult(bool tripwireTriggered, string? reason, IReadOnlyDictionary<string, object?>? metadata)
        : this(tripwireTriggered, reason, metadata, null)
    {
    }

    /// <summary>Creates a fully specified guardrail result.</summary>
    public GuardrailResult(
        bool tripwireTriggered,
        string? reason,
        IReadOnlyDictionary<string, object?>? metadata,
        JsonNode? replacementData)
    {
        TripwireTriggered = tripwireTriggered;
        Reason = reason;
        Metadata = metadata;
        ReplacementData = replacementData;
    }

    /// <summary>Gets whether the guardrail tripwire was triggered.</summary>
    public bool TripwireTriggered { get; init; }

    /// <summary>Gets an optional reason for the outcome.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets optional metadata describing the tripwire decision.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>Gets optional replacement data used by output guardrails.</summary>
    public JsonNode? ReplacementData { get; init; }

    /// <summary>Returns a non-triggering guardrail result.</summary>
    public static GuardrailResult Allow() => new(false);

    /// <summary>Returns a triggered tripwire result with reason.</summary>
    public static GuardrailResult Tripwire(string reason) => new(true, reason, null);

    /// <summary>Returns a triggered tripwire result with reason and metadata.</summary>
    public static GuardrailResult Tripwire(string reason, IReadOnlyDictionary<string, object?>? metadata) => new(true, reason, metadata);
}
