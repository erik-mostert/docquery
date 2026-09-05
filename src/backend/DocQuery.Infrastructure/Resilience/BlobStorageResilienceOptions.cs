namespace DocQuery.Infrastructure.Resilience;

/// <summary>Bound from <c>Resilience:BlobStorage</c>. Defaults suit Azure Blob Storage throttling behaviour.</summary>
public sealed class BlobStorageResilienceOptions : ResiliencePipelineOptions
{
    public const string SectionName = "Resilience:BlobStorage";
}
