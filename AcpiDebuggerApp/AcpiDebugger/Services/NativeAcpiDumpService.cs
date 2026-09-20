using AcpiDebugger.Models;
using Microsoft.Win32;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace AcpiDebugger.Services;

/// <summary>
/// Dumps the ACPI tables exposed by Windows without invoking acpidump.exe.
/// The registry contains the individual SSDT instances; the firmware-table
/// API fills in tables that are not represented in that registry view.
/// </summary>
public static class NativeAcpiDumpService
{
    // MSVC's 'ACPI' multi-character constant is laid out as 0x41435049.
    private const uint AcpiProvider = 0x41435049;
    private const string AcpiRegistryPath = "HARDWARE\\ACPI";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint EnumSystemFirmwareTables(
        uint firmwareTableProviderSignature,
        IntPtr firmwareTableEnumBuffer,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetSystemFirmwareTable(
        uint firmwareTableProviderSignature,
        uint firmwareTableId,
        IntPtr firmwareTableBuffer,
        uint bufferSize);

    public static ProcessResult Dump(string workspaceDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return new ProcessResult(1, "Native ACPI dumping is only available on Windows.");

        Directory.CreateDirectory(workspaceDirectory);
        var tables = new List<AcpiTableBlob>();
        var messages = new List<string>();

        try
        {
            tables.AddRange(ReadRegistryTables());
            messages.Add($"Registry ACPI tables: {tables.Count}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            messages.Add($"Registry ACPI read unavailable: {ex.Message}");
        }

        HashSet<string> knownSignatures = tables
            .Select(table => table.Signature)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            IReadOnlyList<AcpiTableBlob> firmwareTables = ReadFirmwareTables(knownSignatures, messages);
            tables.AddRange(firmwareTables);
            messages.Add($"Windows firmware-table API tables: {firmwareTables.Count}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            messages.Add($"Windows firmware-table API unavailable: {ex.Message}");
        }

        if (tables.Count == 0)
        {
            messages.Add("No ACPI tables were exposed by Windows.");
            return new ProcessResult(1, string.Join(Environment.NewLine, messages));
        }

        IReadOnlyList<string> files = WriteTables(workspaceDirectory, tables);
        messages.Add($"Native ACPI dump completed: {files.Count} tables.");
        messages.Add("Source: HKLM\\HARDWARE\\ACPI plus the Windows ACPI firmware-table API.");
        return new ProcessResult(0, string.Join(Environment.NewLine, messages));
    }

    private static IReadOnlyList<AcpiTableBlob> ReadRegistryTables()
    {
        using RegistryKey root = Registry.LocalMachine.OpenSubKey(AcpiRegistryPath)
            ?? throw new IOException($"Registry key HKLM\\{AcpiRegistryPath} was not found.");

        var tables = new List<AcpiTableBlob>();
        foreach (string subKeyName in root.GetSubKeyNames())
        {
            using RegistryKey? subKey = root.OpenSubKey(subKeyName);
            if (subKey is null) continue;
            int groupOrder = GetRegistryGroupOrder(subKeyName);
            ReadRegistryTables(subKey, $"HKLM\\{AcpiRegistryPath}\\{subKeyName}", groupOrder, tables);
        }

        return tables;
    }

    private static void ReadRegistryTables(
        RegistryKey key,
        string sourcePath,
        int groupOrder,
        ICollection<AcpiTableBlob> tables)
    {
        foreach (string valueName in key.GetValueNames())
        {
            if (key.GetValueKind(valueName) != RegistryValueKind.Binary) continue;
            if (key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                is not byte[] data) continue;
            if (!TryGetSignature(data, out string signature)) continue;

            tables.Add(new AcpiTableBlob(
                signature,
                data,
                $"{sourcePath}\\{valueName}",
                groupOrder));
        }

        foreach (string subKeyName in key.GetSubKeyNames())
        {
            using RegistryKey? subKey = key.OpenSubKey(subKeyName);
            if (subKey is null) continue;
            ReadRegistryTables(subKey, $"{sourcePath}\\{subKeyName}", groupOrder, tables);
        }
    }

    private static IReadOnlyList<AcpiTableBlob> ReadFirmwareTables(
        ISet<string> knownSignatures,
        ICollection<string> messages)
    {
        uint requiredSize = EnumSystemFirmwareTables(AcpiProvider, IntPtr.Zero, 0);
        if (requiredSize == 0)
            throw new IOException($"EnumSystemFirmwareTables failed with Win32 error {Marshal.GetLastWin32Error()}.");
        if (requiredSize % sizeof(uint) != 0)
            throw new InvalidOperationException("The ACPI table identifier buffer has an invalid size.");

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)requiredSize));
        try
        {
            uint returnedSize = EnumSystemFirmwareTables(AcpiProvider, buffer, requiredSize);
            if (returnedSize == 0)
                throw new IOException($"EnumSystemFirmwareTables failed with Win32 error {Marshal.GetLastWin32Error()}.");

            var result = new List<AcpiTableBlob>();
            for (int offset = 0; offset < returnedSize; offset += sizeof(uint))
            {
                uint tableId = unchecked((uint)Marshal.ReadInt32(buffer, offset));
                string signature = SignatureFromId(tableId);
                if (knownSignatures.Contains(signature)) continue;

                byte[] data = ReadFirmwareTable(tableId, signature);
                result.Add(new AcpiTableBlob(signature, data, "Windows ACPI firmware-table API", 1000));
                knownSignatures.Add(signature);
            }

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static byte[] ReadFirmwareTable(uint tableId, string signature)
    {
        uint requiredSize = GetSystemFirmwareTable(AcpiProvider, tableId, IntPtr.Zero, 0);
        if (requiredSize == 0)
            throw new IOException(
                $"GetSystemFirmwareTable failed for {signature} with Win32 error {Marshal.GetLastWin32Error()}.");

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)requiredSize));
        try
        {
            uint returnedSize = GetSystemFirmwareTable(AcpiProvider, tableId, buffer, requiredSize);
            if (returnedSize == 0 || returnedSize > requiredSize)
                throw new IOException(
                    $"GetSystemFirmwareTable returned an invalid size for {signature}.");

            byte[] data = new byte[returnedSize];
            Marshal.Copy(buffer, data, 0, checked((int)returnedSize));
            return data;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<string> WriteTables(
        string workspaceDirectory,
        IEnumerable<AcpiTableBlob> tables)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        IEnumerable<AcpiTableBlob> orderedTables = tables
            .OrderBy(table => GetSignatureOrder(table.Signature))
            .ThenBy(table => table.GroupOrder)
            .ThenBy(table => table.Source, StringComparer.OrdinalIgnoreCase);

        foreach (AcpiTableBlob table in orderedTables)
        {
            int instance = counts.TryGetValue(table.Signature, out int current) ? current : 0;
            counts[table.Signature] = instance + 1;
            string stem = table.Signature.ToLowerInvariant();
            string fileName = instance == 0
                ? $"{stem}.dat"
                : $"{stem}-{instance}.dat";
            string path = Path.Combine(workspaceDirectory, fileName);
            File.WriteAllBytes(path, table.Data);
            files.Add(path);
        }

        return files;
    }

    private static bool TryGetSignature(byte[] data, out string signature)
    {
        signature = string.Empty;
        if (data.Length < 8) return false;

        signature = Encoding.ASCII.GetString(data, 0, 4);
        if (signature.Any(character => character < 0x20 || character > 0x7E))
        {
            signature = string.Empty;
            return false;
        }

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (declaredLength < 8 || declaredLength > data.Length)
        {
            signature = string.Empty;
            return false;
        }

        return true;
    }

    private static string SignatureFromId(uint tableId)
    {
        byte[] bytes = BitConverter.GetBytes(tableId);
        return Encoding.ASCII.GetString(bytes, 0, 4);
    }

    private static int GetRegistryGroupOrder(string name)
    {
        if (name.Equals("DSDT", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.Equals("FACS", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Equals("FADT", StringComparison.OrdinalIgnoreCase)) return 2;
        if (name.Equals("RSDT", StringComparison.OrdinalIgnoreCase)) return 3;
        if (name.Equals("SSDT", StringComparison.OrdinalIgnoreCase)) return 10;
        if (name.StartsWith("SSD", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name[3..], out int numeric)) return 10 + numeric;
        if (name.StartsWith("SSD", StringComparison.OrdinalIgnoreCase)
            && name.Length == 4 && name[3] is >= 'A' and <= 'Z') return 20 + name[3] - 'A';
        return 100;
    }

    private static int GetSignatureOrder(string signature) => signature switch
    {
        "DSDT" => 0,
        "FACS" => 1,
        "FACP" => 2,
        "XSDT" => 3,
        "RSDT" => 4,
        "SSDT" => 10,
        _ => 100
    };

    private sealed record AcpiTableBlob(
        string Signature,
        byte[] Data,
        string Source,
        int GroupOrder);
}
