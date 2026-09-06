namespace DocQuery.Query.Api.Endpoints.Queries;

/// <summary>
/// Body of <c>POST /queries</c>. <paramref name="DocumentId"/> restricts retrieval to one document;
/// <paramref name="TopK"/> overrides the configured number of passages (bounded by <c>Querying:MaxTopK</c>).
/// </summary>
public sealed record AskQuestionRequest(string? Question, Guid? DocumentId = null, int? TopK = null);
