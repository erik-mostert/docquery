using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace DocQuery.Infrastructure.Persistence.Search;

/// <summary>
/// Cosine nearest-neighbour search with pgvector's <c>&lt;=&gt;</c> operator, served by the HNSW index on
/// <c>document_chunks.Embedding</c>. Raw SQL because EF Core has no translation for the operator (ADR 0019).
/// </summary>
internal sealed class ChunkSearch(DocQueryDbContext context) : IChunkSearch
{
    public async Task<IReadOnlyList<ChunkMatch>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, DocumentId? documentId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var query = new Vector(queryEmbedding);
        Guid? scope = documentId?.Value;

        var rows = await context.Database
            .SqlQuery<ChunkMatchRow>($"""
                SELECT c."DocumentId", d."FileName", c."PageNumber", c."Position", c."Text", c."Embedding" <=> {query} AS "Distance"
                FROM document_chunks AS c
                JOIN documents AS d ON d."Id" = c."DocumentId"
                WHERE c."Embedding" IS NOT NULL AND ({scope}::uuid IS NULL OR c."DocumentId" = {scope})
                ORDER BY c."Embedding" <=> {query}
                LIMIT {limit}
                """)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ChunkMatch(new DocumentId(row.DocumentId), row.FileName, row.PageNumber, row.Position, row.Text, row.Distance))
            .ToList();
    }

    /// <summary>Shape of one result row; property names must match the selected column names.</summary>
    private sealed class ChunkMatchRow
    {
        public Guid DocumentId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public int PageNumber { get; set; }

        public int Position { get; set; }

        public string Text { get; set; } = string.Empty;

        public double Distance { get; set; }
    }
}
