using System.Reflection;
using Xunit.Abstractions;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests the coverage-type attribute and trait discoverer wiring.</summary>
public sealed class CoverageTypeAttributeTests
{
    /// <summary>Ensures the attribute can be applied to classes and methods more than once.</summary>
    /// <intent>Protect the trait-annotation contract used by requirement-home tests.</intent>
    /// <scenario>TESTDOCS-COVTYPE-001</scenario>
    /// <behavior>The attribute reports class and method targets and allows multiple instances.</behavior>
    [Fact]
    public void AttributeUsageAllowsMethodAndClassTargetsWithMultipleInstances()
    {
        AttributeUsageAttribute? usage = typeof(CoverageTypeAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Method, usage!.ValidOn);
        Assert.True(usage.AllowMultiple);
    }

    /// <summary>Ensures the attribute stores the requested evidence classification.</summary>
    /// <intent>Protect the coverage-type payload carried by the trait.</intent>
    /// <scenario>TESTDOCS-COVTYPE-002</scenario>
    /// <behavior>The attribute exposes the selected coverage classification on the CoverageType property.</behavior>
    [Fact]
    public void ConstructorStoresTheCoverageType()
    {
        CoverageTypeAttribute attribute = new(RequirementCoverageType.Benchmark);

        Assert.Equal(RequirementCoverageType.Benchmark, attribute.CoverageType);
    }

    /// <summary>Ensures multiple coverage classifications can annotate the same method.</summary>
    /// <intent>Protect the multi-coverage annotation pattern used by mixed-evidence tests.</intent>
    /// <scenario>TESTDOCS-COVTYPE-003</scenario>
    /// <behavior>Two coverage-type attributes remain attached in declaration order.</behavior>
    [Fact]
    public void MultipleAttributesCanBeAppliedToOneMethod()
    {
        CoverageTypeAttribute[] attributes = typeof(CoverageTypeAttributeTests)
            .GetMethod(nameof(MethodWithMultipleCoverageTypes), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetCustomAttributes<CoverageTypeAttribute>()
            .ToArray();

        Assert.Equal(2, attributes.Length);
        Assert.Equal(RequirementCoverageType.Positive, attributes[0].CoverageType);
        Assert.Equal(RequirementCoverageType.Negative, attributes[1].CoverageType);
    }

    /// <summary>Ensures the trait discoverer emits the expected xUnit trait key and value.</summary>
    /// <intent>Protect the trait discovery contract used by xUnit filtering.</intent>
    /// <scenario>TESTDOCS-COVTYPE-004</scenario>
    /// <behavior>The discoverer emits a single `CoverageType` trait keyed by the enum value name.</behavior>
    [Fact]
    public void TraitDiscovererMapsTheCoverageTypeToATrait()
    {
        CoverageTypeTraitDiscoverer discoverer = new();
        KeyValuePair<string, string>[] traits = discoverer
            .GetTraits(new AttributeInfoStub(RequirementCoverageType.Fuzz))
            .ToArray();

        Assert.Single(traits);
        Assert.Equal("CoverageType", traits[0].Key);
        Assert.Equal("Fuzz", traits[0].Value);
    }

    [CoverageType(RequirementCoverageType.Positive)]
    [CoverageType(RequirementCoverageType.Negative)]
    private static void MethodWithMultipleCoverageTypes()
    {
    }

    private sealed class AttributeInfoStub : LongLivedMarshalByRefObject, IAttributeInfo
    {
        private readonly object[] constructorArguments;

        public AttributeInfoStub(params object[] constructorArguments)
        {
            this.constructorArguments = constructorArguments;
        }

        public IEnumerable<object> GetConstructorArguments() => constructorArguments;

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string attributeName) => [];

        public T GetNamedArgument<T>(string name) => default!;
    }
}
