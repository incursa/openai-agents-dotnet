namespace Incursa.OpenAI.Agents;

/// <summary>Default no-op observer implementation.</summary>
public sealed class NullAgentRuntimeObserver : IAgentRuntimeObserver
{
    /// <summary>Observes a runtime event without cancellation.</summary>
    /// <param name="observation">The observation to process.</param>
    public ValueTask ObserveAsync(AgentRuntimeObservation observation)
        => ObserveAsync(observation, CancellationToken.None);

    /// <summary>Observes a runtime event.</summary>
    /// <param name="observation">The observation to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
