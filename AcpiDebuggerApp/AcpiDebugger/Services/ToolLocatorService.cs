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

        return candidates.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static ToolStatus GetStatus(string toolsDirectory)
    {
        return new ToolStatus(
            AcpiNativeCompilerService.IsAvailable,
            File.Exists(Path.Combine(toolsDirectory, "acpidump.exe")));
    }
}

public sealed record ToolStatus(bool AcpicaAvailable, bool AcpiDumpAvailable)
{
    public bool NativeAcpiAvailable => OperatingSystem.IsWindows();
}
