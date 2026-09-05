using DocQuery.Application.Documents.Chunking;

namespace DocQuery.Application.Abstractions;

/// <summary>Extracts text from a PDF as layout blocks per page. Synchronous because the work is CPU-bound.</summary>
public interface IPdfTextExtractor
{
    /// <exception cref="Exceptions.PermanentProcessingException">The stream is not a readable PDF.</exception>
    IReadOnlyList<PageText> Extract(Stream pdf);
}
