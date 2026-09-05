using System.Text.RegularExpressions;
using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Chunking;

/// <summary>
/// Packs whole semantic blocks into chunks of at most <see cref="ChunkingOptions.MaxTokens"/> tokens. A block that
/// does not fit starts a new chunk; overlap repeats the previous block when it fits the overlap budget. Only a block
/// that is itself larger than a chunk is split, and then along its own structure: a list between items, a paragraph
/// between sentences, and as a last resort on token boundaries by character offset (ADR 0015).
/// </summary>
public static partial class TextChunker
{
    private const string BlockSeparator = "\n\n";

    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBoundary();

    public static IReadOnlyList<DocumentChunk> Chunk(IReadOnlyList<PageText> pages, ITokenizer tokenizer, ChunkingOptions options, DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(options);

        var chunks = new List<DocumentChunk>();

        foreach (var page in pages)
        {
            var pieces = BlockSegmenter.Segment(page.Blocks).SelectMany(block => Fit(block, tokenizer, options.MaxTokens));

            foreach (var text in Pack(pieces, options.MaxTokens, options.OverlapTokens))
            {
                chunks.Add(DocumentChunk.Create(documentId, chunks.Count, page.PageNumber, text, tokenizer.CountTokens(text)));
            }
        }

        return chunks;
    }

    /// <summary>Greedy packing of pieces (each already ≤ max tokens) into chunk texts.</summary>
    private static IEnumerable<string> Pack(IEnumerable<Piece> pieces, int maxTokens, int overlapTokens)
    {
        var current = new List<Piece>();
        var currentTokens = 0;
        var currentIsOnlyOverlap = false;

        foreach (var piece in pieces)
        {
            if (current.Count > 0 && currentTokens + piece.Tokens > maxTokens)
            {
                if (currentIsOnlyOverlap)
                {
                    // The carried-over block plus this one does not fit; drop the overlap rather than emit it alone.
                    current.Clear();
                    currentTokens = 0;
                }
                else
                {
                    yield return Join(current);

                    var last = current[^1];
                    current.Clear();
                    currentTokens = 0;
                    if (overlapTokens > 0 && last.Tokens <= overlapTokens)
                    {
                        current.Add(last);
                        currentTokens = last.Tokens;
                        currentIsOnlyOverlap = true;
                    }
                }
            }

            current.Add(piece);
            currentTokens += piece.Tokens;
            currentIsOnlyOverlap = false;
        }

        if (current.Count > 0)
        {
            yield return Join(current);
        }
    }

    private static string Join(List<Piece> pieces) => string.Join(BlockSeparator, pieces.Select(piece => piece.Text));

    /// <summary>A block that fits is one piece; an oversized one is split along its structure.</summary>
    private static IEnumerable<Piece> Fit(SemanticBlock block, ITokenizer tokenizer, int maxTokens)
    {
        var text = block.Text;
        var tokens = tokenizer.CountTokens(text);
        if (tokens <= maxTokens)
        {
            return [new Piece(text, tokens)];
        }

        return block.Kind == BlockKind.List
            ? SplitList(block, tokenizer, maxTokens)
            : SplitSentences(text, tokenizer, maxTokens);
    }

    /// <summary>Splits between list items; the intro stays with the first group. An item too large on its own is split as prose.</summary>
    private static IEnumerable<Piece> SplitList(SemanticBlock list, ITokenizer tokenizer, int maxTokens)
    {
        var entries = list.Intro is null ? list.Items : list.Items.Prepend(list.Intro);
        return PackUnits(entries, '\n', tokenizer, maxTokens, unit => SplitSentences(unit, tokenizer, maxTokens));
    }

    /// <summary>Splits between sentences. A sentence too large on its own falls back to token windows.</summary>
    private static IEnumerable<Piece> SplitSentences(string text, ITokenizer tokenizer, int maxTokens)
    {
        var sentences = SentenceBoundary().Split(text).Where(sentence => sentence.Length > 0).ToList();
        return PackUnits(sentences, ' ', tokenizer, maxTokens, sentence => TokenWindows(sentence, tokenizer, maxTokens));
    }

    /// <summary>Greedily joins units up to the token limit; a unit that exceeds the limit alone is handed to <paramref name="splitOversized"/>.</summary>
    private static IEnumerable<Piece> PackUnits(
        IEnumerable<string> units,
        char separator,
        ITokenizer tokenizer,
        int maxTokens,
        Func<string, IEnumerable<Piece>> splitOversized)
    {
        var current = new List<string>();
        var currentTokens = 0;

        foreach (var unit in units)
        {
            var unitTokens = tokenizer.CountTokens(unit);

            if (unitTokens > maxTokens)
            {
                if (current.Count > 0)
                {
                    yield return new Piece(string.Join(separator, current), currentTokens);
                    current.Clear();
                    currentTokens = 0;
                }

                foreach (var piece in splitOversized(unit))
                {
                    yield return piece;
                }

                continue;
            }

            if (current.Count > 0 && currentTokens + unitTokens > maxTokens)
            {
                yield return new Piece(string.Join(separator, current), currentTokens);
                current.Clear();
                currentTokens = 0;
            }

            current.Add(unit);
            currentTokens += unitTokens;
        }

        if (current.Count > 0)
        {
            yield return new Piece(string.Join(separator, current), currentTokens);
        }
    }

    /// <summary>Last resort: fixed token windows, sliced from the original text by token offsets so no character is altered.</summary>
    private static IEnumerable<Piece> TokenWindows(string text, ITokenizer tokenizer, int maxTokens)
    {
        var spans = tokenizer.EncodeToTokens(text);

        for (var start = 0; start < spans.Count; start += maxTokens)
        {
            var end = Math.Min(start + maxTokens, spans.Count);
            var slice = text[spans[start].Start..spans[end - 1].End].Trim();
            if (slice.Length > 0)
            {
                yield return new Piece(slice, end - start);
            }
        }
    }

    private sealed record Piece(string Text, int Tokens);
}
