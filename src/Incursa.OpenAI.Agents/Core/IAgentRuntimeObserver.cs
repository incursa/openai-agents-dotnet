namespace Incursa.OpenAI.Agents;

/// <summary>Observes runtime events emitted by the runner.</summary>
public interface IAgentRuntimeObserver
{
    /// <summary>Observes a runtime event.</summary>
    /// <param name="observation">The observation to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken);
}
