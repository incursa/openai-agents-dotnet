using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

internal sealed record OpenAiResponsesTurnPlan<TContext>
{
    internal OpenAiResponsesTurnPlan(
        JsonObject body,
        Agent<TContext> effectiveAgent,
        IReadOnlyDictionary<string, AgentHandoff<TContext>> handoffMap)
    {
        Body = body;
        EffectiveAgent = effectiveAgent;
        HandoffMap = handoffMap;
    }

    internal JsonObject Body { get; init; }

    internal Agent<TContext> EffectiveAgent { get; init; }

    internal IReadOnlyDictionary<string, AgentHandoff<TContext>> HandoffMap { get; init; }
}
