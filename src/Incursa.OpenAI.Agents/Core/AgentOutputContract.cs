using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Describes the structured output expected from an agent.
/// </summary>

public sealed record AgentOutputContract
{
    /// <summary>Creates a contract using an explicit JSON schema.</summary>
    public AgentOutputContract(JsonNode schema)
        : this(schema, null, null)
    {
    }

    /// <summary>Creates a contract using an explicit JSON schema and optional output name.</summary>
    public AgentOutputContract(JsonNode schema, string? name)
        : this(schema, name, null)
    {
    }

    /// <summary>Creates a contract using an explicit JSON schema, optional output name, and optional CLR type metadata.</summary>
    public AgentOutputContract(JsonNode schema, string? name, Type? clrType)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema is not JsonObject)
        {
            throw new ArgumentException("Output schema root must be a JSON object.", nameof(schema));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = clrType?.Name;
        }

        Schema = schema.DeepClone();
        ClrType = clrType;
        Name = name;
    }

    /// <summary>Gets the JSON schema sent to the model for structured output.</summary>
    public JsonNode Schema { get; init; }

    /// <summary>Gets the CLR type associated with the structured output, if any.</summary>
    public Type? ClrType { get; init; }

    /// <summary>Gets or sets the named contract used by the model output format.</summary>
    public string? Name { get; init; }
}
