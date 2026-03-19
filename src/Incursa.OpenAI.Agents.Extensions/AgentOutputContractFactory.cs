using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Incursa.OpenAI.Agents.Extensions;

/// <summary>
/// Provides optional helpers for creating explicit structured-output contracts from CLR types.
/// </summary>
public static class AgentOutputContractFactory
{
    private static readonly AIJsonSchemaTransformOptions StrictTransformOptions = new()
    {
        DisallowAdditionalProperties = true,
        RequireAllProperties = true,
        UseNullableKeyword = false,
        ConvertBooleanSchemas = true,
        MoveDefaultKeywordToDescription = true,
    };

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for <typeparamref name="T"/>.
    /// </summary>
    public static AgentOutputContract ForJsonSchema<T>()
        => ForJsonSchema(typeof(T), null, null, null);

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for <typeparamref name="T"/> with an explicit output name.
    /// </summary>
    public static AgentOutputContract ForJsonSchema<T>(string? name)
        => ForJsonSchema(typeof(T), null, name, null);

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for <typeparamref name="T"/>.
    /// </summary>
    public static AgentOutputContract ForJsonSchema<T>(JsonSerializerOptions serializerOptions)
        => ForJsonSchema(typeof(T), serializerOptions, null, null);

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for <typeparamref name="T"/>.
    /// </summary>
    public static AgentOutputContract ForJsonSchema<T>(
        JsonSerializerOptions serializerOptions,
        string? name,
        string? description)
        => ForJsonSchema(typeof(T), serializerOptions, name, description);

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for the provided CLR type.
    /// </summary>
    public static AgentOutputContract ForJsonSchema(Type type)
        => ForJsonSchema(type, null, null, null);

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for the provided CLR type with an explicit output name.
    /// </summary>
    public static AgentOutputContract ForJsonSchema(Type type, string? name)
        => ForJsonSchema(type, null, name, null);

    /// <summary>
    /// Creates an explicit output contract by generating a JSON schema for the provided CLR type.
    /// </summary>
    public static AgentOutputContract ForJsonSchema(
        Type type,
        JsonSerializerOptions? serializerOptions,
        string? name,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(type);

        ChatResponseFormatJson format = (ChatResponseFormatJson)ChatResponseFormat.ForJsonSchema(
            type,
            serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web),
            name,
            description);

        JsonElement schemaElement = format.Schema
            ?? throw new InvalidOperationException($"No JSON schema was generated for output type '{type.FullName}'.");

        JsonElement strictSchema = AIJsonUtilities.TransformSchema(schemaElement, StrictTransformOptions);

        JsonNode schema = JsonNode.Parse(strictSchema.GetRawText())
            ?? throw new InvalidOperationException($"Generated JSON schema for output type '{type.FullName}' was empty.");

        return new AgentOutputContract(schema, format.SchemaName ?? name ?? type.Name, type);
    }
}
