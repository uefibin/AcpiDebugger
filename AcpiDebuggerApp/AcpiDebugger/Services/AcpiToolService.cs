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

    public bool IsAvailable => ToolLocatorService.GetStatus(_toolsDirectory).AcpicaAvailable;

    public Task<ProcessResult> DumpAsync(string workspaceDirectory, CancellationToken cancellationToken = default) =>
        RunAsync(Path.Combine(_toolsDirectory, "acpidump.exe"), "-b", workspaceDirectory, cancellationToken);

    public Task<ProcessResult> DecompileAsync(
        string workspaceDirectory,
        string fileName,
        bool includeDsdt,
        CancellationToken cancellationToken = default)
    {
        string target = Path.Combine(workspaceDirectory, fileName);
        string dsdt = Path.Combine(workspaceDirectory, "dsdt.dat");
        string args = includeDsdt && File.Exists(dsdt)
            ? $"-e \"{dsdt}\" -d \"{target}\""
            : $"-d \"{target}\"";
        return RunAsync(Path.Combine(_toolsDirectory, "iasl.exe"), args, workspaceDirectory, cancellationToken);
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
}
