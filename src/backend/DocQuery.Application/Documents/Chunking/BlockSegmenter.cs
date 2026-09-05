using System.Text.RegularExpressions;

namespace DocQuery.Application.Documents.Chunking;

/// <summary>
/// Turns layout blocks into semantic blocks: runs of list items (with their intro line and wrapped continuation
/// lines) become one list block; everything else becomes a paragraph. This is a heuristic over leading characters,
/// because PDF carries no structural markup (ADR 0015).
/// </summary>
public static partial class BlockSegmenter
{
    // Bullets; "1." "1)" "(1)"; "a." "a)" "(a)"; roman numerals "ii." "(iv)". A marker must be followed by whitespace
    // and more text, so "-5", "1.5", "*emphasis*" and "well-known" are not list items.
    [GeneratedRegex(@"^\s*(?:[•‣◦▪▫·*\-–—]|\(?\d{1,3}[.)]|\(?(?:[A-Za-z]|i{1,3}|iv|vi{0,3}|ix|xi{0,3})[.)])\s+\S", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListItemPattern();

    public static IReadOnlyList<SemanticBlock> Segment(IReadOnlyList<TextBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var result = new List<SemanticBlock>();

        foreach (var block in blocks)
        {
            SegmentBlock(block, result);
        }

        return result;
    }

    private static void SegmentBlock(TextBlock block, List<SemanticBlock> result)
    {
        var paragraph = new List<string>();
        var items = new List<string>();

        foreach (var rawLine in block.Lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (IsListItem(line))
            {
                if (items.Count == 0)
                {
                    FlushParagraph(paragraph, result);
                }

                items.Add(line);
            }
            else if (items.Count > 0)
            {
                // An unmarked line inside a list run is the previous item wrapping onto another line.
                items[^1] = items[^1] + " " + line;
            }
            else
            {
                paragraph.Add(line);
            }
        }

        FlushList(items, result);
        FlushParagraph(paragraph, result);
    }

    private static void FlushParagraph(List<string> lines, List<SemanticBlock> result)
    {
        if (lines.Count == 0)
        {
            return;
        }

        result.Add(SemanticBlock.Paragraph(string.Join(' ', lines)));
        lines.Clear();
    }

    private static void FlushList(List<string> items, List<SemanticBlock> result)
    {
        if (items.Count == 0)
        {
            return;
        }

        // A short paragraph ending with a colon right before the list is its intro; keep them together.
        string? intro = null;
        if (result.Count > 0 && result[^1] is { Kind: BlockKind.Paragraph } previous && previous.Text.EndsWith(':'))
        {
            intro = previous.Text;
            result.RemoveAt(result.Count - 1);
        }

        result.Add(SemanticBlock.List(intro, items.ToArray()));
        items.Clear();
    }

    private static bool IsListItem(string line) => ListItemPattern().IsMatch(line);
}
