namespace DocQuery.Infrastructure.Storage;

/// <summary>Bound from the <c>BlobStorage</c> configuration section.</summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>Create the container at startup if missing. On for local runs; provisioned separately in Azure.</summary>
    public bool CreateContainerOnStartup { get; set; } = true;
}
