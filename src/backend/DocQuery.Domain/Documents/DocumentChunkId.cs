namespace DocQuery.Domain.Documents;

/// <summary>Strongly typed identifier for a <see cref="DocumentChunk"/>. Time-ordered (UUID v7) for index locality.</summary>
public readonly record struct DocumentChunkId(Guid Value)
{
    public static DocumentChunkId Create() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
