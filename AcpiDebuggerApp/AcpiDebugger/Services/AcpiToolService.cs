using AcpiDebugger.Models;
using System.IO;

namespace AcpiDebugger.Services;

public sealed class AcpiToolService
{
    public AcpiToolService(string toolsDirectory)
    {
        _ = toolsDirectory;
    }

    public bool IsAvailable => AcpiNativeCompilerService.IsAvailable;

    public Task<ProcessResult> DumpAsync(string workspaceDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => NativeAcpiDumpService.Dump(workspaceDirectory), cancellationToken);

    public Task<ProcessResult> DecompileAsync(
        string workspaceDirectory,
        string fileName,
        bool includeExternalTables,
        CancellationToken cancellationToken = default)
    {
        string target = Path.Combine(workspaceDirectory, fileName);
        IReadOnlyList<string> externalTables = includeExternalTables
            ? GetExternalTableNames(workspaceDirectory, fileName)
            : Array.Empty<string>();
        string[] externalPaths = externalTables
            .Select(name => Path.Combine(workspaceDirectory, name))
            .ToArray();
        return RunNativeAsync(
            () => AcpiNativeCompilerService.Disassemble(target, externalPaths, CreateLogPath()),
            cancellationToken);
    }

    public static IReadOnlyList<string> GetExternalTableNames(
        string workspaceDirectory,
        string fileName)
    {
        if (fileName.Equals("dsdt.dat", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("dsdt.aml", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.EnumerateFiles(workspaceDirectory)
                .Where(path => Path.GetFileName(path).StartsWith("ssdt", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)
                            || path.EndsWith(".aml", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(GetTableFileOrder)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        string dsdt = Directory.EnumerateFiles(workspaceDirectory)
            .FirstOrDefault(path =>
                Path.GetFileName(path).Equals("dsdt.dat", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("dsdt.aml", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
        return string.IsNullOrEmpty(dsdt)
            ? Array.Empty<string>()
            : [Path.GetFileName(dsdt)];
    }

    public Task<ProcessResult> CompileAsync(
        string workspaceDirectory,
        string fileName,
        CancellationToken cancellationToken = default) =>
        RunNativeAsync(
            () => AcpiNativeCompilerService.Compile(
                Path.Combine(workspaceDirectory, fileName),
                CreateLogPath()),
            cancellationToken);

    private static Task<ProcessResult> RunNativeAsync(
        Func<ProcessResult> operation,
        CancellationToken cancellationToken)
    {
        return Task.Run(operation, cancellationToken);
    }

    private static string CreateLogPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"AcpiDebugger-{Guid.NewGuid():N}.log");
    }

    private static int GetTableFileOrder(string name)
    {
        string stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Equals("ssdt", StringComparison.OrdinalIgnoreCase)) return 0;
        if (stem.StartsWith("ssdt-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(stem[5..], out int instance))
            return instance;
        return int.MaxValue;
    }
}
