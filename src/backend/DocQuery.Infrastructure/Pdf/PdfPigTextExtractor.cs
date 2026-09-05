using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Chunking;
using DocQuery.Application.Exceptions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace DocQuery.Infrastructure.Pdf;

/// <summary>
/// PdfPig-based extractor. Words are grouped into layout blocks with the Docstrum segmenter (line spacing and
/// alignment), which is how paragraphs and list bodies come out as units, then ordered for reading. PDF bytes are
/// immutable, so any parser failure is permanent (ADR 0015).
/// </summary>
internal sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public IReadOnlyList<PageText> Extract(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        try
        {
            using var document = PdfDocument.Open(pdf);
            var pages = new List<PageText>();

            foreach (var page in document.GetPages())
            {
                pages.Add(new PageText(page.Number, ExtractBlocks(page)));
            }

            return pages;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new PermanentProcessingException("PDF could not be parsed.", exception);
        }
    }

    private static TextBlock[] ExtractBlocks(UglyToad.PdfPig.Content.Page page)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToList();
        if (words.Count == 0)
        {
            return [];
        }

        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
        var ordered = UnsupervisedReadingOrderDetector.Instance.Get(blocks);

        return ordered
            .Select(block => new TextBlock(block.TextLines.Select(line => line.Text).ToArray()))
            .ToArray();
    }
}
