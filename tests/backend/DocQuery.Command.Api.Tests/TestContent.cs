using System.Net.Http.Headers;

namespace DocQuery.Command.Api.Tests;

internal static class TestContent
{
    public static readonly byte[] PdfBytes = "%PDF-1.4 test document"u8.ToArray();

    public static readonly Uri DocumentsUri = new("/documents", UriKind.Relative);

    /// <summary>Builds a multipart body with one <c>file</c> part.</summary>
    public static MultipartFormDataContent FileForm(byte[] bytes, string contentType, string fileName = "report.pdf")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    public static MultipartFormDataContent PdfForm() => FileForm(PdfBytes, "application/pdf");
}
