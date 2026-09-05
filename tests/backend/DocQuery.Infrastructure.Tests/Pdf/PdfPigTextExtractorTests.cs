using DocQuery.Application.Exceptions;
using DocQuery.Infrastructure.Pdf;
using static DocQuery.Infrastructure.Tests.Pdf.PdfBuilder;

namespace DocQuery.Infrastructure.Tests.Pdf;

public sealed class PdfPigTextExtractorTests
{
    private readonly PdfPigTextExtractor _extractor = new();

    [Fact]
    public void Extracts_pages_in_order_with_their_lines()
    {
        using var pdf = new MemoryStream(Build(
            [new Line(72, 720, "First page paragraph line one."), new Line(72, 706, "First page paragraph line two.")],
            [new Line(72, 720, "Second page text.")]));

        var pages = _extractor.Extract(pdf);

        Assert.Equal(2, pages.Count);
        Assert.Equal(1, pages[0].PageNumber);
        Assert.Equal(2, pages[1].PageNumber);
        var firstPageText = string.Join(' ', pages[0].Blocks.SelectMany(b => b.Lines));
        Assert.Contains("line one.", firstPageText, StringComparison.Ordinal);
        Assert.Contains("line two.", firstPageText, StringComparison.Ordinal);
        Assert.Contains("Second page text.", string.Join(' ', pages[1].Blocks.SelectMany(b => b.Lines)), StringComparison.Ordinal);
    }

    [Fact]
    public void List_lines_that_are_laid_out_together_arrive_in_one_block_in_order()
    {
        using var pdf = new MemoryStream(Build(
        [
            new Line(72, 720, "Introduction paragraph above the list."),
            new Line(72, 706, "It has a second line."),
            new Line(72, 640, "- first item"),
            new Line(72, 626, "- second item"),
            new Line(72, 612, "- third item"),
        ]));

        var pages = _extractor.Extract(pdf);

        var page = Assert.Single(pages);
        var listBlock = Assert.Single(page.Blocks, block => block.Lines.Any(line => line.Contains("first item", StringComparison.Ordinal)));
        var listLines = listBlock.Lines.Where(line => line.StartsWith('-')).ToArray();
        Assert.Equal(["- first item", "- second item", "- third item"], listLines);
    }

    [Fact]
    public void Blank_page_yields_no_blocks()
    {
        using var pdf = new MemoryStream(Build([new Line(72, 720, "Text")], []));

        var pages = _extractor.Extract(pdf);

        Assert.Equal(2, pages.Count);
        Assert.Empty(pages[1].Blocks);
    }

    [Fact]
    public void Garbage_input_is_a_permanent_failure()
    {
        using var notPdf = new MemoryStream("this is not a pdf at all"u8.ToArray());

        Assert.Throws<PermanentProcessingException>(() => _extractor.Extract(notPdf));
    }
}
