using System.ComponentModel.DataAnnotations;

namespace DocQuery.Application.Documents.Embedding;

/// <summary>Bound from the <c>Embedding</c> configuration section by the embedding worker; validated at startup.</summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>Chunks sent to the embedding model per request. Azure OpenAI accepts up to 2048 inputs per call.</summary>
    [Range(1, 2048)]
    public int BatchSize { get; set; } = 16;
}
