using Amazon.S3;

namespace Incursa.OpenAI.Agents.Storage.S3;

/// <summary>Options for AWS S3-backed agent session storage.</summary>
public sealed class S3AgentSessionStoreOptions
{
    /// <summary>Gets or sets an existing S3 client to use.</summary>
    public IAmazonS3? Client { get; set; }

    /// <summary>Gets or sets the bucket name.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Gets or sets the key prefix used to keep session objects grouped.</summary>
    public string Prefix { get; set; } = "sessions";

    /// <summary>Gets or sets whether the bucket is created on demand.</summary>
    public bool CreateBucketIfMissing { get; set; } = false;

    /// <summary>Gets or sets the core session retention options.</summary>
    public AgentSessionStoreOptions SessionOptions { get; set; } = new();
}
