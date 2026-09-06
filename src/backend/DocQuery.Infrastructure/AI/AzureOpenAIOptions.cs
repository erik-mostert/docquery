using System.ComponentModel.DataAnnotations;

namespace DocQuery.Infrastructure.AI;

/// <summary>Bound from the <c>AzureOpenAI</c> configuration section; validated at startup by every host that uses it.</summary>
public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Deployment name of the embedding model. Must produce <c>DocumentChunk.EmbeddingDimensions</c>-sized vectors.</summary>
    [Required]
    [MinLength(1)]
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";

    /// <summary>Deployment name of the chat model used to answer questions (query API only).</summary>
    [Required]
    [MinLength(1)]
    public string ChatDeployment { get; set; } = "gpt-5-mini";
}
