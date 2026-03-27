namespace Nop.Core.Configuration;

/// <summary>
/// Represents Azure Blob Storage configuration parameters for Data Protection
/// </summary>
public partial class AzureBlobStorageDataProtectionConfig : IConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether we should use Azure Blob Storage for Data Protection keys
    /// </summary>
    public bool Enabled { get; protected set; } = false;

    /// <summary>
    /// Gets or sets connection string
    /// </summary>
    public string ConnectionString { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets or sets container name
    /// </summary>
    public string ContainerName { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets or sets blob name
    /// </summary>
    public string BlobName { get; protected set; } = string.Empty;
}
