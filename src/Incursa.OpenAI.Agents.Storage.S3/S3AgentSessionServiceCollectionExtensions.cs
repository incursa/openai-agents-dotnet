using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Incursa.OpenAI.Agents.Storage.S3;

/// <summary>DI helpers for AWS S3-backed agent session storage.</summary>
public static class S3AgentSessionServiceCollectionExtensions
{
    /// <summary>Registers AWS S3-backed session storage.</summary>
    public static IServiceCollection AddS3AgentSessions(
        this IServiceCollection services,
        Action<S3AgentSessionStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<S3AgentSessionStoreOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.Replace(ServiceDescriptor.Singleton<IAmazonS3>(sp =>
        {
            S3AgentSessionStoreOptions options = sp.GetRequiredService<IOptions<S3AgentSessionStoreOptions>>().Value;
            // The adapter owns client creation only when callers did not supply one,
            // which keeps test and production wiring flexible.
            return options.Client ?? new AmazonS3Client();
        }));

        services.Replace(ServiceDescriptor.Singleton<S3AgentSessionStore>(sp =>
            new S3AgentSessionStore(
                sp.GetRequiredService<IAmazonS3>(),
                sp.GetRequiredService<IOptions<S3AgentSessionStoreOptions>>().Value)));
        services.Replace(ServiceDescriptor.Singleton<IVersionedAgentSessionStore>(sp => sp.GetRequiredService<S3AgentSessionStore>()));
        services.Replace(ServiceDescriptor.Singleton<IAgentSessionStore>(sp => sp.GetRequiredService<S3AgentSessionStore>()));

        return services;
    }
}
