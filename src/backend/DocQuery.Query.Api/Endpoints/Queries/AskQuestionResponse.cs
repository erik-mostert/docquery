namespace DocQuery.Query.Api.Endpoints.Queries;

/// <summary>The answer and the passages it cites; "[n]" in the answer refers to the citation with <c>Number</c> n.</summary>
public sealed record AskQuestionResponse(string Answer, IReadOnlyList<CitationResponse> Citations);

/// <summary><paramref name="Score"/> is cosine similarity (1 = identical); <paramref name="Excerpt"/> is the start of the passage.</summary>
public sealed record CitationResponse(int Number, Guid DocumentId, string FileName, int PageNumber, int Position, string Excerpt, double Score);
