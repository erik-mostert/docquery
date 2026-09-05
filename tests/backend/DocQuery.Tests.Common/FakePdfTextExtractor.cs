using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Chunking;

namespace DocQuery.Tests.Common;

public sealed class FakePdfTextExtractor : IPdfTextExtractor
{
    private Exception? _failure;

    public List<PageText> Pages { get; } = [];

    public int CallCount { get; private set; }

    public void FailWith(Exception exception) => _failure = exception;

    public IReadOnlyList<PageText> Extract(Stream pdf)
    {
        CallCount++;
        if (_failure is not null)
        {
            throw _failure;
        }

        return Pages;
    }
}
