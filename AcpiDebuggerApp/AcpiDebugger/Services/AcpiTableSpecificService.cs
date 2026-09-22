using AcpiDebugger.Models;
using System.Buffers.Binary;
using System.IO;
using System.Text;

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
            "DBG2" => ParseDbg2(table),
            "DBGP" => ParseDbgp(table),
            "DTPR" => ParseDtpr(table),
            "FACP" => ParseFacp(table),
            "FACS" => ParseFacs(table),
            "FIDT" => ParseFidt(table),
            "FPDT" => ParseFpdt(table),
            "HPET" => ParseHpet(table),
            "LPIT" => ParseLpit(table),
            "MCFG" => ParseMcfg(table),
            "NHLT" => ParseNhlt(table),
            "PHAT" => ParsePhat(table),
            "TPM2" => ParseTpm2(table),
            "UEFI" => ParseUefi(table),
            "WSMT" => ParseWsmt(table),
            "RSDT" or "XSDT" => ParseRootTable(table, signature == "XSDT"),
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
        var fields = new List<(string, string)>
        {
            ("Local APIC address", Hex32(table, 36)),
            ("Flags", Hex32(table, 40)),
            ("PC-AT compatibility", (Read32(table, 40) & 1) != 0 ? "Yes" : "No")
        };
        int offset = 44;
        int index = 0;
        while (offset + 2 <= table.Length)
        {
            byte type = table[offset];
            int size = table[offset + 1];
            if (size < 2 || offset + size > table.Length)
            {
                fields.Add(("MADT status", "Truncated or invalid subtable"));
                break;
            }

            index++;
            string prefix = $"Subtable {index}";
            fields.Add(($"{prefix} type", $"{type} ({MadtType(type)})"));
            fields.Add(($"{prefix} length", $"{size} bytes"));
            switch (type)
            {
                case 0 when size >= 8:
                    fields.Add(($"{prefix} processor ID", Read8(table, offset + 2).ToString()));
                    fields.Add(($"{prefix} APIC ID", Read8(table, offset + 3).ToString()));
                    fields.Add(($"{prefix} flags", Hex32(table, offset + 4)));
                    break;
                case 1 when size >= 12:
                    fields.Add(($"{prefix} I/O APIC ID", Read8(table, offset + 2).ToString()));
                    fields.Add(($"{prefix} address", Hex32(table, offset + 4)));
                    fields.Add(($"{prefix} global IRQ base", Read32(table, offset + 8).ToString()));
                    break;
                case 2 when size >= 10:
                    fields.Add(($"{prefix} bus", Read8(table, offset + 2).ToString()));
                    fields.Add(($"{prefix} source IRQ", Read8(table, offset + 3).ToString()));
                    fields.Add(($"{prefix} global IRQ", Read32(table, offset + 4).ToString()));
                    fields.Add(($"{prefix} flags", Hex16(table, offset + 8)));
                    break;
                case 3 when size >= 8:
                    fields.Add(($"{prefix} flags", Hex16(table, offset + 2)));
                    fields.Add(($"{prefix} global IRQ", Read32(table, offset + 4).ToString()));
                    break;
                case 4 when size >= 6:
                    fields.Add(($"{prefix} processor ID", Read8(table, offset + 2).ToString()));
                    fields.Add(($"{prefix} flags", Hex16(table, offset + 3)));
                    fields.Add(($"{prefix} LINT", Read8(table, offset + 5).ToString()));
                    break;
                case 5 when size >= 12:
                    fields.Add(($"{prefix} local APIC address", Hex64(table, offset + 4)));
                    break;
                case 9 when size >= 16:
                    fields.Add(($"{prefix} x2APIC ID", Hex32(table, offset + 4)));
                    fields.Add(($"{prefix} flags", Hex32(table, offset + 8)));
                    fields.Add(($"{prefix} UID", Hex32(table, offset + 12)));
                    break;
                case 10 when size >= 12:
                    fields.Add(($"{prefix} flags", Hex16(table, offset + 2)));
                    fields.Add(($"{prefix} UID", Hex32(table, offset + 4)));
                    fields.Add(($"{prefix} LINT", Read8(table, offset + 8).ToString()));
                    break;
                default:
                    if (size > 2)
                        fields.Add(($"{prefix} data", Hex(table, offset + 2, size - 2)));
                    break;
            }
            offset += size;
        }
        fields.Insert(3, ("Interrupt structures", index.ToString()));
        return Fields("Multiple APIC Description Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseDbgp(ReadOnlySpan<byte> table) =>
        Fields("Debug Port Table",
            ("Interface type", table.Length > 36 ? $"{table[36]} ({DebugPortType(table[36])})" : "Unavailable"),
            ("Port register", Gas(table, 40)));

    private static AcpiTableSpecificInfo ParseDbg2(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        uint infoOffsetValue = Read32(table, 36);
        uint infoCount = Read32(table, 40);
        fields.Add(("Info offset", $"0x{infoOffsetValue:X8}"));
        fields.Add(("Info structure count", infoCount.ToString()));
        if (infoOffsetValue >= table.Length)
            return Fields("Debug Port Table 2", fields.ToArray());

        int cursor = checked((int)infoOffsetValue);
        int parsed = 0;
        for (uint index = 0; index < Math.Min(infoCount, 4096); index++)
        {
            if (!Has(table, cursor, 4)) break;
            int length = Read16(table, cursor + 1);
            if (length < 4 || !Has(table, cursor, length))
            {
                fields.Add(("DBG2 status", "Truncated or invalid device structure"));
                break;
            }

            parsed++;
            string prefix = $"Device {parsed}";
            int registerCount = Read8(table, cursor + 3);
            fields.Add(($"{prefix} revision", Read8(table, cursor).ToString()));
            fields.Add(($"{prefix} length", $"{length} bytes"));
            fields.Add(($"{prefix} register count", registerCount.ToString()));
            if (length >= 22)
            {
                int nameLength = Read16(table, cursor + 4);
                int nameOffset = Read16(table, cursor + 6);
                int oemLength = Read16(table, cursor + 8);
                int oemOffset = Read16(table, cursor + 10);
                int baseOffset = Read16(table, cursor + 18);
                int sizeOffset = Read16(table, cursor + 20);
                fields.Add(($"{prefix} port type", Hex16(table, cursor + 12)));
                fields.Add(($"{prefix} port subtype", Hex16(table, cursor + 14)));
                fields.Add(($"{prefix} namepath offset", $"0x{nameOffset:X4}"));
                fields.Add(($"{prefix} namepath length", nameLength.ToString()));
                if (nameOffset <= length && nameLength <= length - nameOffset)
                    fields.Add(($"{prefix} namepath", Ascii(table, cursor + nameOffset, nameLength)));
                if (oemOffset <= length && oemLength <= length - oemOffset && oemLength > 0)
                    fields.Add(($"{prefix} OEM data", Hex(table, cursor + oemOffset, oemLength)));
                for (int register = 0; register < registerCount; register++)
                {
                    if (baseOffset + register * 12 + 12 > length || sizeOffset + register * 4 + 4 > length)
                        break;
                    fields.Add(($"{prefix} register {register + 1}", Gas(table, cursor + baseOffset + register * 12)));
                    fields.Add(($"{prefix} register {register + 1} size", $"{Read32(table, cursor + sizeOffset + register * 4)} bytes"));
                }
            }
            cursor += length;
        }
        if (parsed != infoCount)
            fields.Add(("DBG2 parse status", $"Parsed {parsed} of {infoCount} structures"));
        return Fields("Debug Port Table 2", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseDtpr(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>
        {
            ("Flags", Hex32(table, 36)),
            ("Instance count", Read32(table, 40).ToString())
        };
        uint instanceCount = Read32(table, 40);
        int offset = 44;
        int parsedInstances = 0;
        for (uint instance = 0; instance < Math.Min(instanceCount, 4096); instance++)
        {
            if (!Has(table, offset, 8)) break;
            parsedInstances++;
            string prefix = $"Instance {instance + 1}";
            uint rangeCount = Read32(table, offset + 4);
            fields.Add(($"{prefix} flags", Hex32(table, offset)));
            fields.Add(($"{prefix} TPR range count", rangeCount.ToString()));
            offset += 8;
            int parsedRanges = 0;
            for (uint range = 0; range < Math.Min(rangeCount, 4096); range++)
            {
                if (!Has(table, offset, 8)) break;
                parsedRanges++;
                fields.Add(($"{prefix} TPR {range + 1} base address", Hex64(table, offset)));
                offset += 8;
            }
            AddCountStatus(fields, $"{prefix} TPR", rangeCount, parsedRanges);
        }
        AddCountStatus(fields, "Instance", instanceCount, parsedInstances);
        if (Has(table, offset, 4))
        {
            uint count = Read32(table, offset);
            fields.Add(("Serialization register count", count.ToString()));
            offset += 4;
            int parsed = 0;
            for (uint index = 0; index < Math.Min(count, 4096); index++)
            {
                if (!Has(table, offset, 8)) break;
                parsed++;
                fields.Add(($"Serialization register {index + 1}", Hex64(table, offset)));
                offset += 8;
            }
            AddCountStatus(fields, "Serialization register", count, parsed);
        }
        AddRawRange(fields, table, "Trailing data", offset, table.Length - offset);
        return Fields("DMA Remapping Reporting Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseFacs(ReadOnlySpan<byte> table)
    {
        uint flags = Read32(table, 20);
        var fields = new List<(string, string)>
        {
            ("Hardware signature", Hex32(table, 8)),
            ("32-bit firmware waking vector", Hex32(table, 12)),
            ("Global lock", Hex32(table, 16)),
            ("Flags", $"0x{flags:X8}"),
            ("S4BIOS supported", (flags & 1) != 0 ? "Yes" : "No"),
            ("64-bit firmware waking vector", Hex64(table, 24)),
            ("Version", Read8(table, 32).ToString())
        };
        if (table.Length > 33)
            fields.Add(("Reserved", Hex(table, 33, Math.Min(3, table.Length - 33))));
        return Fields("Firmware ACPI Control Structure", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseFidt(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)> { ("Format", "OEM Firmware ID Table") };
        if (!Has(table, 36, 7))
        {
            fields.Add(("FIDT status", "Missing OEM record header"));
            return Fields("Firmware ID Table", fields.ToArray());
        }
        ushort length = Read16(table, 41);
        int actual = Math.Min(length, table.Length - 36);
        fields.Add(("Record signature", Ascii(table, 36, 4)));
        fields.Add(("Record revision", Read8(table, 40).ToString()));
        fields.Add(("Record length", $"{length} bytes"));
        if (actual > 7)
        {
            AddAsciiRuns(fields, table, 43, actual - 7, "OEM string");
            AddRawRange(fields, table, "OEM record data", 43, actual - 7);
        }
        if (actual != length) fields.Add(("FIDT status", "Truncated OEM record"));
        return Fields("Firmware ID Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseFacp(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>
        {
            ("FACS address", Hex32(table, 36)), ("DSDT address", Hex32(table, 40)),
            ("System interrupt model", Hex8(table, 44)),
            ("Preferred PM profile", table.Length > 45 ? $"{table[45]} ({PmProfile(table[45])})" : "Unavailable"),
            ("SCI interrupt", U16(table, 46)), ("SMI command port", Hex32(table, 48)),
            ("ACPI enable", Hex8(table, 52)), ("ACPI disable", Hex8(table, 53)),
            ("S4 BIOS request", Hex8(table, 54)), ("P-state control", Hex8(table, 55)),
            ("PM1a event block", Hex32(table, 56)), ("PM1b event block", Hex32(table, 60)),
            ("PM1a control block", Hex32(table, 64)), ("PM1b control block", Hex32(table, 68)),
            ("PM2 control block", Hex32(table, 72)), ("PM timer block", Hex32(table, 76)),
            ("GPE0 block", Hex32(table, 80)), ("GPE1 block", Hex32(table, 84)),
            ("PM1 event length", $"{Read8(table, 88)} bytes"),
            ("PM1 control length", $"{Read8(table, 89)} bytes"),
            ("PM2 control length", $"{Read8(table, 90)} bytes"),
            ("PM timer length", $"{Read8(table, 91)} bytes"),
            ("GPE0 block length", $"{Read8(table, 92)} bytes"),
            ("GPE1 block length", $"{Read8(table, 93)} bytes"),
            ("GPE1 base", Read8(table, 94).ToString()),
            ("CST control", Hex8(table, 95)),
            ("C2 latency", $"{Read16(table, 96)} us"),
            ("C3 latency", $"{Read16(table, 98)} us"),
            ("Flush size", $"{Read16(table, 100)} bytes"),
            ("Flush stride", Read16(table, 102).ToString()),
            ("Duty offset", Read8(table, 104).ToString()),
            ("Duty width", Read8(table, 105).ToString()),
            ("Day alarm", Read8(table, 106).ToString()),
            ("Month alarm", Read8(table, 107).ToString()),
            ("Century", Read8(table, 108).ToString()),
            ("Boot architecture flags", Hex16(table, 109)),
            ("Flags", Hex32(table, 112))
        };
        if (Has(table, 116, 12)) fields.Add(("Reset register", Gas(table, 116)));
        if (Has(table, 128, 1)) fields.Add(("Reset value", Hex8(table, 128)));
        if (Has(table, 129, 2)) fields.Add(("ARM boot architecture flags", Hex16(table, 129)));
        if (Has(table, 131, 1)) fields.Add(("FADT minor revision", Read8(table, 131).ToString()));
        if (Has(table, 132, 8)) fields.Add(("Extended FACS address", Hex64(table, 132)));
        if (Has(table, 140, 8)) fields.Add(("Extended DSDT address", Hex64(table, 140)));
        string[] extendedNames =
        [
            "Extended PM1a event block", "Extended PM1b event block",
            "Extended PM1a control block", "Extended PM1b control block",
            "Extended PM2 control block", "Extended PM timer block",
            "Extended GPE0 block", "Extended GPE1 block",
            "Sleep control", "Sleep status"
        ];
        for (int offset = 148, index = 0; index < extendedNames.Length; offset += 12, index++)
            if (Has(table, offset, 12)) fields.Add((extendedNames[index], Gas(table, offset)));
        if (Has(table, 268, 8)) fields.Add(("Hypervisor vendor ID", Hex64(table, 268)));
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
            if (size < 4 || offset + size > table.Length)
            {
                fields.Add(("FPDT status", "Truncated or invalid record"));
                break;
            }
            index++;
            string prefix = $"Record {index}";
            fields.Add(($"{prefix} type", $"0x{type:X4}"));
            fields.Add(($"{prefix} type name", type switch
            {
                0 => "Firmware Basic Boot Performance Table pointer",
                1 => "S3 Performance Table pointer",
                _ => "Unknown performance pointer record"
            }));
            fields.Add(($"{prefix} length", $"{size} bytes"));
            fields.Add(($"{prefix} revision", revision.ToString()));
            if (size >= 16)
            {
                fields.Add(($"{prefix} reserved", Hex32(table, offset + 4)));
                fields.Add(($"{prefix} performance table address", Hex64(table, offset + 8)));
                fields.Add(($"{prefix} timing data location", "Physical memory pointed to by this record"));
            }
            offset += size;
        }
        if (index == 0) fields.Add(("Records", "None"));
        else fields.Add(("Timing values", "Stored in the pointed FBPT/S3PT memory, not inside the FPDT bytes"));
        fields.Insert(0, ("Record count", index.ToString()));
        return Fields("Firmware Performance Data Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseHpet(ReadOnlySpan<byte> table) =>
        Fields("High Precision Event Timer Table",
            ("Hardware ID", Hex32(table, 36)),
            ("Timer address", Gas(table, 40)),
            ("Sequence", ByteValue(table, 52)),
            ("Minimum tick", U16(table, 53)),
            ("Flags", Hex8(table, 55)));

    private static AcpiTableSpecificInfo ParseLpit(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        int offset = 36;
        int count = 0;
        while (offset + 8 <= table.Length)
        {
            uint type = Read32(table, offset);
            int size = checked((int)Read32(table, offset + 4));
            if (size < 16 || offset + size > table.Length)
            {
                fields.Add(("LPIT status", "Truncated or invalid subtable"));
                break;
            }
            count++;
            string prefix = $"Subtable {count}";
            fields.Add(($"{prefix} type", $"0x{type:X8}"));
            fields.Add(($"{prefix} type name", type == 0 ? "Native C-state" : "Reserved or OEM-defined"));
            fields.Add(($"{prefix} length", $"{size} bytes"));
            fields.Add(($"{prefix} unique ID", Hex16(table, offset + 8)));
            fields.Add(($"{prefix} flags", Hex32(table, offset + 12)));
            if (size >= 28) fields.Add(($"{prefix} entry trigger", Gas(table, offset + 16)));
            if (size >= 32) fields.Add(($"{prefix} residency", $"{Read32(table, offset + 28)} units"));
            if (size >= 36) fields.Add(($"{prefix} latency", $"{Read32(table, offset + 32)} units"));
            if (size >= 48) fields.Add(($"{prefix} counter", Gas(table, offset + 36)));
            if (size >= 56) fields.Add(($"{prefix} counter frequency", $"{Read64(table, offset + 48)} Hz"));
            offset += size;
        }
        if (count == 0) fields.Add(("LPIT status", "No complete subtables"));
        fields.Insert(0, ("Descriptor count", count.ToString()));
        return Fields("Low Power Idle Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseMcfg(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)> { ("Reserved", Hex(table, 36, 8)) };
        int count = 0;
        int offset;
        for (offset = 44; offset + 16 <= table.Length; offset += 16)
        {
            ulong address = Read64(table, offset);
            ushort segment = Read16(table, offset + 8);
            byte startBus = table[offset + 10];
            byte endBus = table[offset + 11];
            count++;
            string prefix = $"Allocation {count}";
            fields.Add(($"{prefix} base address", $"0x{address:X16}"));
            fields.Add(($"{prefix} segment", $"0x{segment:X4}"));
            fields.Add(($"{prefix} start bus", startBus.ToString()));
            fields.Add(($"{prefix} end bus", endBus.ToString()));
            if (endBus >= startBus)
                fields.Add(($"{prefix} ECAM size", $"{((ulong)(endBus - startBus) + 1) * 1024 * 1024:N0} bytes"));
            else
                fields.Add(($"{prefix} status", "End bus is below start bus"));
        }
        if (offset != table.Length) fields.Add(("MCFG status", "Trailing or truncated allocation data"));
        if (count == 0) fields.Add(("Allocations", "None"));
        fields.Insert(0, ("Allocation count", count.ToString()));
        return Fields("PCI Express Memory Mapped Configuration", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseNhlt(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        if (!Has(table, 36, 1))
            return Fields("Non-HD Audio Link Table", ("NHLT status", "Missing endpoint count"));

        int endpointCount = Read8(table, 36);
        fields.Add(("Endpoint count", endpointCount.ToString()));
        int offset = 37;
        for (int endpoint = 0; endpoint < endpointCount; endpoint++)
        {
            string prefix = $"Endpoint {endpoint + 1}";
            if (!Has(table, offset, 19))
            {
                fields.Add(($"{prefix} status", "Truncated endpoint header"));
                break;
            }
            int length = checked((int)Read32(table, offset));
            fields.Add(($"{prefix} length", $"{length} bytes"));
            fields.Add(($"{prefix} link type", LinkType(Read8(table, offset + 4))));
            fields.Add(($"{prefix} instance", Read8(table, offset + 5).ToString()));
            fields.Add(($"{prefix} vendor ID", Hex16(table, offset + 6)));
            fields.Add(($"{prefix} device ID", Hex16(table, offset + 8)));
            fields.Add(($"{prefix} revision ID", Hex16(table, offset + 10)));
            fields.Add(($"{prefix} subsystem ID", Hex32(table, offset + 12)));
            fields.Add(($"{prefix} device type", Hex8(table, offset + 16)));
            fields.Add(($"{prefix} direction", Direction(Read8(table, offset + 17))));
            fields.Add(($"{prefix} virtual bus ID", Hex8(table, offset + 18)));
            if (length < 19 || !Has(table, offset, length))
            {
                fields.Add(($"{prefix} status", "Truncated or invalid endpoint"));
                break;
            }

            int end = offset + length;
            int cursor = offset + 19;
            if (cursor + 4 <= end && Has(table, cursor, 4))
            {
                int capabilitySize = checked((int)Read32(table, cursor));
                fields.Add(($"{prefix} device config size", $"{capabilitySize} bytes"));
                cursor += 4;
                int actual = Math.Min(capabilitySize, end - cursor);
                if (actual > 0) fields.Add(($"{prefix} device config", Hex(table, cursor, actual)));
                cursor += actual;
                if (actual != capabilitySize)
                {
                    fields.Add(($"{prefix} device config status", "Truncated capability data"));
                    break;
                }
            }

            if (cursor < end)
            {
                int formatCount = Read8(table, cursor++);
                fields.Add(($"{prefix} format count", formatCount.ToString()));
                for (int format = 0; format < formatCount; format++)
                {
                    if (end - cursor < 44)
                    {
                        fields.Add(($"{prefix} format status", "Truncated format header"));
                        cursor = end;
                        break;
                    }
                    string formatName = $"{prefix} format {format + 1}";
                    int formatOffset = cursor;
                    fields.Add(($"{formatName} tag", Hex16(table, formatOffset)));
                    fields.Add(($"{formatName} channels", Read16(table, formatOffset + 2).ToString()));
                    fields.Add(($"{formatName} samples/sec", Read32(table, formatOffset + 4).ToString()));
                    fields.Add(($"{formatName} bits/sample", Read16(table, formatOffset + 14).ToString()));
                    ushort extraSize = Read16(table, formatOffset + 16);
                    cursor += 40;
                    uint configSize = Read32(table, cursor);
                    cursor += 4;
                    fields.Add(($"{formatName} extra format size", $"{extraSize} bytes"));
                    fields.Add(($"{formatName} config size", $"{configSize} bytes"));
                    if (configSize > end - cursor)
                    {
                        fields.Add(($"{formatName} config status", "Truncated format capability data"));
                        cursor = end;
                        break;
                    }
                    if (configSize > 0)
                        fields.Add(($"{formatName} config", Hex(table, cursor, checked((int)configSize))));
                    cursor += checked((int)configSize);
                }
            }

            if (cursor < end)
            {
                int deviceCount = Read8(table, cursor++);
                fields.Add(($"{prefix} device info count", deviceCount.ToString()));
                for (int device = 0; device < deviceCount; device++)
                {
                    if (end - cursor < 18)
                    {
                        fields.Add(($"{prefix} device info status", "Truncated device info"));
                        cursor = end;
                        break;
                    }
                    fields.Add(($"{prefix} device {device + 1} ID", Hex(table, cursor, 16)));
                    fields.Add(($"{prefix} device {device + 1} instance", Read8(table, cursor + 16).ToString()));
                    fields.Add(($"{prefix} device {device + 1} port", Read8(table, cursor + 17).ToString()));
                    cursor += 18;
                }
            }
            AddRawRange(fields, table, $"{prefix} unparsed data", cursor, end - cursor);
            offset = end;
        }

        if (Has(table, offset, 4))
        {
            uint size = Read32(table, offset);
            int actual = Math.Min(checked((int)size), table.Length - offset - 4);
            fields.Add(("OED capabilities size", $"{size} bytes"));
            if (actual > 0) fields.Add(("OED capabilities", Hex(table, offset + 4, actual)));
            if (actual != size) fields.Add(("NHLT status", "Truncated OED capabilities"));
        }
        else if (offset != table.Length)
        {
            fields.Add(("NHLT status", "Trailing or truncated OED configuration"));
        }
        return Fields("Non-HD Audio Link Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParsePhat(ReadOnlySpan<byte> table)
    {
        var fields = new List<(string, string)>();
        int offset = 36;
        int record = 0;
        while (Has(table, offset, 5))
        {
            ushort type = Read16(table, offset);
            int length = Read16(table, offset + 2);
            byte revision = Read8(table, offset + 4);
            if (length < 5 || !Has(table, offset, length))
            {
                fields.Add(("PHAT status", "Truncated or invalid record"));
                break;
            }
            record++;
            string prefix = $"Record {record}";
            fields.Add(($"{prefix} type", PhatType(type)));
            fields.Add(($"{prefix} length", $"{length} bytes"));
            fields.Add(($"{prefix} revision", revision.ToString()));
            if (type == 0)
            {
                if (length < 12)
                {
                    fields.Add(($"{prefix} status", "Short firmware version record"));
                }
                else
                {
                    uint count = Read32(table, offset + 8);
                    fields.Add(($"{prefix} element count", count.ToString()));
                    int available = length - 12;
                    int parsed = Math.Min(checked((int)Math.Min(count, 4096)), available / 28);
                    for (int item = 0; item < parsed; item++)
                    {
                        int element = offset + 12 + item * 28;
                        string name = $"{prefix} element {item + 1}";
                        fields.Add(($"{name} GUID", Hex(table, element, 16)));
                        fields.Add(($"{name} version", Hex64(table, element + 16)));
                        fields.Add(($"{name} producer ID", Hex32(table, element + 24)));
                    }
                    AddCountStatus(fields, $"{prefix} element", count, parsed);
                }
            }
            else if (type == 1)
            {
                if (length < 28)
                {
                    fields.Add(($"{prefix} status", "Short firmware health record"));
                }
                else
                {
                    byte health = Read8(table, offset + 7);
                    uint specificOffset = Read32(table, offset + 24);
                    fields.Add(($"{prefix} health", PhatHealth(health)));
                    fields.Add(($"{prefix} device GUID", Hex(table, offset + 8, 16)));
                    fields.Add(($"{prefix} device-specific offset", $"0x{specificOffset:X8}"));
                    int pathStart = offset + 28;
                    int pathEnd = offset + length;
                    if (specificOffset != 0 && specificOffset >= 28 && specificOffset <= length)
                    {
                        pathEnd = offset + checked((int)specificOffset);
                        AddRawRange(fields, table, $"{prefix} device-specific data", pathEnd, length - checked((int)specificOffset));
                    }
                    else if (specificOffset != 0)
                    {
                        fields.Add(($"{prefix} status", "Invalid device-specific offset"));
                    }
                    if (pathEnd > pathStart)
                    {
                        string path = Utf16Ascii(table, pathStart, pathEnd - pathStart);
                        fields.Add(($"{prefix} device path", string.IsNullOrEmpty(path)
                            ? Hex(table, pathStart, pathEnd - pathStart) : path));
                    }
                }
            }
            else
            {
                AddRawRange(fields, table, $"{prefix} data", offset + 5, length - 5);
            }
            offset += length;
        }
        if (record == 0) fields.Add(("Records", "None"));
        return Fields("Platform Health Assessment Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseTpm2(ReadOnlySpan<byte> table)
    {
        uint method = Read32(table, 48);
        var fields = new List<(string, string)>
        {
            (table.Length > 8 && table[8] >= 4 ? "Platform class" : "Reserved",
                table.Length > 8 && table[8] >= 4 ? Hex16(table, 36) : Hex32(table, 36)),
            ("Control area", Hex64(table, 40)),
            ("Start method", $"0x{method:X8}"),
            ("Start method name", TpmStartMethod(method))
        };
        if (table.Length > 52)
            fields.Add(("Platform data", Hex(table, 52, table.Length - 52)));
        return Fields("Trusted Platform Module 2.0 Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseUefi(ReadOnlySpan<byte> table)
    {
        ushort dataOffset = Read16(table, 52);
        int offset = dataOffset >= 54 && dataOffset < table.Length ? dataOffset : 54;
        var fields = new List<(string, string)>
        {
            ("Identifier", Has(table, 36, 16) ? new Guid(table.Slice(36, 16)).ToString() : "Unavailable"),
            ("Identifier bytes", Hex(table, 36, 16)),
            ("Data offset", dataOffset.ToString()),
            ("Data length", $"{Math.Max(0, table.Length - offset):N0} bytes")
        };
        if (table.Length > offset) fields.Add(("Data", Hex(table, offset, table.Length - offset)));
        return Fields("UEFI ACPI Data Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseWsmt(ReadOnlySpan<byte> table)
    {
        uint flags = Read32(table, 36);
        return Fields("Windows SMM Security Mitigations Table", ("Protection flags", $"0x{flags:X8}"),
            ("Fixed communication buffers", (flags & 1) != 0 ? "Enabled" : "Disabled"),
            ("Nested pointer protection", (flags & 2) != 0 ? "Enabled" : "Disabled"),
            ("System resource protection", (flags & 4) != 0 ? "Enabled" : "Disabled"));
    }

    private static AcpiTableSpecificInfo ParseRootTable(ReadOnlySpan<byte> table, bool extended)
    {
        int entrySize = extended ? 8 : 4;
        int payload = Math.Max(0, table.Length - 36);
        int count = payload / entrySize;
        var fields = new List<(string, string)>
        {
            ("Entry size", $"{entrySize} bytes"),
            ("Entry count", count.ToString())
        };
        for (int index = 0; index < count; index++)
        {
            int offset = 36 + index * entrySize;
            fields.Add(($"Entry {index + 1} table address", extended ? Hex64(table, offset) : Hex32(table, offset)));
        }
        if (payload % entrySize != 0)
            fields.Add(("Root table status", "Trailing incomplete address entry"));
        return Fields(extended ? "Extended System Description Table" : "Root System Description Table", fields.ToArray());
    }

    private static AcpiTableSpecificInfo ParseGeneric(ReadOnlySpan<byte> table, string signature)
    {
        int payload = Math.Max(0, table.Length - 36);
        var fields = new List<(string, string)> { ("Payload length", $"{payload:N0} bytes") };
        int shown = Math.Min(payload, 4096);
        for (int offset = 36; offset < 36 + shown; offset += 16)
        {
            int count = Math.Min(16, 36 + shown - offset);
            fields.Add(($"Data @ 0x{offset:X4}", Hex(table, offset, count)));
        }
        if (shown < payload)
            fields.Add(("Payload status", $"Showing first {shown:N0} of {payload:N0} bytes"));
        if (payload == 0) fields.Add(("Payload", "Empty"));
        return Fields($"{signature} table — generic payload view", fields.ToArray());
    }

    private static AcpiTableSpecificInfo Fields(string summary, params (string Name, string Value)[] values) =>
        new(summary, values.Select(value => new AcpiSpecificField(value.Name, value.Value)).ToArray());

    private static bool Has(ReadOnlySpan<byte> data, int offset, int length) =>
        offset >= 0 && length >= 0 && offset <= data.Length && length <= data.Length - offset;

    private static byte Read8(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 1) ? data[offset] : (byte)0;
    private static ushort Read16(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 2) ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : (ushort)0;
    private static uint Read32(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 4) ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : 0;
    private static ulong Read64(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 8) ? BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]) : 0;
    private static string ByteValue(ReadOnlySpan<byte> data, int offset) => offset < data.Length ? data[offset].ToString() : "Unavailable";
    private static string Hex8(ReadOnlySpan<byte> data, int offset) => offset < data.Length ? $"0x{data[offset]:X2}" : "Unavailable";
    private static string Hex16(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 2) ? $"0x{Read16(data, offset):X4}" : "Unavailable";
    private static string U16(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 2) ? Read16(data, offset).ToString() : "Unavailable";
    private static string U32(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 4) ? Read32(data, offset).ToString() : "Unavailable";
    private static string Hex32(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 4) ? $"0x{Read32(data, offset):X8}" : "Unavailable";
    private static string Hex64(ReadOnlySpan<byte> data, int offset) => Has(data, offset, 8) ? $"0x{Read64(data, offset):X16}" : "Unavailable";

    private static string Gas(ReadOnlySpan<byte> data, int offset)
    {
        if (!Has(data, offset, 12)) return "Unavailable";
        byte spaceId = Read8(data, offset);
        string space = spaceId switch
        {
            0 => "System memory",
            1 => "System I/O",
            2 => "PCI configuration space",
            3 => "Embedded controller",
            4 => "SMBus",
            5 => "System CMOS",
            6 => "PCI device BAR target",
            7 => "IPMI",
            8 => "General purpose I/O",
            9 => "Generic serial bus",
            10 => "Platform communications channel",
            _ => $"Unknown address space ({spaceId})"
        };
        return $"{space}, {Read8(data, offset + 1)} bits, bit offset {Read8(data, offset + 2)}, "
            + $"access size {Read8(data, offset + 3)}, address {Hex64(data, offset + 4)}";
    }

    private static string Hex(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (count <= 0) return "Empty";
        if (!Has(data, offset, Math.Min(count, Math.Max(0, data.Length - offset)))) return "Unavailable";
        int actual = Math.Min(count, data.Length - offset);
        return string.Join(" ", Convert.ToHexString(data.Slice(offset, actual)).Chunk(2).Select(chars => new string(chars)));
    }

    private static string Ascii(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (!Has(data, offset, count)) return "Unavailable";
        return Encoding.ASCII.GetString(data.Slice(offset, count)).TrimEnd('\0', ' ');
    }

    private static string Utf16Ascii(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (!Has(data, offset, count) || (count & 1) != 0) return string.Empty;
        var value = new StringBuilder();
        for (int index = 0; index < count; index += 2)
        {
            byte low = data[offset + index];
            byte high = data[offset + index + 1];
            if (low == 0 && high == 0) break;
            if (high != 0 || low < 0x20 || low > 0x7E) return string.Empty;
            value.Append((char)low);
        }
        return value.ToString();
    }

    private static void AddRawRange(List<(string, string)> fields, ReadOnlySpan<byte> data,
        string name, int offset, int length)
    {
        if (length <= 0 || !Has(data, offset, length)) return;
        fields.Add(($"{name} length", $"{length:N0} bytes"));
        fields.Add((name, Hex(data, offset, length)));
    }

    private static void AddAsciiRuns(List<(string, string)> fields, ReadOnlySpan<byte> data,
        int offset, int length, string prefix)
    {
        if (!Has(data, offset, length)) return;
        int end = offset + length;
        int cursor = offset;
        while (cursor < end)
        {
            while (cursor < end && (data[cursor] < 0x20 || data[cursor] > 0x7E)) cursor++;
            int start = cursor;
            while (cursor < end && data[cursor] >= 0x20 && data[cursor] <= 0x7E) cursor++;
            if (cursor - start >= 4)
                fields.Add(($"{prefix} @ 0x{start:X4}", Ascii(data, start, cursor - start)));
        }
    }

    private static void AddCountStatus(List<(string, string)> fields, string name, uint expected, int parsed)
    {
        if (expected != parsed)
            fields.Add(($"{name} status", $"Parsed {parsed} of {expected} entries"));
    }

    private static string DebugPortType(byte type) => type switch { 0 => "Full 16550", 1 => "16550 subset", _ => "Reserved" };
    private static string PmProfile(byte profile) => profile switch { 0 => "Unspecified", 1 => "Desktop", 2 => "Mobile", 3 => "Workstation", 4 => "Enterprise server", 5 => "SOHO server", 6 => "Appliance PC", 7 => "Performance server", 8 => "Tablet", _ => "Reserved" };
    private static string TpmStartMethod(uint method) => method switch { 0 => "Not allowed", 2 => "ACPI start method", 6 => "Memory mapped", 7 => "Command response buffer", 8 => "Command response buffer with start method", 11 => "ARM SMC", 13 => "Pluton", 15 => "ARM FF-A", _ => "Reserved or platform-specific" };
    private static string MadtType(byte type) => type switch
    {
        0 => "Processor Local APIC", 1 => "I/O APIC", 2 => "Interrupt Source Override",
        3 => "NMI Source", 4 => "Local APIC NMI", 5 => "Local APIC Address Override",
        6 => "I/O SAPIC", 7 => "Local SAPIC", 8 => "Platform Interrupt Source",
        9 => "Processor Local x2APIC", 10 => "Local x2APIC NMI",
        11 => "GICC", 12 => "GIC Distributor", 13 => "GIC MSI Frame",
        14 => "GIC Redistributor", 15 => "GIC ITS", 16 => "Multiprocessor Wakeup",
        _ => "Reserved or OEM-defined"
    };
    private static string LinkType(byte value) => value switch
    {
        0 => "HDA", 1 => "DSP", 2 => "PDM", 3 => "SSP", 4 => "SLIMBUS",
        5 => "SoundWire", 6 => "UAOL", _ => $"Unknown ({value})"
    };
    private static string Direction(byte value) => value switch
    {
        0 => "Render", 1 => "Capture", _ => $"Unknown ({value})"
    };
    private static string PhatType(ushort type) => type switch
    {
        0 => "Firmware Version Data (0)", 1 => "Firmware Health Data (1)",
        _ => $"Reserved or OEM-defined ({type})"
    };
    private static string PhatHealth(byte health) => health switch
    {
        0 => "Errors found", 1 => "No errors found", 2 => "Unknown", 3 => "Advisory",
        _ => $"Reserved ({health})"
    };
}
