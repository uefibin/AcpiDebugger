using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace AcpiDebugger;

public static class AcpiLoader
{
    private readonly record struct AcpiTableIdentity(
        string Signature,
        string OemId,
        string OemTableId,
        uint OemRevision);

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

        string overridePath = GetOverridePath(sourcePath);
        string overrideDirectory = Path.GetDirectoryName(overridePath)!;
        Directory.CreateDirectory(overrideDirectory);
        File.WriteAllBytes(overridePath, table);
        return overridePath;
    }

    private static string GetOverridePath(string sourcePath)
    {
        string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
        return Path.Combine(
            sourceDirectory,
            "Overrides",
            Path.GetFileNameWithoutExtension(sourcePath) + ".override.aml");
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

    public static string RemoveAcpiTable(
        string amlFilePath,
        string toolsDir,
        CancellationToken cancellationToken = default)
    {
        if (!IsRunningAsAdministrator())
        {
            return "Error: Administrator privileges are required.\n"
                 + "Please restart AcpiDebugger as administrator.";
        }

        string aslPath = Path.Combine(toolsDir, "asl.exe");
        if (!File.Exists(aslPath))
            aslPath = "asl.exe";

        try
        {
            // Prefer the exact table generated during Load. Rebuilding it from the
            // current source could target a different revision after the file changes.
            string overridePath = GetOverridePath(amlFilePath);
            if (!File.Exists(overridePath))
            {
                overridePath = PrepareOverrideTable(
                    amlFilePath,
                    out _,
                    out _);
            }

            AcpiTableIdentity identity = ReadAcpiTableIdentity(overridePath);
            bool existedBefore = RegistryOverrideExists(identity);

            using var proc = new Process();
            proc.StartInfo.FileName = aslPath;
            proc.StartInfo.Arguments = $"/loadtable -v -d \"{overridePath}\"";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.CreateNoWindow = true;

            proc.Start();
            Task<string> outputTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                var timeout = Stopwatch.StartNew();
                while (!proc.WaitForExit(200))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (timeout.Elapsed >= TimeSpan.FromSeconds(30))
                        throw new TimeoutException("asl.exe did not finish within 30 seconds.");
                }
            }
            catch
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
                throw;
            }

            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();

            string fullOutput = (output + "\n" + error).Trim();

            // Microsoft ASL 5.0 returns exit code 1 even after a successful
            // /loadtable -d operation. Its completion message is authoritative.
            bool registryDataDeleted = fullOutput.Contains(
                "Registry data deleted",
                StringComparison.OrdinalIgnoreCase);
            bool existsAfter = RegistryOverrideExists(identity);

            if (!existsAfter && (registryDataDeleted || (proc.ExitCode == 0 && existedBefore)))
            {
                return "Success: ACPI override revision was removed.\n"
                     + $"Table: {Path.GetFileName(amlFilePath)}\n"
                     + $"OEM Revision: 0x{identity.OemRevision:X8}\n"
                     + "The currently loaded ACPI namespace is unchanged. "
                     + "Restart Windows to restore the firmware ACPI table.";
            }

            if (!existedBefore && !existsAfter)
            {
                return "Error: no matching staged ACPI override was found.\n"
                     + $"Table: {Path.GetFileName(amlFilePath)}\n"
                     + $"OEM Revision: 0x{identity.OemRevision:X8}\n\n"
                     + fullOutput;
            }

            if (existsAfter)
            {
                return "Error: asl.exe did not remove the requested ACPI override revision "
                     + $"0x{identity.OemRevision:X8}.\n\n{fullOutput}";
            }

            return $"Error removing ACPI override (Exit Code {proc.ExitCode}):\n{fullOutput}";
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            try
            {
                string overridePath = GetOverridePath(amlFilePath);
                if (!File.Exists(overridePath))
                {
                    overridePath = PrepareOverrideTable(
                        amlFilePath,
                        out _,
                        out _);
                }

                AcpiTableIdentity identity = ReadAcpiTableIdentity(overridePath);
                bool removed = RemoveRegistryRevision(identity);
                return removed
                    ? $"Success: removed ACPI override revision 0x{identity.OemRevision:X8}.\n"
                      + "Restart Windows to restore the firmware ACPI table."
                    : "Error: 'asl.exe' was not found and no matching registry override was found.";
            }
            catch (Exception registryException)
            {
                return "Error: 'asl.exe' was not found and direct registry cleanup failed: "
                     + registryException.Message;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Error executing asl.exe: {ex.Message}";
        }
    }

    private static AcpiTableIdentity ReadAcpiTableIdentity(string amlFilePath)
    {
        byte[] table = File.ReadAllBytes(amlFilePath);
        if (table.Length < 36)
            throw new InvalidDataException("The selected file is not a valid ACPI table.");

        uint tableLength = BitConverter.ToUInt32(table, 4);
        if (tableLength < 36 || tableLength > table.Length)
            throw new InvalidDataException("The ACPI table length in the header is invalid.");

        string signature = ReadAcpiRegistryId(table, 0, 4);
        string oemId = ReadAcpiRegistryId(table, 10, 6);
        string oemTableId = ReadAcpiRegistryId(table, 16, 8);
        if (string.IsNullOrWhiteSpace(signature)
            || string.IsNullOrWhiteSpace(oemId)
            || string.IsNullOrWhiteSpace(oemTableId))
        {
            throw new InvalidDataException("The ACPI table identity fields are invalid.");
        }

        return new AcpiTableIdentity(
            signature,
            oemId,
            oemTableId,
            BitConverter.ToUInt32(table, 24));
    }

    private static bool RegistryOverrideExists(AcpiTableIdentity identity)
    {
        const string parametersPath =
            @"SYSTEM\CurrentControlSet\Services\ACPI\Parameters";
        using RegistryKey? parameters = Registry.LocalMachine.OpenSubKey(parametersPath);
        using RegistryKey? signatureKey = parameters?.OpenSubKey(identity.Signature);
        using RegistryKey? oemKey = signatureKey?.OpenSubKey(identity.OemId);
        using RegistryKey? tableKey = oemKey?.OpenSubKey(identity.OemTableId);
        using RegistryKey? revisionKey = tableKey?.OpenSubKey(
            identity.OemRevision.ToString("X8"));
        return revisionKey != null;
    }

    private static bool RemoveRegistryRevision(AcpiTableIdentity identity)
    {
        const string parametersPath =
            @"SYSTEM\CurrentControlSet\Services\ACPI\Parameters";
        using RegistryKey? parameters = Registry.LocalMachine.OpenSubKey(
            parametersPath,
            writable: true);
        if (parameters == null)
            return false;

        bool removeSignatureKey;
        using (RegistryKey? signatureKey = parameters.OpenSubKey(
            identity.Signature,
            writable: true))
        {
            if (signatureKey == null)
                return false;

            bool removeOemKey;
            using (RegistryKey? oemKey = signatureKey.OpenSubKey(
                identity.OemId,
                writable: true))
            {
                if (oemKey == null)
                    return false;

                bool removeTableKey;
                using (RegistryKey? tableKey = oemKey.OpenSubKey(
                    identity.OemTableId,
                    writable: true))
                {
                    if (tableKey == null)
                        return false;

                    string revision = identity.OemRevision.ToString("X8");
                    using RegistryKey? revisionKey = tableKey.OpenSubKey(revision);
                    if (revisionKey == null)
                        return false;

                    revisionKey.Close();
                    tableKey.DeleteSubKeyTree(revision, throwOnMissingSubKey: false);
                    removeTableKey = tableKey.SubKeyCount == 0 && tableKey.ValueCount == 0;
                }

                if (removeTableKey)
                    oemKey.DeleteSubKeyTree(identity.OemTableId, throwOnMissingSubKey: false);

                removeOemKey = oemKey.SubKeyCount == 0 && oemKey.ValueCount == 0;
            }

            if (removeOemKey)
                signatureKey.DeleteSubKeyTree(identity.OemId, throwOnMissingSubKey: false);

            removeSignatureKey = signatureKey.SubKeyCount == 0
                              && signatureKey.ValueCount == 0;
        }

        if (removeSignatureKey)
            parameters.DeleteSubKeyTree(identity.Signature, throwOnMissingSubKey: false);

        return true;
    }

    private static string ReadAcpiRegistryId(byte[] table, int offset, int length)
    {
        int valueLength = Array.IndexOf(table, (byte)0, offset, length);
        if (valueLength < 0)
            valueLength = offset + length;

        string value = Encoding.ASCII.GetString(table, offset, valueLength - offset);
        if (value.Contains('\\') || value.Any(char.IsControl))
            throw new InvalidDataException("The ACPI table identity fields are invalid.");

        // Preserve spaces before the NUL terminator: asl.exe uses them in the
        // registry key name. Removing them makes direct cleanup miss the key.
        return value;
    }
}
