using DocQuery.Domain.Documents;

namespace DocQuery.Domain.Tests.Documents;

public sealed class DocumentIdTests
{
    [Fact]
    public void Create_returns_non_empty_id()
    {
        var id = DocumentId.Create();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Create_returns_distinct_ids()
    {
        var first = DocumentId.Create();
        var second = DocumentId.Create();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Ids_with_same_value_are_equal()
    {
        var value = Guid.NewGuid();

        Assert.Equal(new DocumentId(value), new DocumentId(value));
    }
}
