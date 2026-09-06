using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Documents;
using Microsoft.Extensions.AI;

namespace DocQuery.Application.Tests.Documents.Querying;

public sealed class RagPromptTests
{
    private static readonly ChunkMatch First = new(DocumentId.Create(), "handbook.pdf", 3, 7, "Refunds are issued within 14 days.", 0.1);
    private static readonly ChunkMatch Second = new(DocumentId.Create(), "faq.pdf", 1, 0, "Contact support by email.", 0.2);

    [Fact]
    public void Starts_with_the_system_instruction_and_ends_with_one_user_message()
    {
        var messages = RagPrompt.Build("How long do refunds take?", [First, Second]);

        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(RagPrompt.SystemInstruction, messages[0].Text);
        Assert.Equal(ChatRole.User, messages[1].Role);
    }

    [Fact]
    public void Numbers_passages_in_order_with_their_source_and_page()
    {
        var user = RagPrompt.Build("How long do refunds take?", [First, Second])[1].Text;

        Assert.Contains("[1] handbook.pdf, page 3", user, StringComparison.Ordinal);
        Assert.Contains("[2] faq.pdf, page 1", user, StringComparison.Ordinal);
        Assert.True(user.IndexOf("[1]", StringComparison.Ordinal) < user.IndexOf("[2]", StringComparison.Ordinal));
        Assert.Contains(First.Text, user, StringComparison.Ordinal);
        Assert.Contains(Second.Text, user, StringComparison.Ordinal);
    }

    [Fact]
    public void Puts_the_question_after_the_passages()
    {
        var user = RagPrompt.Build("How long do refunds take?", [First])[^1].Text!;

        Assert.EndsWith("Question: How long do refunds take?", user, StringComparison.Ordinal);
        Assert.True(user.IndexOf(First.Text, StringComparison.Ordinal) < user.IndexOf("Question:", StringComparison.Ordinal));
    }

    [Fact]
    public void Instruction_demands_citations_and_forbids_outside_knowledge()
    {
        Assert.Contains("[1]", RagPrompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("only the passages provided", RagPrompt.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("do not answer the question", RagPrompt.SystemInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_blank_question()
    {
        Assert.Throws<ArgumentException>(() => RagPrompt.Build(" ", [First]));
    }
}
