using Incursa.OpenAI.Agents.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Incursa.OpenAI.Agents.Extensions;

/// <summary>
/// Receives runtime observations emitted by the agent runtime.
/// </summary>

public interface IAgentRuntimeObservationSink
{
    /// <summary>Observes runtime events emitted by the agent runtime.</summary>
    ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken);
}

/// <summary>
/// Adds convenience overloads for runtime observation sinks.
/// </summary>

public static class AgentRuntimeObservationSinkExtensions
{
    /// <summary>Observes runtime events without requiring an explicit cancellation token.</summary>
    public static ValueTask ObserveAsync(this IAgentRuntimeObservationSink sink, AgentRuntimeObservation observation)
        => sink.ObserveAsync(observation, CancellationToken.None);
}

internal sealed class CompositeAgentRuntimeObserver : IAgentRuntimeObserver
{
    private readonly IReadOnlyList<IAgentRuntimeObservationSink> sinks;

    /// <summary>
    /// Creates an observer that forwards each observation to every configured sink.
    /// </summary>

    public CompositeAgentRuntimeObserver(IEnumerable<IAgentRuntimeObservationSink> sinks)
    {
        this.sinks = sinks.ToArray();
    }

    /// <summary>
    /// Broadcasts one runtime observation to every registered sink.
    /// </summary>

    public async ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
    {
        foreach (IAgentRuntimeObservationSink sink in sinks)
        {
            await sink.ObserveAsync(observation, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class LoggingAgentRuntimeObservationSink : IAgentRuntimeObservationSink
{
    private readonly ILogger<LoggingAgentRuntimeObservationSink> logger;
    private readonly AgentRuntimeOptions options;

    /// <summary>
    /// Creates a logging sink that writes runtime observations when enabled.
    /// </summary>

    public LoggingAgentRuntimeObservationSink(ILogger<LoggingAgentRuntimeObservationSink> logger, IOptions<AgentRuntimeOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    /// <summary>Records runtime observations to logging based on runtime configuration.</summary>
    public ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
    {
        if (!options.EnableLoggingObserver)
        {
            return ValueTask.CompletedTask;
        }

        LogLevel level = observation.EventName switch
        {
            AgentRuntimeEventNames.RunFailed => LogLevel.Error,
            AgentRuntimeEventNames.GuardrailTriggered or AgentRuntimeEventNames.MaxTurnsExceeded => LogLevel.Warning,
            _ => LogLevel.Information,
        };

        logger.Log(
            level,
            observation.Exception,
            "Agent runtime event {EventName} session={SessionKey} agent={AgentName} turn={TurnNumber} status={Status} tool={ToolName} call={ToolCallId} response={ResponseId} durationMs={DurationMs} detail={Detail}",
            observation.EventName,
            observation.SessionKey,
            observation.AgentName,
            observation.TurnNumber,
            observation.Status,
            observation.ToolName,
            observation.ToolCallId,
            observation.ResponseId,
            observation.Duration?.TotalMilliseconds,
            observation.Detail);

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Receives MCP client observations emitted by transport calls.
/// </summary>

public interface IMcpObservationSink
{
    /// <summary>Observes MCP client events emitted by an MCP transport.</summary>
    ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken);
}

/// <summary>
/// Adds convenience overloads for MCP observation sinks.
/// </summary>

public static class McpObservationSinkExtensions
{
    /// <summary>Observes MCP events without requiring an explicit cancellation token.</summary>
    public static ValueTask ObserveAsync(this IMcpObservationSink sink, McpClientObservation observation)
        => sink.ObserveAsync(observation, CancellationToken.None);
}

internal sealed class CompositeMcpClientObserver : IMcpClientObserver
{
    private readonly IReadOnlyList<IMcpObservationSink> sinks;

    /// <summary>
    /// Creates an observer that forwards each MCP observation to every configured sink.
    /// </summary>

    public CompositeMcpClientObserver(IEnumerable<IMcpObservationSink> sinks)
    {
        this.sinks = sinks.ToArray();
    }

    /// <summary>
    /// Broadcasts one MCP observation to every registered sink.
    /// </summary>

    public async ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken)
    {
        foreach (IMcpObservationSink sink in sinks)
        {
            await sink.ObserveAsync(observation, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class LoggingMcpObservationSink : IMcpObservationSink
{
    private readonly ILogger<LoggingMcpObservationSink> logger;
    private readonly OpenAiResponsesOptions options;

    /// <summary>
    /// Creates a logging sink that writes MCP observations when enabled.
    /// </summary>

    public LoggingMcpObservationSink(ILogger<LoggingMcpObservationSink> logger, IOptions<OpenAiResponsesOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    /// <summary>Records MCP observations to logging based on MCP client options.</summary>
    public ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken)
    {
        if (!options.EnableMcpLoggingObserver)
        {
            return ValueTask.CompletedTask;
        }

        LogLevel level = observation.Outcome switch
        {
            McpCallOutcome.Success => LogLevel.Information,
            McpCallOutcome.RetryScheduled => LogLevel.Warning,
            McpCallOutcome.AuthenticationFailure or McpCallOutcome.ServerFailure or McpCallOutcome.TransportFailure => LogLevel.Error,
            _ => LogLevel.Information,
        };

        logger.Log(
            level,
            "MCP {Method} server={ServerLabel} tool={ToolName} attempt={Attempt} outcome={Outcome} status={StatusCode} durationMs={DurationMs} detail={Detail}",
            observation.Method,
            observation.ServerLabel,
            observation.ToolName,
            observation.Attempt,
            observation.Outcome,
            observation.StatusCode,
            observation.Duration.TotalMilliseconds,
            observation.Detail);

        return ValueTask.CompletedTask;
    }
}
