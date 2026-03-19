#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using OpenAI;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

internal static class OpenAiSdkSerialization
{
    internal static T ReadModel<T>(JsonNode node)
        => ModelReaderWriter.Read<T>(BinaryData.FromString(node.ToJsonString()))!;

    internal static JsonObject ToJsonObject<T>(T value)
        => JsonNode.Parse(ModelReaderWriter.Write(value).ToString()) as JsonObject ?? new JsonObject();
}
