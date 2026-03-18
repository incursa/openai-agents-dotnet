namespace Incursa.OpenAI.Agents;

/// <summary>Extension members for <see cref="IAgentRuntimeObserver"/>.</summary>
public static class AgentRuntimeObserverExtensions
{
    /// <summary>Observes a runtime event without cancellation.</summary>
    /// <param name="observer">The observer to invoke.</param>
    /// <param name="observation">The observation to process.</param>
    public static ValueTask ObserveAsync(this IAgentRuntimeObserver observer, AgentRuntimeObservation observation)
        => observer.ObserveAsync(observation, CancellationToken.None);
}
