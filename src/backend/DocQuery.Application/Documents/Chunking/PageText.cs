namespace DocQuery.Application.Documents.Chunking;

/// <summary>Text of one page as layout blocks in reading order, as produced by the PDF extractor.</summary>
public sealed record PageText(int PageNumber, IReadOnlyList<TextBlock> Blocks);

/// <summary>A visually grouped run of lines (a paragraph, a list, a heading) as detected by layout analysis.</summary>
public sealed record TextBlock(IReadOnlyList<string> Lines);
