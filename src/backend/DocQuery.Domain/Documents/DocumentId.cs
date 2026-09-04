namespace DocQuery.Domain.Documents;

/// <summary>Strongly typed identifier for a <see cref="Document"/>. Time-ordered (UUID v7) for index locality.</summary>
public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId Create() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
