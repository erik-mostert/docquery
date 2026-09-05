namespace DocQuery.Infrastructure.Persistence;

/// <summary>Bound from the <c>Persistence</c> configuration section.</summary>
public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    /// <summary>
    /// Apply pending EF Core migrations when the host starts. Convenient for local runs and the demo; a production
    /// deployment would run migrations from a job before rolling out new pods.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}
