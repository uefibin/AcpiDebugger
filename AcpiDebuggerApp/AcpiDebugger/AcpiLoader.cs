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

        // Windows selects the table with the highest OEM Revision. The staged
        // image and its registry revision key must therefore be newer than the
        // matching firmware table.
        overrideRevision = originalRevision + 1;
        Array.Copy(BitConverter.GetBytes(overrideRevision), 0, table, 24, sizeof(uint));

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

    public static string LoadAcpiTable(string amlFilePath)
    {
        if (!IsRunningAsAdministrator())
        {
            return "Error: Administrator privileges are required.\n"
                 + "The ACPI override table must be written to HKLM. "
                 + "Please restart AcpiDebugger as administrator.";
        }

        // 1. Check Test Mode
        if (!IsTestSigningEnabled())
        {
             return "Error: System is not in Test Mode (TestSigning). ACPI tables cannot be loaded.\nPlease enable Test Signing (bcdedit /set testsigning on) and reboot.";
        }

        try
        {
            if (HasAnyStagedOverride())
            {
                return "Error: another ACPI override is already staged.\n"
                     + "Use Remove all overrides, restart Windows, and run Dump before loading a new override.";
            }

            string overridePath = PrepareOverrideTable(
                amlFilePath,
                out uint originalRevision,
                out uint overrideRevision);

            AcpiTableIdentity identity = ReadAcpiTableIdentity(overridePath);
            byte[] overrideTable = ReadValidatedTable(overridePath);
            AcpiTableIdentity firmwareIdentity = identity with
            {
                OemRevision = originalRevision
            };
            byte[] firmwareTable = ReadLiveFirmwareBaseTable(firmwareIdentity, amlFilePath);
            ValidateTableForOverride(firmwareTable, "The live firmware table");
            if (firmwareTable.AsSpan().SequenceEqual(overrideTable))
                throw new InvalidOperationException("The override does not differ from the live firmware table.");

            RemoveSystemOverrideBeforeRegistryStage(identity);
            int valueCount = StageRegistryDelta(identity, firmwareTable, overrideTable);
            if (!RegistryOverrideExists(identity))
                throw new IOException("The staged ACPI override registry key could not be verified.");

            return "Success: ACPI override staged by the integrated loader.\n"
                 + $"OEM Revision: 0x{originalRevision:X8} -> 0x{overrideRevision:X8}\n"
                 + $"Override file: {overridePath}\n"
                 + $"Registry delta values: {valueCount}\n"
                 + "IMPORTANT: Restart Windows before verifying the ACPI namespace.";
        }
        catch (Exception ex)
        {
             return $"Error staging the ACPI override: {ex.Message}";
        }
    }

    private static byte[] ReadValidatedTable(string path)
    {
        byte[] table = File.ReadAllBytes(path);
        ValidateTableForOverride(table, "The selected override table");
        return table;
    }

    private static void ValidateTableForOverride(byte[] table, string description)
    {
        if (table.Length < 36)
            throw new InvalidDataException($"{description} is shorter than an ACPI table header.");

        uint declaredLength = BitConverter.ToUInt32(table, 4);
        if (declaredLength != table.Length)
        {
            throw new InvalidDataException(
                $"{description} length is invalid: header={declaredLength}, file={table.Length}.");
        }

        int checksum = 0;
        foreach (byte value in table)
            checksum = (checksum + value) & 0xFF;
        if (checksum != 0)
            throw new InvalidDataException($"{description} has an invalid ACPI checksum.");
    }

    public static string RemoveAcpiTable(
        string amlFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsRunningAsAdministrator())
        {
            return "Error: Administrator privileges are required.\n"
                 + "Please restart AcpiDebugger as administrator.";
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            bool fileRemoved = RemoveSystemOverride(identity);
            bool registryRemoved = RemoveRegistryRevision(identity);
            AcpiTableIdentity legacyIdentity = ReadAcpiTableIdentity(overridePath, normalizeForRegistry: false);
            if (legacyIdentity != identity)
                registryRemoved |= RemoveRegistryRevision(legacyIdentity);
            AcpiTableIdentity trimmedIdentity = NormalizeLegacyTrimmedIdentity(legacyIdentity);
            if (trimmedIdentity != identity && trimmedIdentity != legacyIdentity)
                registryRemoved |= RemoveRegistryRevision(trimmedIdentity);
            registryRemoved |= RemoveLegacyOriginalRevision(identity);
            if (fileRemoved || registryRemoved)
            {
                return "Success: ACPI override was removed.\n"
                     + $"Table: {Path.GetFileName(amlFilePath)}\n"
                     + $"OEM Revision: 0x{identity.OemRevision:X8}\n"
                     + "The currently loaded ACPI namespace is unchanged. "
                     + "Restart Windows to restore the firmware ACPI table.";
            }

            return "Error: no matching staged ACPI override was found.\n"
                 + $"Table: {Path.GetFileName(amlFilePath)}\n"
                 + $"OEM Revision: 0x{identity.OemRevision:X8}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Error removing the ACPI override: {ex.Message}";
        }
    }

    public static string RemoveAllAcpiOverrides(CancellationToken cancellationToken = default)
    {
        if (!IsRunningAsAdministrator())
        {
            return "Error: Administrator privileges are required.\n"
                 + "Please restart AcpiDebugger as administrator.";
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            int registryRevisions = RemoveAllRegistryOverrideRevisions(cancellationToken);
            bool systemFileRemoved = RemoveAnySystemOverrideFile();

            if (registryRevisions == 0 && !systemFileRemoved)
                return "Error: no staged ACPI overrides were found.";

            return "Success: all staged ACPI overrides were removed.\n"
                 + $"Registry revisions removed: {registryRevisions}\n"
                 + $"acpitabl.dat removed: {(systemFileRemoved ? "Yes" : "No")}\n"
                 + "Restart Windows to restore the firmware ACPI tables.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Error removing ACPI overrides: {ex.Message}";
        }
    }

    private static int RemoveAllRegistryOverrideRevisions(CancellationToken cancellationToken)
    {
        const string parametersPath = @"SYSTEM\CurrentControlSet\Services\ACPI\Parameters";
        using RegistryKey? parameters = Registry.LocalMachine.OpenSubKey(parametersPath, writable: true);
        if (parameters == null)
            return 0;

        int removed = 0;
        foreach (string signatureName in parameters.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (signatureName.Equals("WakeUp", StringComparison.OrdinalIgnoreCase))
                continue;

            using RegistryKey? signatureKey = parameters.OpenSubKey(signatureName, writable: true);
            if (signatureKey == null) continue;
            foreach (string oemName in signatureKey.GetSubKeyNames())
            {
                using RegistryKey? oemKey = signatureKey.OpenSubKey(oemName, writable: true);
                if (oemKey == null) continue;
                foreach (string tableName in oemKey.GetSubKeyNames())
                {
                    using RegistryKey? tableKey = oemKey.OpenSubKey(tableName, writable: true);
                    if (tableKey == null) continue;
                    foreach (string revisionName in tableKey.GetSubKeyNames())
                    {
                        using RegistryKey? revisionKey = tableKey.OpenSubKey(revisionName);
                        if (revisionKey == null || !IsOverrideRevisionKey(revisionName, revisionKey))
                            continue;

                        revisionKey.Close();
                        tableKey.DeleteSubKeyTree(revisionName, throwOnMissingSubKey: false);
                        removed++;
                    }
                }
            }
        }

        // Only prune branches that became empty; unrelated ACPI parameters remain.
        foreach (string signatureName in parameters.GetSubKeyNames())
        {
            if (signatureName.Equals("WakeUp", StringComparison.OrdinalIgnoreCase))
                continue;
            PruneEmptyOverrideBranch(parameters, signatureName);
        }
        parameters.Flush();
        return removed;
    }

    private static bool HasAnyStagedOverride()
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDirectory)
            && File.Exists(Path.Combine(windowsDirectory, "System32", "acpitabl.dat")))
            return true;

        const string parametersPath = @"SYSTEM\CurrentControlSet\Services\ACPI\Parameters";
        using RegistryKey? parameters = Registry.LocalMachine.OpenSubKey(parametersPath);
        if (parameters == null)
            return false;

        foreach (string signatureName in parameters.GetSubKeyNames())
        {
            if (signatureName.Equals("WakeUp", StringComparison.OrdinalIgnoreCase))
                continue;
            using RegistryKey? signatureKey = parameters.OpenSubKey(signatureName);
            if (signatureKey == null) continue;
            foreach (string oemName in signatureKey.GetSubKeyNames())
            {
                using RegistryKey? oemKey = signatureKey.OpenSubKey(oemName);
                if (oemKey == null) continue;
                foreach (string tableName in oemKey.GetSubKeyNames())
                {
                    using RegistryKey? tableKey = oemKey.OpenSubKey(tableName);
                    if (tableKey == null) continue;
                    foreach (string revisionName in tableKey.GetSubKeyNames())
                    {
                        using RegistryKey? revisionKey = tableKey.OpenSubKey(revisionName);
                        if (revisionKey != null && IsOverrideRevisionKey(revisionName, revisionKey))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool IsOverrideRevisionKey(string revisionName, RegistryKey key)
    {
        if (revisionName.Length != 8
            || !uint.TryParse(revisionName, System.Globalization.NumberStyles.HexNumber, null, out _))
            return false;

        return key.GetValueNames().Any(name =>
            name.Equals("Action", StringComparison.OrdinalIgnoreCase)
            || (name.Length == 8
                && uint.TryParse(name, System.Globalization.NumberStyles.HexNumber, null, out _)
                && key.GetValueKind(name) == RegistryValueKind.Binary));
    }

    private static void PruneEmptyOverrideBranch(RegistryKey parameters, string signatureName)
    {
        using (RegistryKey? signatureKey = parameters.OpenSubKey(signatureName, writable: true))
        {
            if (signatureKey == null) return;
            foreach (string oemName in signatureKey.GetSubKeyNames())
            {
                using (RegistryKey? oemKey = signatureKey.OpenSubKey(oemName, writable: true))
                {
                    if (oemKey == null) continue;
                    foreach (string tableName in oemKey.GetSubKeyNames())
                    {
                        using RegistryKey? tableKey = oemKey.OpenSubKey(tableName);
                        if (tableKey != null && tableKey.SubKeyCount == 0 && tableKey.ValueCount == 0)
                        {
                            tableKey.Close();
                            oemKey.DeleteSubKeyTree(tableName, throwOnMissingSubKey: false);
                        }
                    }
                }

                using RegistryKey? remainingOemKey = signatureKey.OpenSubKey(oemName);
                if (remainingOemKey != null
                    && remainingOemKey.SubKeyCount == 0
                    && remainingOemKey.ValueCount == 0)
                {
                    remainingOemKey.Close();
                    signatureKey.DeleteSubKeyTree(oemName, throwOnMissingSubKey: false);
                }
            }
        }

        using RegistryKey? remainingSignatureKey = parameters.OpenSubKey(signatureName);
        if (remainingSignatureKey != null
            && remainingSignatureKey.SubKeyCount == 0
            && remainingSignatureKey.ValueCount == 0)
        {
            remainingSignatureKey.Close();
            parameters.DeleteSubKeyTree(signatureName, throwOnMissingSubKey: false);
        }
    }

    private static bool RemoveAnySystemOverrideFile()
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDirectory))
            return false;

        string path = Path.Combine(windowsDirectory, "System32", "acpitabl.dat");
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return !File.Exists(path);
    }

    private static byte[] ReadLiveFirmwareBaseTable(
        AcpiTableIdentity target,
        string sourcePath)
    {
        using RegistryKey root = Registry.LocalMachine.OpenSubKey(@"HARDWARE\ACPI")
            ?? throw new IOException("HKLM\\HARDWARE\\ACPI is unavailable.");
        var candidates = new List<byte[]>();
        CollectMatchingHardwareTables(root, target, candidates);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "No live ACPI table matches the override identity. Remove all overrides, restart Windows, run Dump, and try again.");
        }

        if (candidates.Count == 1)
            return candidates[0];

        string directory = Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
        string workspaceBase = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(sourcePath) + ".dat");
        if (File.Exists(workspaceBase))
        {
            byte[] workspaceBytes = File.ReadAllBytes(workspaceBase);
            List<byte[]> exact = candidates
                .Where(candidate => candidate.AsSpan().SequenceEqual(workspaceBytes))
                .ToList();
            if (exact.Count == 1)
                return exact[0];
        }

        throw new InvalidOperationException(
            $"{candidates.Count} live ACPI tables have the same identity. "
            + "The firmware base is ambiguous, so loading was blocked to prevent an invalid boot override.");
    }

    private static void CollectMatchingHardwareTables(
        RegistryKey key,
        AcpiTableIdentity target,
        ICollection<byte[]> matches)
    {
        foreach (string valueName in key.GetValueNames())
        {
            if (key.GetValueKind(valueName) != RegistryValueKind.Binary
                || key.GetValue(valueName) is not byte[] data
                || data.Length < 36)
                continue;

            try
            {
                AcpiTableIdentity identity = ReadAcpiTableIdentity(data);
                if (identity == target)
                    matches.Add(data);
            }
            catch (InvalidDataException)
            {
                // Ignore non-table binary ACPI registry values.
            }
        }

        foreach (string subKeyName in key.GetSubKeyNames())
        {
            using RegistryKey? child = key.OpenSubKey(subKeyName);
            if (child != null)
                CollectMatchingHardwareTables(child, target, matches);
        }
    }

    private static int StageRegistryDelta(
        AcpiTableIdentity identity,
        byte[] firmwareTable,
        byte[] overrideTable)
    {
        const string parametersPath = @"SYSTEM\CurrentControlSet\Services\ACPI\Parameters";
        const int maximumValueSize = 0x1C00;
        const int maximumRecordStart = 0x1B80;
        IReadOnlyList<byte[]> values = BuildRegistryDeltaValues(
            firmwareTable,
            overrideTable,
            maximumValueSize,
            maximumRecordStart);
        if (values.Count == 0)
            throw new InvalidOperationException("The override does not differ from the firmware table.");
        VerifyRegistryDelta(firmwareTable, overrideTable, values);

        try
        {
            using RegistryKey parameters = Registry.LocalMachine.CreateSubKey(parametersPath, writable: true)
                ?? throw new UnauthorizedAccessException("Could not open the ACPI Parameters registry key for writing.");
            using RegistryKey signatureKey = parameters.CreateSubKey(identity.Signature, writable: true);
            using RegistryKey oemKey = signatureKey.CreateSubKey(identity.OemId, writable: true);
            using RegistryKey tableKey = oemKey.CreateSubKey(identity.OemTableId, writable: true);
            string revision = identity.OemRevision.ToString("X8");

            using RegistryKey revisionKey = tableKey.CreateSubKey(revision, writable: true);
            foreach (string valueName in revisionKey.GetValueNames())
                revisionKey.DeleteValue(valueName, throwOnMissingValue: false);

            for (int index = 0; index < values.Count; index++)
            {
                string valueName = index.ToString("X8");
                revisionKey.SetValue(valueName, values[index], RegistryValueKind.Binary);
                if (revisionKey.GetValue(valueName) is not byte[] stored
                    || !stored.AsSpan().SequenceEqual(values[index]))
                {
                    throw new IOException($"Registry delta value {valueName} could not be verified.");
                }
            }

            revisionKey.SetValue("Action", 0, RegistryValueKind.DWord);
            // The UI can reboot immediately after this method returns. Force the
            // staged delta through the registry cache before offering that action.
            revisionKey.Flush();
            tableKey.Flush();
            oemKey.Flush();
            signatureKey.Flush();
            parameters.Flush();
            return values.Count;
        }
        catch
        {
            // Do not leave a half-written revision that would be consumed by
            // Acpi.sys on the next boot. Cleanup is best-effort; preserve the
            // original staging exception for the caller.
            try { RemoveRegistryRevision(identity); }
            catch { }
            throw;
        }
    }

    private static string GetRegistryRevisionPath(AcpiTableIdentity identity)
    {
        return $@"SYSTEM\CurrentControlSet\Services\ACPI\Parameters\{identity.Signature}\{identity.OemId}\{identity.OemTableId}\{identity.OemRevision:X8}";
    }

    private static IReadOnlyList<byte[]> BuildRegistryDeltaValues(
        byte[] original,
        byte[] modified,
        int maximumValueSize,
        int maximumRecordStart)
    {
        if (maximumRecordStart < 8 || maximumRecordStart > maximumValueSize - 8)
            throw new ArgumentOutOfRangeException(nameof(maximumRecordStart));

        _ = original;

        // Microsoft asl.exe uses a full replacement stream when the update
        // table has the required higher OEM Revision. The first zero-length
        // record establishes the final table size, then records starting at
        // offset zero carry every byte of the new image. A sparse byte delta
        // reconstructs correctly in user mode but is not the format Acpi.sys
        // expects for this revisioned replacement and causes bug check 0xA5.
        var records = new List<(int Offset, int Length)>
        {
            (modified.Length, 0),
            (0, modified.Length)
        };

        var values = new List<byte[]>();
        using var current = new MemoryStream(maximumValueSize);
        foreach ((int offset, int length) in records)
        {
            int consumed = 0;
            do
            {
                // Match Microsoft asl.exe: a record header must begin no later
                // than 0x1B80, while its payload may extend to 0x1C00.
                if (current.Length + 8 > maximumRecordStart)
                {
                    values.Add(current.ToArray());
                    current.SetLength(0);
                }

                int available = maximumValueSize - checked((int)current.Length) - 8;
                int chunkLength = Math.Min(length - consumed, available);
                current.Write(BitConverter.GetBytes(offset + consumed));
                current.Write(BitConverter.GetBytes(chunkLength));
                if (chunkLength > 0)
                    current.Write(modified, offset + consumed, chunkLength);
                consumed += chunkLength;

                if (current.Length == maximumValueSize)
                {
                    values.Add(current.ToArray());
                    current.SetLength(0);
                }
            }
            while (consumed < length);
        }

        if (current.Length > 0)
            values.Add(current.ToArray());
        return values;
    }

    private static void VerifyRegistryDelta(
        byte[] original,
        byte[] expected,
        IReadOnlyList<byte[]> values)
    {
        var reconstructed = new List<byte>(original);
        foreach (byte[] value in values)
        {
            int cursor = 0;
            while (cursor < value.Length)
            {
                if (value.Length - cursor < 8)
                    throw new InvalidDataException("A registry delta record header is truncated.");

                uint offset = BitConverter.ToUInt32(value, cursor);
                uint length = BitConverter.ToUInt32(value, cursor + 4);
                cursor += 8;
                if (length > value.Length - cursor)
                    throw new InvalidDataException("A registry delta record payload is truncated.");

                if (length == 0)
                {
                    if (offset > int.MaxValue)
                        throw new InvalidDataException("A registry delta resize offset is invalid.");
                    int newLength = (int)offset;
                    if (newLength < reconstructed.Count)
                        reconstructed.RemoveRange(newLength, reconstructed.Count - newLength);
                    else if (newLength > reconstructed.Count)
                        reconstructed.AddRange(new byte[newLength - reconstructed.Count]);
                }
                else
                {
                    long end = (long)offset + length;
                    if (end > reconstructed.Count || end > int.MaxValue)
                        throw new InvalidDataException("A registry delta write exceeds the table size.");
                    value.AsSpan(cursor, (int)length).CopyTo(
                        CollectionsMarshal.AsSpan(reconstructed).Slice((int)offset, (int)length));
                }

                cursor += (int)length;
            }
        }

        if (!CollectionsMarshal.AsSpan(reconstructed).SequenceEqual(expected))
            throw new InvalidDataException("The generated registry delta does not reconstruct the override table.");
    }

    private static bool RemoveSystemOverride(AcpiTableIdentity identity)
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDirectory))
            return false;

        string path = Path.Combine(windowsDirectory, "System32", "acpitabl.dat");
        if (!File.Exists(path) || ReadAcpiTableIdentity(path) != identity)
            return false;

        File.Delete(path);
        return !File.Exists(path);
    }

    private static void RemoveSystemOverrideBeforeRegistryStage(AcpiTableIdentity identity)
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDirectory))
            return;

        string path = Path.Combine(windowsDirectory, "System32", "acpitabl.dat");
        if (!File.Exists(path))
            return;

        if (ReadAcpiTableIdentity(path) != identity)
        {
            throw new InvalidOperationException(
                $"A different acpitabl.dat override still exists at {path}. Remove it before staging a registry override.");
        }

        File.Delete(path);
        if (File.Exists(path))
            throw new IOException($"Could not remove the previous system override: {path}");
    }

    private static AcpiTableIdentity ReadAcpiTableIdentity(
        string amlFilePath,
        bool normalizeForRegistry = true)
    {
        return ReadAcpiTableIdentity(File.ReadAllBytes(amlFilePath), normalizeForRegistry);
    }

    private static AcpiTableIdentity ReadAcpiTableIdentity(
        byte[] table,
        bool normalizeForRegistry = true)
    {
        if (table.Length < 36)
            throw new InvalidDataException("The selected file is not a valid ACPI table.");

        uint tableLength = BitConverter.ToUInt32(table, 4);
        if (tableLength < 36 || tableLength > table.Length)
            throw new InvalidDataException("The ACPI table length in the header is invalid.");

        string signature = ReadAcpiRegistryId(table, 0, 4, normalizeForRegistry);
        string oemId = ReadAcpiRegistryId(table, 10, 6, normalizeForRegistry);
        string oemTableId = ReadAcpiRegistryId(table, 16, 8, normalizeForRegistry);
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

    private static AcpiTableIdentity NormalizeLegacyTrimmedIdentity(AcpiTableIdentity identity)
    {
        return new AcpiTableIdentity(
            identity.Signature.TrimEnd(' ').Replace(' ', '_'),
            identity.OemId.TrimEnd(' ').Replace(' ', '_'),
            identity.OemTableId.TrimEnd(' ').Replace(' ', '_'),
            identity.OemRevision);
    }

    private static bool RemoveLegacyOriginalRevision(AcpiTableIdentity identity)
    {
        if (identity.OemRevision == 0)
            return false;

        return RemoveRegistryRevision(identity with
        {
            OemRevision = identity.OemRevision - 1
        });
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

    private static string ReadAcpiRegistryId(
        byte[] table,
        int offset,
        int length,
        bool normalizeForRegistry)
    {
        int valueLength = Array.IndexOf(table, (byte)0, offset, length);
        if (valueLength < 0)
            valueLength = offset + length;

        string value = Encoding.ASCII.GetString(table, offset, valueLength - offset);
        if (value.Contains('\\') || value.Any(char.IsControl))
            throw new InvalidDataException("The ACPI table identity fields are invalid.");

        if (!normalizeForRegistry)
            return value;

        // Match Microsoft asl.exe /loadtable and HKLM\HARDWARE\ACPI: every
        // fixed-width padding space is represented by an underscore, including
        // spaces at the end of OEM ID and OEM Table ID.
        return value.Replace(' ', '_');
    }
}
