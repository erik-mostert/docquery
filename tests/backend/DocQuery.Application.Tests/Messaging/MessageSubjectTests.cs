using DocQuery.Contracts.Documents;
using DocQuery.Contracts.Messaging;

namespace DocQuery.Application.Tests.Messaging;

/// <summary>
/// Subjects are the contract type's full name. The literal strings below are also the correlation filters on the
/// Service Bus subscriptions (AppHost and infra/bicep), so renaming a contract must fail here first.
/// </summary>
public sealed class MessageSubjectTests
{
    [Theory]
    [InlineData(typeof(DocumentUploaded), "DocQuery.Contracts.Documents.DocumentUploaded")]
    [InlineData(typeof(DocumentChunked), "DocQuery.Contracts.Documents.DocumentChunked")]
    [InlineData(typeof(DocumentEmbedded), "DocQuery.Contracts.Documents.DocumentEmbedded")]
    public void Subject_is_the_contract_type_full_name(Type contract, string expected)
    {
        Assert.Equal(expected, MessageSubject.Of(contract));
    }

    [Fact]
    public void Generic_and_type_overloads_agree()
    {
        Type contract = typeof(DocumentUploaded);

        Assert.Equal(MessageSubject.Of<DocumentUploaded>(), MessageSubject.Of(contract));
    }

    [Fact]
    public void Instance_overload_uses_the_runtime_type()
    {
        object message = new DocumentChunked(Guid.NewGuid(), 3, DateTimeOffset.UtcNow);

        Assert.Equal(MessageSubject.Of<DocumentChunked>(), MessageSubject.Of(message));
    }
}
