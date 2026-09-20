using AcpiDebugger.Models;
using System.Diagnostics;
using System.IO;

namespace AcpiDebugger.Services;

public sealed class AcpiToolService
{
    private readonly string _toolsDirectory;

    public AcpiToolService(string toolsDirectory)
    {
        _toolsDirectory = toolsDirectory;
    }

    public bool IsAvailable => ToolLocatorService.GetStatus(_toolsDirectory).IaslAvailable;

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
        string externalArgs = externalTables.Count == 0
            ? string.Empty
            : "-e " + string.Join(" ", externalTables.Select(name => $"\"{name}\"")) + " ";
        string args = $"{externalArgs}-d \"{target}\"";
        return RunAsync(Path.Combine(_toolsDirectory, "iasl.exe"), args, workspaceDirectory, cancellationToken);
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
        RunAsync(
            Path.Combine(_toolsDirectory, "iasl.exe"),
            $"-ve \"{fileName}\"",
            workspaceDirectory,
            cancellationToken);

    private static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            (await outputTask) + Environment.NewLine + (await errorTask));
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
