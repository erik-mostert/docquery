using DocQuery.Infrastructure.Text;

namespace DocQuery.Infrastructure.Tests.Text;

public sealed class O200kTokenizerTests
{
    private static readonly O200kTokenizer Tokenizer = new();

    [Fact]
    public void Counts_tokens_for_ordinary_text()
    {
        var count = Tokenizer.CountTokens("The quick brown fox jumps over the lazy dog.");

        Assert.InRange(count, 8, 12);
    }

    [Fact]
    public void Token_spans_tile_the_text_without_gaps_or_overlap()
    {
        const string text = "Résumé: naïve café — 12 items, 3.5 kg; 日本語 too.";

        var spans = Tokenizer.EncodeToTokens(text);

        Assert.NotEmpty(spans);
        Assert.Equal(0, spans[0].Start);
        Assert.Equal(text.Length, spans[^1].End);
        for (var i = 1; i < spans.Count; i++)
        {
            Assert.Equal(spans[i - 1].End, spans[i].Start);
        }

        Assert.Equal(text, string.Concat(spans.Select(span => text[span.Start..span.End])));
    }

    [Fact]
    public void Count_matches_span_count()
    {
        const string text = "Chunking by tokens keeps every chunk under the embedding limit.";

        Assert.Equal(Tokenizer.EncodeToTokens(text).Count, Tokenizer.CountTokens(text));
    }
}
