namespace DocQuery.Command.Api.Options;

/// <summary>Bound from the <c>Upload</c> configuration section.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    public long MaxFileSizeBytes { get; set; } = 50L * 1024 * 1024;
}
