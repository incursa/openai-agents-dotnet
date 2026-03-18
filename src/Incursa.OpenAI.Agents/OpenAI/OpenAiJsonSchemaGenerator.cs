using System.Reflection;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

internal static class OpenAiJsonSchemaGenerator
{
    /// <summary>
    /// Builds a schema from a CLR type using reflection and cycle detection.
    /// </summary>

    public static JsonNode CreateSchema(Type type) => CreateSchema(type, new HashSet<Type>());

    private static JsonNode CreateSchema(Type type, ISet<Type> visited)
    {
        // Guard against recursive type graphs by short-circuiting back-edges to a generic object.
        if (!visited.Add(type))
        {
            return new JsonObject { ["type"] = "object" };
        }

        // Map CLR scalar types to their JSON Schema primitive counterparts.
        if (type == typeof(string) || type == typeof(char) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (type == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (type.IsEnum)
        {
            return new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(type.GetEnumNames().Select(name => (JsonNode)name).ToArray()),
            };
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return new JsonObject { ["type"] = "number" };
        }

        if (type.IsArray)
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = CreateSchema(type.GetElementType()!, visited),
            };
        }

        // Support IEnumerable<T> collections as JSON arrays.
        if (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = CreateSchema(type.GetGenericArguments()[0], visited),
            };
        }

        // Reflect public instance properties into JSON object schema, marking non-nullable properties required.
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            properties[property.Name] = CreateSchema(property.PropertyType, visited);
            if (!IsNullable(property.PropertyType))
            {
                required.Add(property.Name);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static bool IsNullable(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
}
