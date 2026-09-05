using System.Globalization;
using System.Text;

namespace DocQuery.Infrastructure.Tests.Pdf;

/// <summary>
/// Builds a small, valid PDF with Helvetica text lines at given positions, computing the cross-reference offsets so
/// the parser's happy path is exercised rather than its recovery path.
/// </summary>
internal static class PdfBuilder
{
    public sealed record Line(double X, double Y, string Text);

    public static byte[] Build(params IReadOnlyList<Line>[] pages)
    {
        var objects = new SortedDictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [3] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var kids = new List<string>();
        var nextId = 4;
        foreach (var page in pages)
        {
            var pageId = nextId++;
            var contentId = nextId++;
            var content = string.Join('\n', page.Select(line =>
                string.Create(CultureInfo.InvariantCulture, $"BT /F1 12 Tf {line.X} {line.Y} Td ({Escape(line.Text)}) Tj ET")));

            objects[contentId] = string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
            objects[pageId] = string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>");
            kids.Add(string.Create(CultureInfo.InvariantCulture, $"{pageId} 0 R"));
        }

        objects[2] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{string.Join(' ', kids)}] /Count {pages.Length} >>");

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new SortedDictionary<int, int>();
        foreach (var (id, body) in objects)
        {
            offsets[id] = Encoding.ASCII.GetByteCount(pdf.ToString());
            pdf.Append(CultureInfo.InvariantCulture, $"{id} 0 obj\n{body}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {nextId}\n");
        pdf.Append("0000000000 65535 f \n");
        for (var id = 1; id < nextId; id++)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{offsets[id]:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {nextId} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
}
