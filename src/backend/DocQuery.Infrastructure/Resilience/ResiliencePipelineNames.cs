namespace DocQuery.Infrastructure.Resilience;

/// <summary>Names of the Polly pipelines registered with <c>AddResiliencePipeline</c>. One per outbound dependency (ADR 0007).</summary>
public static class ResiliencePipelineNames
{
    public const string BlobStorage = "blob-storage";
}
