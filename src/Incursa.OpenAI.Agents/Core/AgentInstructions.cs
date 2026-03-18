namespace Incursa.OpenAI.Agents;

/// <summary>
/// Resolves agent instructions from fixed text or async factories.
/// </summary>

public sealed class AgentInstructions<TContext>
{
    private readonly string? text;
    private readonly Func<AgentInstructionContext<TContext>, CancellationToken, ValueTask<string>>? factory;

    private AgentInstructions(string text)
    {
        this.text = text;
    }

    private AgentInstructions(Func<AgentInstructionContext<TContext>, CancellationToken, ValueTask<string>> factory)
    {
        this.factory = factory;
    }

    /// <summary>Gets instruction text via delegate or fixed value.</summary>
    public static AgentInstructions<TContext> FromText(string text) => new(text);

    /// <summary>Gets instructions from an asynchronous factory.</summary>
    public static AgentInstructions<TContext> FromDelegate(Func<AgentInstructionContext<TContext>, CancellationToken, ValueTask<string>> factory) => new(factory);

    /// <summary>Converts plain text into an instructions instance.</summary>
    public static implicit operator AgentInstructions<TContext>(string text) => FromText(text);

    /// <summary>Resolves instruction content without cancellation token.</summary>
    public ValueTask<string?> ResolveAsync(AgentInstructionContext<TContext> context)
        => ResolveAsync(context, CancellationToken.None);

    /// <summary>Resolves instruction content, using factory when provided.</summary>
    public ValueTask<string?> ResolveAsync(AgentInstructionContext<TContext> context, CancellationToken cancellationToken)
    {
        if (factory is not null)
        {
            return ResolveFactoryAsync(context, cancellationToken);
        }

        return ValueTask.FromResult<string?>(text);
    }

    private async ValueTask<string?> ResolveFactoryAsync(AgentInstructionContext<TContext> context, CancellationToken cancellationToken)
        => await factory!(context, cancellationToken).ConfigureAwait(false);
}
