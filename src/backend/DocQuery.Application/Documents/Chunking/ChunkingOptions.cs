using System.ComponentModel.DataAnnotations;

namespace DocQuery.Application.Documents.Chunking;

/// <summary>Bound from the <c>Chunking</c> configuration section by consumer hosts; validated at startup.</summary>
public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    /// <summary>Upper bound of tokens per chunk. Embedding models accept 8191; ~500 balances context and precision.</summary>
    [Range(50, 4000)]
    public int MaxTokens { get; set; } = 500;

    /// <summary>
    /// Budget for repeating the previous block at the start of the next chunk, so context is not lost at a boundary.
    /// Must be smaller than <see cref="MaxTokens"/>.
    /// </summary>
    [Range(0, 3999)]
    public int OverlapTokens { get; set; } = 50;
}
