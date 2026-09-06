using System.ComponentModel.DataAnnotations;

namespace DocQuery.Infrastructure.AI;

/// <summary>Bound from the <c>AzureOpenAI</c> configuration section; validated at startup.</summary>
public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Deployment name of the embedding model. Must produce <c>DocumentChunk.EmbeddingDimensions</c>-sized vectors.</summary>
    [Required]
    [MinLength(1)]
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";
}
