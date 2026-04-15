namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Describes the kind of evidence a test provides.</summary>
public enum RequirementCoverageType
{
    /// <summary>Positive-path coverage for expected behavior.</summary>
    Positive,
    /// <summary>Negative-path coverage for rejection and error behavior.</summary>
    Negative,
    /// <summary>Boundary or threshold coverage for edge conditions.</summary>
    Edge,
    /// <summary>Fuzz coverage for randomized or malformed input robustness.</summary>
    Fuzz,
    /// <summary>Benchmark coverage for performance-sensitive paths.</summary>
    Benchmark
}
