using DocQuery.Domain.Common;

namespace DocQuery.Domain.Documents;

/// <summary>
/// A piece of a document's text sized for embedding. Stored in its own table and never loaded with the
/// <see cref="Document"/> aggregate, which only tracks how many chunks exist.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Size of every stored embedding, fixed by the model (text-embedding-3-small) and by the vector column. Changing
    /// the model means a migration and re-embedding every chunk (ADR 0017).
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    private DocumentChunk(DocumentChunkId id, DocumentId documentId, int position, int pageNumber, string text, int tokenCount)
    {
        Id = id;
        DocumentId = documentId;
        Position = position;
        PageNumber = pageNumber;
        Text = text;
        TokenCount = tokenCount;
    }

    public DocumentChunkId Id { get; }

    public DocumentId DocumentId { get; }

    /// <summary>Zero-based order of the chunk within the document.</summary>
    public int Position { get; }

    /// <summary>One-based page the chunk's text comes from. Chunks never span pages.</summary>
    public int PageNumber { get; }

    public string Text { get; }

    public int TokenCount { get; }

    /// <summary>The chunk's embedding, or null until the embedding worker has processed it.</summary>
    public ReadOnlyMemory<float>? Embedding { get; private set; }

    public bool HasEmbedding => Embedding is not null;

    public static DocumentChunk Create(DocumentId documentId, int position, int pageNumber, string text, int tokenCount)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("A chunk must contain text.");
        }

        if (position < 0)
        {
            throw new DomainException("Chunk position must not be negative.");
        }

        if (pageNumber < 1)
        {
            throw new DomainException("Page numbers start at 1.");
        }

        if (tokenCount <= 0)
        {
            throw new DomainException("A chunk must contain at least one token.");
        }

        return new DocumentChunk(DocumentChunkId.Create(), documentId, position, pageNumber, text, tokenCount);
    }

    public void SetEmbedding(ReadOnlyMemory<float> embedding)
    {
        if (embedding.Length != EmbeddingDimensions)
        {
            throw new DomainException(FormattableString.Invariant(
                $"Embeddings must have {EmbeddingDimensions} dimensions; got {embedding.Length}."));
        }

        Embedding = embedding;
    }
}
