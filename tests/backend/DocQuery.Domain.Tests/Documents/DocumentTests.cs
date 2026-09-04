using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Domain.Tests.Documents;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Upload_sets_properties_and_uploaded_status()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);

        Assert.NotEqual(default, document.Id);
        Assert.Equal("report.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(1234, document.SizeInBytes);
        Assert.Equal(UploadedAt, document.UploadedAt);
        Assert.Equal(DocumentStatus.Uploaded, document.Status);
    }

    [Fact]
    public void Upload_derives_blob_name_from_id()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);

        Assert.Equal(FormattableString.Invariant($"{document.Id.Value}.pdf"), document.BlobName);
    }

    [Fact]
    public void Upload_raises_document_uploaded_event()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);

        var domainEvent = Assert.Single(document.DomainEvents);
        var uploaded = Assert.IsType<DocumentUploadedEvent>(domainEvent);
        Assert.Equal(document.Id, uploaded.DocumentId);
        Assert.Equal(document.BlobName, uploaded.BlobName);
        Assert.Equal(UploadedAt, uploaded.UploadedAt);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_event_list()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);

        document.ClearDomainEvents();

        Assert.Empty(document.DomainEvents);
    }

    [Fact]
    public void Upload_accepts_pdf_content_type_case_insensitively()
    {
        var document = Document.Upload("report.pdf", "Application/PDF", 1234, UploadedAt);

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Upload_rejects_blank_file_name(string fileName)
    {
        Assert.Throws<DomainException>(() => Document.Upload(fileName, "application/pdf", 1234, UploadedAt));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/png")]
    [InlineData("")]
    public void Upload_rejects_non_pdf_content_type(string contentType)
    {
        Assert.Throws<DomainException>(() => Document.Upload("report.pdf", contentType, 1234, UploadedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Upload_rejects_non_positive_size(long sizeInBytes)
    {
        Assert.Throws<DomainException>(() => Document.Upload("report.pdf", "application/pdf", sizeInBytes, UploadedAt));
    }
}
