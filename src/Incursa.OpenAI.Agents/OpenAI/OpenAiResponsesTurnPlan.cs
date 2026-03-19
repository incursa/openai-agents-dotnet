#pragma warning disable OPENAI001

using OpenAI.Responses;

namespace Incursa.OpenAI.Agents;

internal sealed record OpenAiResponsesTurnPlan<TContext>
{
    internal OpenAiResponsesTurnPlan(
        CreateResponseOptions options,
        Agent<TContext> effectiveAgent,
        IReadOnlyDictionary<string, AgentHandoff<TContext>> handoffMap)
    {
        Options = options;
        EffectiveAgent = effectiveAgent;
        HandoffMap = handoffMap;
    }

    internal CreateResponseOptions Options { get; init; }

    internal Agent<TContext> EffectiveAgent { get; init; }

    internal IReadOnlyDictionary<string, AgentHandoff<TContext>> HandoffMap { get; init; }
}
