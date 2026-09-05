using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Domain.Tests.Documents;

public sealed class DocumentChunkTests
{
    private static readonly DocumentId DocumentId = DocumentId.Create();

    [Fact]
    public void Create_sets_properties_and_a_fresh_id()
    {
        var chunk = DocumentChunk.Create(DocumentId, position: 3, pageNumber: 2, text: "Some text.", tokenCount: 3);

        Assert.NotEqual(default, chunk.Id);
        Assert.Equal(DocumentId, chunk.DocumentId);
        Assert.Equal(3, chunk.Position);
        Assert.Equal(2, chunk.PageNumber);
        Assert.Equal("Some text.", chunk.Text);
        Assert.Equal(3, chunk.TokenCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_text(string text)
    {
        Assert.Throws<DomainException>(() => DocumentChunk.Create(DocumentId, 0, 1, text, 1));
    }

    [Fact]
    public void Create_rejects_negative_position()
    {
        Assert.Throws<DomainException>(() => DocumentChunk.Create(DocumentId, -1, 1, "text", 1));
    }

    [Fact]
    public void Create_rejects_page_below_one()
    {
        Assert.Throws<DomainException>(() => DocumentChunk.Create(DocumentId, 0, 0, "text", 1));
    }

    [Fact]
    public void Create_rejects_non_positive_token_count()
    {
        Assert.Throws<DomainException>(() => DocumentChunk.Create(DocumentId, 0, 1, "text", 0));
    }
}
