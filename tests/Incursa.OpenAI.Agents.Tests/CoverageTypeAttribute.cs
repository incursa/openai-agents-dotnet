using Xunit.Sdk;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Marks a test with a requirement coverage classification.</summary>
[TraitDiscoverer("Incursa.OpenAI.Agents.Tests.CoverageTypeTraitDiscoverer", "Incursa.OpenAI.Agents.Tests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CoverageTypeAttribute : Attribute, ITraitAttribute
{
    /// <summary>Creates a new coverage-type trait for the given evidence classification.</summary>
    public CoverageTypeAttribute(RequirementCoverageType coverageType)
    {
        CoverageType = coverageType;
    }

    /// <summary>Gets the evidence classification attached to the test.</summary>
    public RequirementCoverageType CoverageType { get; }
}
