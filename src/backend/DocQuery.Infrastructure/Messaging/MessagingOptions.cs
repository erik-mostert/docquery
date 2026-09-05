using System.ComponentModel.DataAnnotations;

namespace DocQuery.Infrastructure.Messaging;

/// <summary>Bound from the <c>Messaging</c> configuration section; validated at startup.</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Topic that carries every document event; workers subscribe to it (ADR 0014).</summary>
    [Required]
    [MinLength(1)]
    public string TopicName { get; set; } = string.Empty;
}
