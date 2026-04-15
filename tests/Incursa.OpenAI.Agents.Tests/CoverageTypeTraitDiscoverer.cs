using Xunit.Abstractions;
using Xunit.Sdk;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Maps coverage classifications to xUnit traits.</summary>
public sealed class CoverageTypeTraitDiscoverer : ITraitDiscoverer
{
    /// <summary>Returns a single <c>CoverageType</c> trait for the configured evidence classification.</summary>
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        object? coverageType = traitAttribute.GetConstructorArguments().FirstOrDefault();

        if (coverageType is not RequirementCoverageType typedCoverageType)
        {
            return [];
        }

        return [new("CoverageType", typedCoverageType.ToString())];
    }
}
