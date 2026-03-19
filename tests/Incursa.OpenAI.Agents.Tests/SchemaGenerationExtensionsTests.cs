using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for optional structured-output schema generation helpers in the extensions package.</summary>
public sealed class SchemaGenerationExtensionsTests
{
    /// <summary>Generated contracts from CLR types produce explicit strict object schemas for the core runtime.</summary>
    /// <intent>Protect the optional extensions-based convenience path after removing schema generation from the core package.</intent>
    /// <scenario>LIB-EXT-SCHEMA-001</scenario>
    /// <behavior>Generated contracts keep CLR type metadata and produce strict root and nested object schemas.</behavior>
    [Fact]
    public void ForJsonSchema_GenericCreatesStrictExplicitContract()
    {
        AgentOutputContract contract = AgentOutputContractFactory.ForJsonSchema<GuardrailDecision>();

        JsonObject schema = Assert.IsType<JsonObject>(contract.Schema);
        JsonObject properties = Assert.IsType<JsonObject>(schema["properties"]);
        JsonObject reason = Assert.IsType<JsonObject>(properties["reason"]);

        Assert.Equal(typeof(GuardrailDecision), contract.ClrType);
        Assert.Equal(nameof(GuardrailDecision), contract.Name);
        Assert.Equal("object", schema["type"]?.GetValue<string>());
        Assert.False(schema["additionalProperties"]?.GetValue<bool>() ?? true);
        Assert.False(reason["additionalProperties"]?.GetValue<bool>() ?? true);
        Assert.Contains("isJailbreak", Assert.IsType<JsonArray>(schema["required"]).Select(node => node?.GetValue<string>()));
    }

    /// <summary>Type-based generation honors explicit schema names on the resulting output contract.</summary>
    /// <intent>Protect the naming surface used by the OpenAI response format.</intent>
    /// <scenario>LIB-EXT-SCHEMA-002</scenario>
    /// <behavior>Explicit names override the default CLR type name.</behavior>
    [Fact]
    public void ForJsonSchema_TypeOverloadUsesExplicitName()
    {
        AgentOutputContract contract = AgentOutputContractFactory.ForJsonSchema(typeof(GuardrailDecision), name: "guardrail_result");

        Assert.Equal("guardrail_result", contract.Name);
        Assert.Equal(typeof(GuardrailDecision), contract.ClrType);
    }

    private sealed record GuardrailDecision(bool IsJailbreak, ReasonDetails Reason);

    private sealed record ReasonDetails(string Category, string Explanation);
}
