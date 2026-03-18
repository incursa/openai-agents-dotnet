namespace Incursa.OpenAI.Agents;

/// <summary>
/// Carries the data contract for ApprovalDecision.
/// </summary>

public sealed record ApprovalDecision
{
    /// <summary>Creates an approval decision with optional reason.</summary>
    public ApprovalDecision(bool requiresApproval)
        : this(requiresApproval, null)
    {
    }

    /// <summary>Creates an approval decision specifying whether approval is required.</summary>
    public ApprovalDecision(bool requiresApproval, string? reason)
    {
        RequiresApproval = requiresApproval;
        Reason = reason;
    }

    /// <summary>Gets whether approval is required.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>Gets optional approval decision reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Creates an allow decision.</summary>
    public static ApprovalDecision Allow() => new(false);

    /// <summary>Creates a require-approval decision.</summary>
    public static ApprovalDecision Require(string? reason) => new(true, reason);
}
