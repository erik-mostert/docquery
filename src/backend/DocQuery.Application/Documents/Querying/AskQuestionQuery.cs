using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Querying;

/// <summary>
/// Ask a question over every embedded document, or over one when <paramref name="DocumentId"/> is given.
/// <paramref name="TopK"/> overrides the configured number of passages retrieved.
/// </summary>
public sealed record AskQuestionQuery(string Question, DocumentId? DocumentId = null, int? TopK = null);
