namespace DocQuery.Application.Documents.Chunking;

public enum BlockKind
{
    Paragraph,
    List,
}

/// <summary>
/// A unit of meaning the chunker tries to keep whole. A paragraph has one item (its text); a list has one item per
/// list entry plus an optional intro line ending with a colon.
/// </summary>
public sealed record SemanticBlock(BlockKind Kind, string? Intro, IReadOnlyList<string> Items)
{
    public static SemanticBlock Paragraph(string text) => new(BlockKind.Paragraph, null, [text]);

    public static SemanticBlock List(string? intro, IReadOnlyList<string> items) => new(BlockKind.List, intro, items);

    /// <summary>The block as it appears in a chunk: paragraphs as-is, lists one entry per line with the intro first.</summary>
    public string Text => Kind == BlockKind.List
        ? string.Join('\n', Intro is null ? Items : Items.Prepend(Intro))
        : Items[0];
}
