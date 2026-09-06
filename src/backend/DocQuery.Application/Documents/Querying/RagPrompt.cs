using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;

namespace DocQuery.Application.Documents.Querying;

/// <summary>
/// Builds the chat messages for retrieval-augmented answering: a fixed system instruction, then one user message
/// holding the numbered passages and the question. Passage numbers match the citation numbers returned to the
/// caller, so "[2]" in the answer refers to citation 2 (ADR 0019).
/// </summary>
public static class RagPrompt
{
    public const string SystemInstruction =
        "You answer questions about documents using only the passages provided. " +
        "Cite every fact with the passage number in square brackets, for example [1] or [2][3]. " +
        "If the passages do not contain the information needed, say that the documents do not answer the question. " +
        "Do not use outside knowledge. Answer in the language of the question.";

    public static IReadOnlyList<ChatMessage> Build(string question, IReadOnlyList<ChunkMatch> passages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(passages);

        var user = new StringBuilder();
        user.AppendLine("Passages:");
        for (var index = 0; index < passages.Count; index++)
        {
            var passage = passages[index];
            user.AppendLine();
            user.AppendLine(string.Create(CultureInfo.InvariantCulture, $"[{index + 1}] {passage.FileName}, page {passage.PageNumber}"));
            user.AppendLine(passage.Text);
        }

        user.AppendLine();
        user.Append("Question: ").Append(question);

        return
        [
            new ChatMessage(ChatRole.System, SystemInstruction),
            new ChatMessage(ChatRole.User, user.ToString()),
        ];
    }
}
