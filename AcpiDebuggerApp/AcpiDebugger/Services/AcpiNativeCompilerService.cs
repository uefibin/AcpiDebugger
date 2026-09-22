using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using AcpiDebugger.Models;

namespace AcpiDebugger.Services;

internal static class AcpiNativeCompilerService
{
    private const string LibraryName = "AcpiCompilerHost";

    public static bool IsAvailable
    {
        get
        {
            string[] candidates =
            [
                Path.Combine(AppContext.BaseDirectory, "AcpiCompilerHost.dll"),
                Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "AcpiCompilerHost.dll"),
                Path.Combine(AppContext.BaseDirectory, "runtimes", "win-arm64", "native", "AcpiCompilerHost.dll")
            ];

            return candidates.Any(File.Exists);
        }
    }

    public static ProcessResult Compile(string inputPath, string logPath)
    {
        return Invoke(
            () => NativeMethods.Compile(inputPath, logPath),
            logPath,
            "compile");
    }

    public static ProcessResult Disassemble(
        string inputPath,
        IReadOnlyList<string> externalFiles,
        string logPath)
    {
        string externalList = string.Join(
            ";",
            externalFiles.Where(File.Exists).Select(Path.GetFullPath));

        return Invoke(
            () => NativeMethods.Disassemble(inputPath, externalList, logPath),
            logPath,
            "disassemble");
    }

    private static ProcessResult Invoke(
        Func<int> operation,
        string logPath,
        string operationName)
    {
        try
        {
            int status = operation();
            string output = ReadLog(logPath);
            if (string.IsNullOrWhiteSpace(output))
            {
                output = $"Integrated ACPICA {operationName} completed.";
            }

            if (status != 0)
            {
                output = $"Integrated ACPICA returned status 0x{status:X8}." + Environment.NewLine + output;
            }

            return new ProcessResult(status, output);
        }
        catch (DllNotFoundException)
        {
            return new ProcessResult(-1, "The integrated ACPICA backend is not available for this architecture.");
        }
        catch (EntryPointNotFoundException)
        {
            return new ProcessResult(-1, "The integrated ACPICA backend is missing the requested entry point.");
        }
        catch (BadImageFormatException)
        {
            return new ProcessResult(-1, "The integrated ACPICA backend architecture does not match the application.");
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    private static string ReadLog(string logPath)
    {
        if (!File.Exists(logPath))
        {
            return string.Empty;
        }

        try
        {
            return File.ReadAllText(logPath, Encoding.UTF8).Trim();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Diagnostics are best-effort; the operation result is already known.
        }
    }

    private static class NativeMethods
    {
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, EntryPoint = "AcpiCompilerHost_Compile")]
        public static extern int Compile(string inputPath, string logPath);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, EntryPoint = "AcpiCompilerHost_Disassemble")]
        public static extern int Disassemble(string inputPath, string externalFiles, string logPath);
    }
}
