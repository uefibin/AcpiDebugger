using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AcpiDebugger;

public static class AcpiLoader
{
    // SYSTEM_CODEINTEGRITY_INFORMATION
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_CODEINTEGRITY_INFORMATION
    {
        public uint Length;
        public uint CodeIntegrityOptions;
    }

    private const uint SystemCodeIntegrityInformation = 103;
    private const uint CODEINTEGRITY_OPTION_TESTSIGN = 0x00000002;

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        uint SystemInformationClass,
        ref SYSTEM_CODEINTEGRITY_INFORMATION SystemInformation,
        uint SystemInformationLength,
        out uint ReturnLength);

    public static bool IsTestSigningEnabled()
    {
        try
        {
            var info = new SYSTEM_CODEINTEGRITY_INFORMATION();
            info.Length = (uint)Marshal.SizeOf(info);
            uint returnLength;

            int status = NtQuerySystemInformation(
                SystemCodeIntegrityInformation,
                ref info,
                info.Length,
                out returnLength);

            if (status >= 0) // STATUS_SUCCESS = 0
            {
                return (info.CodeIntegrityOptions & CODEINTEGRITY_OPTION_TESTSIGN) != 0;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking test signing: {ex.Message}");
        }
        return false;
    }

    private static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string PrepareOverrideTable(
        string sourcePath,
        out uint originalRevision,
        out uint overrideRevision)
    {
        byte[] table = File.ReadAllBytes(sourcePath);
        if (table.Length < 36)
            throw new InvalidDataException("The selected file is not a valid ACPI table.");

        uint tableLength = BitConverter.ToUInt32(table, 4);
        if (tableLength < 36 || tableLength > table.Length)
            throw new InvalidDataException("The ACPI table length in the header is invalid.");

        originalRevision = BitConverter.ToUInt32(table, 24);
        if (originalRevision == uint.MaxValue)
            throw new InvalidDataException("OEM Revision is already 0xFFFFFFFF and cannot be increased.");

        overrideRevision = originalRevision + 1;
        byte[] revisionBytes = BitConverter.GetBytes(overrideRevision);
        Array.Copy(revisionBytes, 0, table, 24, revisionBytes.Length);

        // Recalculate the ACPI checksum after changing the OEM Revision.
        table[9] = 0;
        int sum = 0;
        for (int index = 0; index < tableLength; index++)
            sum = (sum + table[index]) & 0xFF;
        table[9] = unchecked((byte)(0 - sum));

        string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
        string overrideDirectory = Path.Combine(sourceDirectory, "Overrides");
        Directory.CreateDirectory(overrideDirectory);
        string overridePath = Path.Combine(
            overrideDirectory,
            Path.GetFileNameWithoutExtension(sourcePath) + ".override.aml");
        File.WriteAllBytes(overridePath, table);
        return overridePath;
    }

    public static string LoadAcpiTable(string amlFilePath, string toolsDir)
    {
        if (!IsRunningAsAdministrator())
        {
            return "Error: Administrator privileges are required.\n"
                 + "asl.exe must write the ACPI override table to HKLM. "
                 + "Please restart AcpiDebugger as administrator.";
        }

        // 1. Check Test Mode
        if (!IsTestSigningEnabled())
        {
             return "Error: System is not in Test Mode (TestSigning). ACPI tables cannot be loaded.\nPlease enable Test Signing (bcdedit /set testsigning on) and reboot.";
        }

        // 2. Locate asl.exe (Microsoft ASL Compiler)
        string aslPath = Path.Combine(toolsDir, "asl.exe");
        
        // Explicitly check tools folder first
        if (!File.Exists(aslPath))
        {
            // If not in tools, maybe in PATH?
            // But relying on PATH is flaky. Let's warn the user if it's not in tools.
            // We can try running "asl.exe" blindly, but if it fails, we give a better message.
            aslPath = "asl.exe"; 
        }

        try
        {
            string overridePath = PrepareOverrideTable(
                amlFilePath,
                out uint originalRevision,
                out uint overrideRevision);

            // /loadtable stages the override in the registry. Acpi.sys consumes it
            // during the next boot; it does not replace the live namespace.
            using (var proc = new Process())
            {
                proc.StartInfo.FileName = aslPath;
                proc.StartInfo.Arguments = $"/loadtable -v \"{overridePath}\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;

                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                string fullOutput = output + "\n" + err;

                if (proc.ExitCode == 0)
                {
                    return "Success: ACPI override staged in the registry.\n"
                         + $"OEM Revision: 0x{originalRevision:X8} -> 0x{overrideRevision:X8}\n"
                         + $"Override file: {overridePath}\n"
                         + "IMPORTANT: Restart Windows before verifying the ACPI namespace. "
                         + "A new acpidump taken before restart will still show the firmware table.\n\n"
                         + fullOutput;
                }
                else
                {
                    if (fullOutput.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                        || fullOutput.Contains("cannot find", StringComparison.OrdinalIgnoreCase))
                    {
                         return "Error: Microsoft 'asl.exe' not found.\nPlease download the WDK or copy 'asl.exe' to the 'tools' folder.";
                    }

                    if (fullOutput.Contains("Could not access the registry path", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Error: asl.exe could not write the ACPI override registry key.\n"
                             + "Verify that AcpiDebugger is running as administrator and that security software is not blocking HKLM writes.\n\n"
                             + fullOutput;
                    }

                    return $"Error loading table (Exit Code {proc.ExitCode}):\n{fullOutput}";
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2) // File not found
        {
             return "Error: 'asl.exe' (Microsoft ASL Compiler) not found.\n" + 
                    "It is NOT included with this tool.\n" + 
                    "Please download the Windows Driver Kit (WDK) or copy 'asl.exe' to the 'tools' folder.";
        }
        catch (Exception ex)
        {
             return $"Error executing asl.exe: {ex.Message}\nMake sure Microsoft ASL Compiler is installed or in the tools folder.";
        }
    }
}
