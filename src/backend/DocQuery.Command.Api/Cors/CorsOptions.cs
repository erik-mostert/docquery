namespace DocQuery.Command.Api.Cors;

/// <summary>Bound from the <c>Cors</c> configuration section.</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Origins allowed to call the API from a browser, e.g. the Vite dev server.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];
}
