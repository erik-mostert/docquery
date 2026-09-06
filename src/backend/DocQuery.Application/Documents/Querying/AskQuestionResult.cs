using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Querying;

/// <summary>The generated answer and the passages it was grounded in, numbered as the answer cites them ([1], [2], ...).</summary>
public sealed record AskQuestionResult(string Answer, IReadOnlyList<Citation> Citations)
{
    /// <summary>Returned, without consulting the chat model, when no embedded chunk matches the question.</summary>
    public const string NoPassagesAnswer = "No embedded document content is available to answer this question.";
}

/// <summary><paramref name="Score"/> is cosine similarity (1 = identical); <paramref name="Excerpt"/> is the start of the passage.</summary>
public sealed record Citation(int Number, DocumentId DocumentId, string FileName, int PageNumber, int Position, string Excerpt, double Score);
