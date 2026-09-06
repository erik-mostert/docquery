using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Querying;

/// <summary>One retrieved chunk. <paramref name="Distance"/> is cosine distance (0 = identical, 2 = opposite).</summary>
public sealed record ChunkMatch(DocumentId DocumentId, string FileName, int PageNumber, int Position, string Text, double Distance);
