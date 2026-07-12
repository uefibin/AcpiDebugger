using AcpiDebugger.Models;
using System.IO;
using System.Text;

namespace AcpiDebugger.Services;

public static class AcpiTableService
{
    public static AcpiTableInfo Read(string filePath)
    {
        byte[] header = File.ReadAllBytes(filePath);
        if (header.Length < 36)
            throw new InvalidDataException("Invalid ACPI table header.");

        uint length = BitConverter.ToUInt32(header, 4);
        if (length < 36 || length > header.Length)
            throw new InvalidDataException("The ACPI table length in the header is invalid.");

        string signature = Encoding.ASCII.GetString(header, 0, 4);
        return new AcpiTableInfo(
            signature,
            GetSignatureDescription(signature),
            Encoding.ASCII.GetString(header, 10, 6).Trim(),
            Encoding.ASCII.GetString(header, 16, 8).Trim(),
            BitConverter.ToUInt32(header, 24),
            Encoding.ASCII.GetString(header, 28, 4).Trim(),
            BitConverter.ToUInt32(header, 32),
            header[8],
            length,
            header[9],
            header.Take((int)length).Aggregate(0, (sum, value) => (sum + value) & 0xFF) == 0,
            Path.GetFileName(filePath));
    }

    public static string GetSummary(string filePath)
    {
        try
        {
            AcpiTableInfo info = Read(filePath);
            return $"OEM: {info.OemId}, Table: {info.TableId}";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetSignatureDescription(string signature) => signature switch
    {
        "DSDT" => "Differentiated System Description Table",
        "SSDT" => "Secondary System Description Table",
        "FACP" => "Fixed ACPI Description Table",
        "APIC" => "Multiple APIC Description Table",
        "HPET" => "High Precision Event Timer Table",
        "MCFG" => "PCI Express Memory Mapped Configuration",
        "TPM2" => "Trusted Platform Module 2 Table",
        _ => "ACPI firmware table"
    };
}
