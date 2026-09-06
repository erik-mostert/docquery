namespace DocQuery.Application.Documents.Listing;

/// <summary>The most recent documents, newest first. <paramref name="Limit"/> defaults to <see cref="DefaultLimit"/>.</summary>
public sealed record ListDocumentsQuery(int? Limit = null)
{
    public const int DefaultLimit = 50;

    public const int MaxLimit = 200;
}
