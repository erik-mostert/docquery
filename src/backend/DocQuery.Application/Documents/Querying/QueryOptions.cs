using System.ComponentModel.DataAnnotations;

namespace DocQuery.Application.Documents.Querying;

/// <summary>Bound from the <c>Querying</c> configuration section by the query API; validated at startup.</summary>
public sealed class QueryOptions
{
    public const string SectionName = "Querying";

    /// <summary>Passages retrieved and handed to the chat model when the caller does not choose.</summary>
    [Range(1, 50)]
    public int DefaultTopK { get; set; } = 5;

    /// <summary>Upper bound on the passages a caller may request; more passages cost tokens and dilute the answer.</summary>
    [Range(1, 50)]
    public int MaxTopK { get; set; } = 20;

    /// <summary>Characters of each passage returned in a citation excerpt.</summary>
    [Range(50, 5000)]
    public int ExcerptLength { get; set; } = 300;

    /// <summary>Cap on the answer length in tokens.</summary>
    [Range(1, 32_000)]
    public int MaxAnswerTokens { get; set; } = 800;
}
