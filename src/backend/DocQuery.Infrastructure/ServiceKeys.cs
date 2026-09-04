namespace DocQuery.Infrastructure;

/// <summary>Keys for services that are registered raw and then wrapped by a decorator.</summary>
public static class ServiceKeys
{
    /// <summary>The undecorated <c>IBlobStore</c> adapter; <c>ResilientBlobStore</c> wraps it as the public registration.</summary>
    public const string RawBlobStore = "blob-store:raw";
}
