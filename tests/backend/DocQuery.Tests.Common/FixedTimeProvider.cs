namespace DocQuery.Tests.Common;

/// <summary>A <see cref="TimeProvider"/> frozen at one instant.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
