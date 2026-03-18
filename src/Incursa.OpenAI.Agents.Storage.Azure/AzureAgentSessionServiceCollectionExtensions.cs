using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Incursa.OpenAI.Agents.Storage.Azure;

/// <summary>DI helpers for Azure-backed agent session storage.</summary>
public static class AzureAgentSessionServiceCollectionExtensions
{
    /// <summary>Registers Azure Blob-backed session storage.</summary>
    public static IServiceCollection AddAzureAgentSessions(
        this IServiceCollection services,
        Action<AzureAgentSessionStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AzureAgentSessionStoreOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.Replace(ServiceDescriptor.Singleton<AzureAgentSessionStore>(sp =>
            new AzureAgentSessionStore(
                ResolveContainerClient(sp),
                sp.GetRequiredService<IOptions<AzureAgentSessionStoreOptions>>().Value)));
        services.Replace(ServiceDescriptor.Singleton<IVersionedAgentSessionStore>(sp => sp.GetRequiredService<AzureAgentSessionStore>()));
        services.Replace(ServiceDescriptor.Singleton<IAgentSessionStore>(sp => sp.GetRequiredService<AzureAgentSessionStore>()));

        return services;
    }

    private static BlobContainerClient ResolveContainerClient(IServiceProvider services)
    {
        AzureAgentSessionStoreOptions options = services.GetRequiredService<IOptions<AzureAgentSessionStoreOptions>>().Value;
        if (options.ContainerClient is not null)
        {
            return options.ContainerClient;
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            // The adapter can bootstrap itself from a connection string when callers do
            // not want to prebuild a container client in application startup.
            return new BlobContainerClient(options.ConnectionString, options.ContainerName);
        }

        throw new InvalidOperationException("Azure agent sessions require either a BlobContainerClient or a connection string.");
    }
}
