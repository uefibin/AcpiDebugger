using System.IO;

namespace AcpiDebugger.Services;

public static class ToolLocatorService
{
    public static string LocateToolsDirectory()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "tools"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tools")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools"))
        };

        return candidates.FirstOrDefault(path =>
            File.Exists(Path.Combine(path, "iasl.exe")) &&
            File.Exists(Path.Combine(path, "acpidump.exe"))) ?? string.Empty;
    }

    public static ToolStatus GetStatus(string toolsDirectory)
    {
        return new ToolStatus(
            File.Exists(Path.Combine(toolsDirectory, "iasl.exe")),
            File.Exists(Path.Combine(toolsDirectory, "acpidump.exe")),
            File.Exists(Path.Combine(toolsDirectory, "asl.exe")) || IsOnPath("asl.exe"));
    }

    private static bool IsOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return false;

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory.Trim(), fileName)));
    }
}

public sealed record ToolStatus(bool IaslAvailable, bool AcpiDumpAvailable, bool MicrosoftAslAvailable)
{
    public bool AcpicaAvailable => IaslAvailable && AcpiDumpAvailable;
}
