using DocQuery.Domain.Common;
using DocQuery.Domain.Querying;

namespace DocQuery.Domain.Tests.Querying;

public sealed class QuestionTests
{
    [Fact]
    public void Trims_surrounding_whitespace()
    {
        var question = Question.Create("  What is the refund policy?\n");

        Assert.Equal("What is the refund policy?", question.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Rejects_empty_text(string text)
    {
        var exception = Assert.Throws<DomainException>(() => Question.Create(text));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_text_longer_than_the_maximum()
    {
        var text = new string('a', Question.MaxLength + 1);

        var exception = Assert.Throws<DomainException>(() => Question.Create(text));

        Assert.Contains(Question.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_text_at_the_maximum_length()
    {
        var text = new string('a', Question.MaxLength);

        Assert.Equal(text, Question.Create(text).Text);
    }
}
