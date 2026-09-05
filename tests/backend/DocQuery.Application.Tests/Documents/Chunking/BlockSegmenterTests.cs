using DocQuery.Application.Documents.Chunking;

namespace DocQuery.Application.Tests.Documents.Chunking;

public sealed class BlockSegmenterTests
{
    [Theory]
    [InlineData("•")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("–")]
    [InlineData("◦")]
    public void Bulleted_lines_form_one_list_block(string marker)
    {
        var blocks = Segment(Block($"{marker} first", $"{marker} second", $"{marker} third"));

        var list = Assert.Single(blocks);
        Assert.Equal(BlockKind.List, list.Kind);
        Assert.Null(list.Intro);
        Assert.Equal([$"{marker} first", $"{marker} second", $"{marker} third"], list.Items);
    }

    [Theory]
    [InlineData("1.", "2.", "3.")]
    [InlineData("1)", "2)", "3)")]
    [InlineData("(1)", "(2)", "(3)")]
    [InlineData("a)", "b)", "c)")]
    [InlineData("A.", "B.", "C.")]
    [InlineData("(i)", "(ii)", "(iii)")]
    [InlineData("iv.", "v.", "vi.")]
    public void Enumerated_lines_form_one_list_block(string first, string second, string third)
    {
        var blocks = Segment(Block($"{first} alpha", $"{second} beta", $"{third} gamma"));

        var list = Assert.Single(blocks);
        Assert.Equal(BlockKind.List, list.Kind);
        Assert.Equal(3, list.Items.Count);
    }

    [Fact]
    public void Line_ending_with_colon_before_a_list_becomes_its_intro()
    {
        var blocks = Segment(Block("Ingredients:", "- eggs", "- milk"));

        var list = Assert.Single(blocks);
        Assert.Equal("Ingredients:", list.Intro);
        Assert.Equal(["- eggs", "- milk"], list.Items);
        Assert.Equal("Ingredients:\n- eggs\n- milk", list.Text);
    }

    [Fact]
    public void Intro_in_the_preceding_layout_block_is_glued_to_the_list()
    {
        var blocks = Segment(Block("We will need:"), Block("- a hammer", "- nails"));

        var list = Assert.Single(blocks);
        Assert.Equal("We will need:", list.Intro);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Unmarked_lines_after_an_item_continue_that_item()
    {
        var blocks = Segment(Block("- first item that", "wraps onto a second line", "- second item"));

        var list = Assert.Single(blocks);
        Assert.Equal(["- first item that wraps onto a second line", "- second item"], list.Items);
    }

    [Fact]
    public void Paragraph_lines_are_joined_with_spaces()
    {
        var blocks = Segment(Block("The quick brown fox", "jumps over the lazy dog."));

        var paragraph = Assert.Single(blocks);
        Assert.Equal(BlockKind.Paragraph, paragraph.Kind);
        Assert.Equal("The quick brown fox jumps over the lazy dog.", paragraph.Text);
    }

    [Fact]
    public void Paragraph_followed_by_list_in_one_layout_block_yields_two_semantic_blocks()
    {
        var blocks = Segment(Block("Some introduction sentence.", "- one", "- two"));

        Assert.Collection(
            blocks,
            paragraph => Assert.Equal(BlockKind.Paragraph, paragraph.Kind),
            list =>
            {
                Assert.Equal(BlockKind.List, list.Kind);
                Assert.Null(list.Intro);
            });
    }

    [Fact]
    public void List_followed_by_paragraph_ends_the_list()
    {
        var blocks = Segment(Block("- one", "- two"), Block("Closing remarks follow."));

        Assert.Equal(2, blocks.Count);
        Assert.Equal(BlockKind.List, blocks[0].Kind);
        Assert.Equal(BlockKind.Paragraph, blocks[1].Kind);
    }

    [Theory]
    [InlineData("well-known fact")]
    [InlineData("-5 degrees outside")]
    [InlineData("*emphasis* is not a bullet")]
    [InlineData("1.5 litres of water")]
    public void Marker_like_text_without_a_following_space_is_a_paragraph(string line)
    {
        var blocks = Segment(Block(line));

        Assert.Equal(BlockKind.Paragraph, Assert.Single(blocks).Kind);
    }

    [Fact]
    public void Blank_lines_and_empty_blocks_are_dropped()
    {
        var blocks = Segment(Block("", "   "), Block(), Block("Real content"));

        var paragraph = Assert.Single(blocks);
        Assert.Equal("Real content", paragraph.Text);
    }

    private static IReadOnlyList<SemanticBlock> Segment(params TextBlock[] blocks) => BlockSegmenter.Segment(blocks);

    private static TextBlock Block(params string[] lines) => new(lines);
}
