using System.ComponentModel.DataAnnotations;

namespace DocQuery.Command.Api.Options;

/// <summary>Bound from the <c>Upload</c> configuration section; validated at startup.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    /// <summary>Between 1 byte and 10 GiB.</summary>
    [Range(typeof(long), "1", "10737418240")]
    public long MaxFileSizeBytes { get; set; } = 50L * 1024 * 1024;
}
