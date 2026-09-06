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
        Assert.False(chunk.HasEmbedding);
        Assert.Null(chunk.Embedding);
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

    [Fact]
    public void SetEmbedding_stores_a_vector_of_the_configured_dimensions()
    {
        var chunk = DocumentChunk.Create(DocumentId, 0, 1, "text", 1);
        var vector = new float[DocumentChunk.EmbeddingDimensions];
        vector[0] = 0.5f;

        chunk.SetEmbedding(vector);

        Assert.True(chunk.HasEmbedding);
        Assert.Equal(DocumentChunk.EmbeddingDimensions, chunk.Embedding!.Value.Length);
        Assert.Equal(0.5f, chunk.Embedding.Value.Span[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1535)]
    [InlineData(3072)]
    public void SetEmbedding_rejects_a_vector_of_the_wrong_size(int length)
    {
        var chunk = DocumentChunk.Create(DocumentId, 0, 1, "text", 1);

        Assert.Throws<DomainException>(() => chunk.SetEmbedding(new float[length]));
    }
}
