using Azure.Storage.Blobs;

namespace Incursa.OpenAI.Agents.Storage.Azure;

/// <summary>Options for Azure-backed agent session storage.</summary>
public sealed class AzureAgentSessionStoreOptions
{
    /// <summary>Gets or sets an existing container client to use.</summary>
    public BlobContainerClient? ContainerClient { get; set; }

    /// <summary>Gets or sets the Azure Storage connection string used to create a container client.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets the blob container name.</summary>
    public string ContainerName { get; set; } = "agent-sessions";

    /// <summary>Gets or sets the blob name prefix used to keep session objects grouped.</summary>
    public string Prefix { get; set; } = "sessions";

    /// <summary>Gets or sets whether the container is created on demand.</summary>
    public bool CreateContainerIfMissing { get; set; } = true;

    /// <summary>Gets or sets the core session retention options.</summary>
    public AgentSessionStoreOptions SessionOptions { get; set; } = new();
}
