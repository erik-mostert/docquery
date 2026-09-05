namespace DocQuery.Tests.Common;

/// <summary>Detects a reachable Docker engine so container-backed tests can skip instead of fail without one.</summary>
public static class DockerAvailability
{
    public static bool IsAvailable { get; } = Detect();

    public static string SkipReason => "Docker engine is not available.";

    private static bool Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return File.Exists(@"\\.\pipe\docker_engine") || File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine");
        }

        return File.Exists("/var/run/docker.sock");
    }
}
