using System.Net.Http.Headers;
using Incursa.OpenAI.Agents.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Incursa.OpenAI.Agents.Extensions;

/// <summary>
/// Registers agent runtime services in IServiceCollection.
/// </summary>

public static class AgentServiceCollectionExtensions
{
    /// <summary>Registers in-memory session store and baseline agent services.</summary>
    public static IServiceCollection AddIncursaAgents(this IServiceCollection services)
        => AddIncursaAgents(services, null);

    /// <summary>Registers in-memory session store and baseline agent services with optional runtime configuration.</summary>
    public static IServiceCollection AddIncursaAgents(
        this IServiceCollection services,
        Action<AgentRuntimeOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AgentRuntimeOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentRuntimeObservationSink, LoggingAgentRuntimeObservationSink>());
        services.TryAddSingleton<IAgentRuntimeObserver, CompositeAgentRuntimeObserver>();

        services.TryAddSingleton<IAgentApprovalService, AllowAllAgentApprovalService>();
        services.TryAddSingleton<InMemoryAgentSessionStore>();
        services.TryAddSingleton<IAgentSessionStore>(sp => sp.GetRequiredService<InMemoryAgentSessionStore>());
        services.TryAddSingleton<IVersionedAgentSessionStore>(sp => sp.GetRequiredService<InMemoryAgentSessionStore>());

        services.TryAddTransient(sp => new AgentRunner(
            sp.GetRequiredService<IAgentSessionStore>(),
            sp.GetRequiredService<IAgentApprovalService>(),
            sp.GetRequiredService<IAgentRuntimeObserver>()));

        return services;
    }

    /// <summary>Replaces session storage with file-backed persistence.</summary>
    public static IServiceCollection AddFileAgentSessions(
        this IServiceCollection services,
        string directoryPath)
        => AddFileAgentSessions(services, directoryPath, null);

    /// <summary>Replaces session storage with file-backed persistence with optional retention configuration.</summary>
    public static IServiceCollection AddFileAgentSessions(
        this IServiceCollection services,
        string directoryPath,
        Action<AgentSessionRetentionOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        services.AddOptions<AgentSessionRetentionOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.Replace(ServiceDescriptor.Singleton<FileAgentSessionStore>(sp =>
            new(directoryPath, sp.GetRequiredService<IOptions<AgentSessionRetentionOptions>>().Value.ToStoreOptions())));
        services.Replace(ServiceDescriptor.Singleton<IAgentSessionStore>(sp => sp.GetRequiredService<FileAgentSessionStore>()));
        services.Replace(ServiceDescriptor.Singleton<IVersionedAgentSessionStore>(sp => sp.GetRequiredService<FileAgentSessionStore>()));

        return services;
    }

    /// <summary>Registers OpenAI responses client/runner integration using default configuration.</summary>
    public static IServiceCollection AddOpenAiResponses(this IServiceCollection services)
        => AddOpenAiResponses(services, null);

    /// <summary>Registers OpenAI responses client/runner integration with optional configuration.</summary>
    public static IServiceCollection AddOpenAiResponses(
        this IServiceCollection services,
        Action<OpenAiResponsesOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OpenAiResponsesOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMcpObservationSink, LoggingMcpObservationSink>());
        services.TryAddSingleton<IMcpClientObserver, CompositeMcpClientObserver>();

        services.AddHttpClient(OpenAiResponsesDefaults.OpenAiHttpClientName)
            .ConfigureHttpClient((sp, client) => ConfigureOpenAiClient(sp, client));
        services.AddHttpClient(OpenAiResponsesDefaults.McpHttpClientName);

        services.TryAddTransient<IOpenAiResponsesClient>(sp =>
        {
            OpenAiResponsesOptions options = sp.GetRequiredService<IOptions<OpenAiResponsesOptions>>().Value;
            HttpClient client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(options.HttpClientName);
            ApplyOpenAiOptions(client, options);
            return new OpenAiResponsesClient(
                client,
                options.ResponsesPath);
        });

        services.TryAddSingleton(sp =>
        {
            OpenAiResponsesOptions options = sp.GetRequiredService<IOptions<OpenAiResponsesOptions>>().Value;
            return options.ToMcpClientOptions(sp.GetService<IMcpClientObserver>());
        });

        services.TryAddTransient(sp =>
        {
            OpenAiResponsesOptions options = sp.GetRequiredService<IOptions<OpenAiResponsesOptions>>().Value;
            return new OpenAiResponsesRunner(
                sp.GetRequiredService<IOpenAiResponsesClient>(),
                sp.GetRequiredService<IAgentSessionStore>(),
                sp.GetRequiredService<IAgentApprovalService>(),
                sp.GetService<IUserScopedMcpAuthResolver>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(options.McpHttpClientName),
                sp.GetService<IMcpToolMetadataResolver>(),
                sp.GetRequiredService<McpClientOptions>(),
                sp.GetRequiredService<IAgentRuntimeObserver>());
        });

        return services;
    }

    private static void ConfigureOpenAiClient(IServiceProvider services, HttpClient client)
        => ApplyOpenAiOptions(client, services.GetRequiredService<IOptions<OpenAiResponsesOptions>>().Value);

    private static void ApplyOpenAiOptions(HttpClient client, OpenAiResponsesOptions options)
    {
        if (options.BaseAddress is not null)
        {
            client.BaseAddress = options.BaseAddress;
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    private static class OpenAiResponsesDefaults
    {
        public const string OpenAiHttpClientName = "openai";
        public const string McpHttpClientName = "incursa-agents-mcp";
    }
}
