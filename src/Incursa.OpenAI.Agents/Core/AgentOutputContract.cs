namespace Incursa.OpenAI.Agents;

/// <summary>
/// Describes the structured output expected from an agent.
/// </summary>

public sealed record AgentOutputContract
{
    /// <summary>Creates a contract using the provided CLR type and optional output name.</summary>
    public AgentOutputContract(Type clrType)
        : this(clrType, null)
    {
    }

    /// <summary>Creates a contract using CLR type and name metadata.</summary>
    public AgentOutputContract(Type clrType, string? name)
    {
        ClrType = clrType;
        Name = name;
    }

    /// <summary>Gets the CLR type expected for the agent output.</summary>
    public Type ClrType { get; init; }

    /// <summary>Gets or sets the named contract used by the model output format.</summary>
    public string? Name { get; init; }

    /// <summary>Creates an output contract using the inferred type name.</summary>
    public static AgentOutputContract For<T>() => new(typeof(T), typeof(T).Name);

    /// <summary>Creates an output contract with an explicit output name.</summary>
    public static AgentOutputContract For<T>(string? name) => new(typeof(T), name ?? typeof(T).Name);
}
