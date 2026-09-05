using DocQuery.Application.Documents.Chunking;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;

namespace DocQuery.Application.Tests.Documents.Chunking;

/// <summary>Uses the word-per-token fake tokenizer, so "tokens" below are words.</summary>
public sealed class TextChunkerTests
{
    private static readonly DocumentId DocumentId = DocumentId.Create();
    private readonly FakeTokenizer _tokenizer = new();

    [Fact]
    public void Blocks_that_fit_together_are_packed_into_one_chunk()
    {
        var pages = Page(1, Block("one two three four"), Block("five six seven eight"));

        var chunks = Chunk(pages, maxTokens: 10);

        var chunk = Assert.Single(chunks);
        Assert.Equal("one two three four\n\nfive six seven eight", chunk.Text);
        Assert.Equal(8, chunk.TokenCount);
        Assert.Equal(1, chunk.PageNumber);
    }

    [Fact]
    public void Block_that_would_overflow_starts_a_new_chunk()
    {
        var pages = Page(1, Block("a b c d e f"), Block("g h i j k l"));

        var chunks = Chunk(pages, maxTokens: 10);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("a b c d e f", chunks[0].Text);
        Assert.Equal("g h i j k l", chunks[1].Text);
    }

    [Fact]
    public void A_list_that_fits_in_a_chunk_is_never_split()
    {
        var pages = Page(1, Block("Intro paragraph with five words"), Block("Steps:", "- one two", "- three four", "- five six"));

        var chunks = Chunk(pages, maxTokens: 10);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Intro paragraph with five words", chunks[0].Text);
        Assert.Equal("Steps:\n- one two\n- three four\n- five six", chunks[1].Text);
    }

    [Fact]
    public void Overlap_repeats_the_previous_block_when_it_fits_the_overlap_budget()
    {
        var pages = Page(1, Block("a1 a2 a3"), Block("b1 b2 b3"), Block("c1 c2 c3 c4 c5 c6"));

        var chunks = Chunk(pages, maxTokens: 10, overlapTokens: 3);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("a1 a2 a3\n\nb1 b2 b3", chunks[0].Text);
        Assert.Equal("b1 b2 b3\n\nc1 c2 c3 c4 c5 c6", chunks[1].Text);
    }

    [Fact]
    public void Overlap_is_skipped_when_the_previous_block_exceeds_the_overlap_budget()
    {
        var pages = Page(1, Block("a1 a2 a3 a4 a5"), Block("b1 b2 b3 b4 b5 b6"));

        var chunks = Chunk(pages, maxTokens: 10, overlapTokens: 3);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("b1 b2 b3 b4 b5 b6", chunks[1].Text);
    }

    [Fact]
    public void Chunks_never_span_pages_and_carry_their_page_number()
    {
        var pages = new[] { Page(1, Block("page one"))[0], Page(2, Block("page two"))[0] };

        var chunks = Chunk(pages, maxTokens: 10);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(1, chunks[0].PageNumber);
        Assert.Equal(2, chunks[1].PageNumber);
    }

    [Fact]
    public void Positions_increase_across_the_document()
    {
        var pages = new[] { Page(1, Block("a b c d e f"), Block("g h i j k l"))[0], Page(2, Block("m n"))[0] };

        var chunks = Chunk(pages, maxTokens: 10);

        Assert.Equal([0, 1, 2], chunks.Select(c => c.Position).ToArray());
        Assert.All(chunks, chunk => Assert.Equal(DocumentId, chunk.DocumentId));
    }

    [Fact]
    public void Blank_pages_produce_no_chunks()
    {
        var pages = new[] { new PageText(1, []), Page(2, Block("only page with text"))[0] };

        var chunks = Chunk(pages, maxTokens: 10);

        Assert.Single(chunks);
        Assert.Equal(2, chunks[0].PageNumber);
    }

    [Fact]
    public void Oversized_list_is_split_only_between_items_and_keeps_the_intro_on_the_first_part()
    {
        var pages = Page(1, Block("Rules:", "- one two three", "- four five six", "- seven eight nine", "- ten eleven twelve", "- thirteen fourteen fifteen"));

        // Each item is four tokens including its marker; nine tokens fit the intro plus two items.
        var chunks = Chunk(pages, maxTokens: 9);

        Assert.Equal(3, chunks.Count);
        Assert.Equal("Rules:\n- one two three\n- four five six", chunks[0].Text);
        Assert.Equal("- seven eight nine\n- ten eleven twelve", chunks[1].Text);
        Assert.Equal("- thirteen fourteen fifteen", chunks[2].Text);
        Assert.All(chunks, chunk => Assert.All(chunk.Text.Split('\n').Where(l => l.StartsWith('-')), item => Assert.Equal(3, item.Split(' ').Length - 1)));
    }

    [Fact]
    public void Oversized_paragraph_is_split_at_sentence_ends()
    {
        var pages = Page(1, Block("Alpha beta gamma. Delta epsilon zeta. Eta theta iota."));

        var chunks = Chunk(pages, maxTokens: 6);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Alpha beta gamma. Delta epsilon zeta.", chunks[0].Text);
        Assert.Equal("Eta theta iota.", chunks[1].Text);
    }

    [Fact]
    public void Oversized_sentence_falls_back_to_token_windows_that_preserve_the_original_text()
    {
        const string sentence = "w01 w02 w03 w04 w05 w06 w07 w08 w09 w10 w11 w12";
        var pages = Page(1, Block(sentence));

        var chunks = Chunk(pages, maxTokens: 5);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.InRange(chunk.TokenCount, 1, 5));
        Assert.Equal(sentence, string.Join(' ', chunks.Select(c => c.Text)));
    }

    [Fact]
    public void Token_count_is_recounted_on_the_final_chunk_text()
    {
        var pages = Page(1, Block("Heading:", "- item one", "- item two"), Block("Trailing paragraph here"));

        var chunks = Chunk(pages, maxTokens: 50);

        var chunk = Assert.Single(chunks);
        Assert.Equal(_tokenizer.CountTokens(chunk.Text), chunk.TokenCount);
    }

    private IReadOnlyList<DocumentChunk> Chunk(IReadOnlyList<PageText> pages, int maxTokens, int overlapTokens = 0) =>
        TextChunker.Chunk(pages, _tokenizer, new ChunkingOptions { MaxTokens = maxTokens, OverlapTokens = overlapTokens }, DocumentId);

    private static PageText[] Page(int number, params TextBlock[] blocks) => [new PageText(number, blocks)];

    private static TextBlock Block(params string[] lines) => new(lines);
}
