using AcpiDebugger.Models;
using System.Buffers.Binary;
using System.IO;

namespace AcpiDebugger.Services;

public static class AcpiTableSpecificService
{
    public static AcpiTableSpecificInfo Read(string filePath, string signature)
    {
        byte[] data = File.ReadAllBytes(filePath);
        if (data.Length < 36)
            throw new InvalidDataException("Invalid ACPI table header.");

        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4)));
        if (length < 36 || length > data.Length)
            throw new InvalidDataException("The ACPI table length in the header is invalid.");

        ReadOnlySpan<byte> table = data.AsSpan(0, length);
        return signature switch
        {
            "APIC" => ParseApic(table),
            "BGRT" => ParseBgrt(filePath),
            "DBG2" => Fields("Debug Port Table 2", ("Info offset", U32(table, 36)), ("Info structure count", U32(table, 40))),
            "DBGP" => ParseDbgp(table),
            "FACP" => ParseFacp(table),
            "FPDT" => ParseFpdt(table),
            "HPET" => ParseHpet(table),
            "LPIT" => ParseLpit(table),
            "MCFG" => ParseMcfg(table),
            "TPM2" => ParseTpm2(table),
            "UEFI" => ParseUefi(table),
            "WSMT" => ParseWsmt(table),
            "XSDT" => ParseXsdt(table),
            "DSDT" or "SSDT" => Fields("AML definition block", ("AML bytecode length", $"{length - 36:N0} bytes"), ("Payload preview", Hex(table, 36, 24))),
            _ => ParseGeneric(table, signature)
        };
    }

    private static AcpiTableSpecificInfo ParseBgrt(string filePath)
    {
        BgrtTableInfo info = BgrtTableService.Read(filePath);
        return Fields("Boot Graphics Resource Table",
            ("Version", info.Version.ToString()),
            ("Status", info.ImageDisplayed ? $"0x{info.Status:X2} — image displayed" : $"0x{info.Status:X2} — image not displayed"),
            ("Orientation", info.OrientationDescription),
            ("Image type", info.ImageType == 0 ? "Bitmap (0)" : $"Reserved ({info.ImageType})"),
            ("Image address", $"0x{info.ImageAddress:X16}"),
            ("Image offset X", $"{info.ImageOffsetX} (0x{info.ImageOffsetX:X8})"),
            ("Image offset Y", $"{info.ImageOffsetY} (0x{info.ImageOffsetY:X8})"));
    }

    private static AcpiTableSpecificInfo ParseApic(ReadOnlySpan<byte> table)
    {
        var counts = new Dictionary<byte, int>();
        int offset = 44;
        while (offset + 2 <= table.Length)
        {
            byte type = table[offset];
            int size = table[offset + 1];
            if (size < 2 || offset + size > table.Length) break;
            counts[type] = counts.GetValueOrDefault(type) + 1;
            offset += size;
        }

        var fields = new List<(string, string)>
        {
            ("Local APIC address", Hex32(table, 36)),
            ("Flags", $"{Hex32(table, 40)} — PC-AT compatible: {(Read32(table, 40) & 1) != 0}"),
            ("Interrupt structures", counts.Values.Sum().ToString())
        };
        string[] names = ["Processor Local APIC", "I/O APIC", "Interrupt Source Override", "NMI Source", "Local APIC NMI", "Local APIC Address Override", "I/O SAPIC", "Local SAPIC", "Platform Interrupt Source", "Processor Local x2APIC", "Local x2APIC NMI"];
        foreach ((byte type, int count) in counts.OrderBy(item => item.Key))
            fields.Add((type < names.Length ? names[type] : $"Structure type {type}", count.ToString()));
        return Fields("Multiple APIC Description Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseDbgp(ReadOnlySpan<byte> table) =>
        Fields("Debug Port Table",
            ("Interface type", table.Length > 36 ? $"{table[36]} ({DebugPortType(table[36])})" : "Unavailable"),
            ("Address space", GasSpace(table, 40)),
            ("Register width", ByteValue(table, 41)),
            ("Access size", ByteValue(table, 43)),
            ("Base address", Hex64(table, 44)));

    private static AcpiTableSpecificInfo ParseFacp(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>
        {
            ("Firmware control", Hex32(table, 36)), ("DSDT address", Hex32(table, 40)),
            ("Preferred PM profile", table.Length > 45 ? $"{table[45]} ({PmProfile(table[45])})" : "Unavailable"),
            ("SCI interrupt", U16(table, 46)), ("SMI command port", Hex32(table, 48)),
            ("ACPI enable value", Hex8(table, 52)), ("ACPI disable value", Hex8(table, 53)),
            ("PM1a event block", Hex32(table, 56)), ("PM1a control block", Hex32(table, 64)),
            ("PM timer block", Hex32(table, 76)), ("Flags", Hex32(table, 112))
        };
        if (table.Length >= 148)
        {
            fields.Add(("X firmware control", Hex64(table, 132)));
            fields.Add(("X DSDT address", Hex64(table, 140)));
        }
        return Fields("Fixed ACPI Description Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseFpdt(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        int offset = 36;
        int index = 0;
        while (offset + 4 <= table.Length)
        {
            ushort type = Read16(table, offset);
            int size = table[offset + 2];
            byte revision = table[offset + 3];
            if (size < 4 || offset + size > table.Length) break;
            fields.Add(($"Record {index++}", $"Type {type}, length {size}, revision {revision}"));
            offset += size;
        }
        fields.Insert(0, ("Record count", index.ToString()));
        return Fields("Firmware Performance Data Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseHpet(ReadOnlySpan<byte> table) =>
        Fields("High Precision Event Timer Table",
            ("Event timer block ID", Hex32(table, 36)), ("Address space", GasSpace(table, 40)),
            ("Register width", ByteValue(table, 41)), ("Base address", Hex64(table, 44)),
            ("HPET number", ByteValue(table, 52)), ("Minimum clock tick", U16(table, 53)),
            ("Page protection", Hex8(table, 55)));

    private static AcpiTableSpecificInfo ParseLpit(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        int offset = 36;
        int count = 0;
        while (offset + 8 <= table.Length)
        {
            uint type = Read32(table, offset);
            int size = checked((int)Read32(table, offset + 4));
            if (size < 8 || offset + size > table.Length) break;
            fields.Add(($"Descriptor {count++}", $"Type {type}, length {size}"));
            offset += size;
        }
        fields.Insert(0, ("Descriptor count", count.ToString()));
        return Fields("Low Power Idle Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseMcfg(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        int count = 0;
        for (int offset = 44; offset + 16 <= table.Length; offset += 16)
        {
            ulong address = Read64(table, offset);
            ushort segment = Read16(table, offset + 8);
            byte startBus = table[offset + 10];
            byte endBus = table[offset + 11];
            fields.Add(($"Allocation {count++}", $"Base 0x{address:X16}, segment {segment}, buses {startBus:X2}-{endBus:X2}"));
        }
        fields.Insert(0, ("Allocation count", count.ToString()));
        return Fields("PCI Express Memory Mapped Configuration", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseTpm2(ReadOnlySpan<byte> table) =>
        Fields("Trusted Platform Module 2 Table",
            ("Platform class", U16(table, 36)), ("Control area", Hex64(table, 40)),
            ("Start method", $"{U32(table, 48)} ({TpmStartMethod(Read32(table, 48))})"),
            ("Method parameters", Hex(table, 52, Math.Min(12, table.Length - 52))));

    private static AcpiTableSpecificInfo ParseUefi(ReadOnlySpan<byte> table)
    {
        string identifier = table.Length >= 52 ? new Guid(table.Slice(36, 16)).ToString() : "Unavailable";
        return Fields("UEFI ACPI Data Table", ("Identifier", identifier), ("Data offset", U16(table, 52)),
            ("Data length", table.Length >= 54 ? $"{table.Length - Read16(table, 52):N0} bytes" : "Unavailable"));
    }

    private static AcpiTableSpecificInfo ParseWsmt(ReadOnlySpan<byte> table)
    {
        uint flags = Read32(table, 36);
        return Fields("Windows SMM Security Mitigations Table", ("Protection flags", $"0x{flags:X8}"),
            ("Fixed communication buffers", ((flags & 1) != 0).ToString()),
            ("Nested pointer protection", ((flags & 2) != 0).ToString()),
            ("System resource protection", ((flags & 4) != 0).ToString()));
    }

    private static AcpiTableSpecificInfo ParseXsdt(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)> { ("Entry count", ((table.Length - 36) / 8).ToString()) };
        for (int offset = 36, index = 0; offset + 8 <= table.Length; offset += 8, index++)
            fields.Add(($"Table address {index}", Hex64(table, offset)));
        return Fields("Extended System Description Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseGeneric(ReadOnlySpan<byte> table, string signature) =>
        Fields($"{signature} table — generic payload view",
            ("Payload length", $"{table.Length - 36:N0} bytes"),
            ("Payload preview", Hex(table, 36, Math.Min(64, table.Length - 36))));

    private static AcpiTableSpecificInfo Fields(string summary, params (string Name, string Value)[] values) =>
        new(summary, values.Select(value => new AcpiSpecificField(value.Name, value.Value)).ToArray());

    private static byte Read8(ReadOnlySpan<byte> data, int offset) => offset < data.Length ? data[offset] : (byte)0;
    private static ushort Read16(ReadOnlySpan<byte> data, int offset) => offset + 2 <= data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : (ushort)0;
    private static uint Read32(ReadOnlySpan<byte> data, int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : 0;
    private static ulong Read64(ReadOnlySpan<byte> data, int offset) => offset + 8 <= data.Length ? BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]) : 0;
    private static string ByteValue(ReadOnlySpan<byte> data, int offset) => offset < data.Length ? data[offset].ToString() : "Unavailable";
    private static string Hex8(ReadOnlySpan<byte> data, int offset) => offset < data.Length ? $"0x{data[offset]:X2}" : "Unavailable";
    private static string U16(ReadOnlySpan<byte> data, int offset) => offset + 2 <= data.Length ? Read16(data, offset).ToString() : "Unavailable";
    private static string U32(ReadOnlySpan<byte> data, int offset) => offset + 4 <= data.Length ? Read32(data, offset).ToString() : "Unavailable";
    private static string Hex32(ReadOnlySpan<byte> data, int offset) => offset + 4 <= data.Length ? $"0x{Read32(data, offset):X8}" : "Unavailable";
    private static string Hex64(ReadOnlySpan<byte> data, int offset) => offset + 8 <= data.Length ? $"0x{Read64(data, offset):X16}" : "Unavailable";
    private static string GasSpace(ReadOnlySpan<byte> data, int offset) => offset < data.Length ? $"{data[offset]} ({data[offset] switch { 0 => "System memory", 1 => "System I/O", 2 => "PCI configuration", 3 => "Embedded controller", 4 => "SMBus", _ => "Other" }})" : "Unavailable";
    private static string Hex(ReadOnlySpan<byte> data, int offset, int count) => count <= 0 || offset >= data.Length ? "Empty" : Convert.ToHexString(data.Slice(offset, Math.Min(count, data.Length - offset))).Chunk(2).Select(chars => new string(chars)).Aggregate((left, right) => left + " " + right);
    private static string DebugPortType(byte type) => type switch { 0 => "Full 16550", 1 => "16550 subset", _ => "Reserved" };
    private static string PmProfile(byte profile) => profile switch { 0 => "Unspecified", 1 => "Desktop", 2 => "Mobile", 3 => "Workstation", 4 => "Enterprise server", 5 => "SOHO server", 6 => "Appliance PC", 7 => "Performance server", 8 => "Tablet", _ => "Reserved" };
    private static string TpmStartMethod(uint method) => method switch { 2 => "ACPI start method", 6 => "Memory mapped", 7 => "Command response buffer", 8 => "Command response buffer with ACPI", 11 => "ARM SMC", 12 => "Command response buffer with ARM SMC", _ => "Vendor/reserved" };
}
