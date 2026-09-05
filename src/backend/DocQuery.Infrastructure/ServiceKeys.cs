namespace DocQuery.Infrastructure;

/// <summary>Keys for services that are registered raw and then wrapped by a resilience decorator (ADR 0007).</summary>
public static class ServiceKeys
{
    /// <summary>The undecorated <c>IBlobStore</c> adapter; <c>ResilientBlobStore</c> wraps it as the public registration.</summary>
    public const string RawBlobStore = "blob-store:raw";

    /// <summary>The undecorated <c>IEventBus</c> adapter; <c>ResilientEventBus</c> wraps it as the public registration.</summary>
    public const string RawEventBus = "event-bus:raw";
}
