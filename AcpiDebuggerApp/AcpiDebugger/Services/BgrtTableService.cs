using AcpiDebugger.Models;
using System.Buffers.Binary;
using System.IO;

namespace AcpiDebugger.Services;

public static class BgrtTableService
{
    public const int MinimumTableLength = 56;

    public static BgrtTableInfo Read(string filePath) => Read(File.ReadAllBytes(filePath));

    public static BgrtTableInfo Read(ReadOnlySpan<byte> table)
    {
        if (table.Length < MinimumTableLength)
            throw new InvalidDataException("The BGRT table must be at least 56 bytes long.");
        if (!table[..4].SequenceEqual("BGRT"u8))
            throw new InvalidDataException("The selected file is not a BGRT table.");

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(table[4..8]);
        if (declaredLength < MinimumTableLength || declaredLength > table.Length)
            throw new InvalidDataException("The BGRT table length in the header is invalid.");

        int checksum = 0;
        foreach (byte value in table[..checked((int)declaredLength)])
            checksum = (checksum + value) & 0xFF;

        return new BgrtTableInfo(
            BinaryPrimitives.ReadUInt16LittleEndian(table[36..38]), table[38], table[39],
            BinaryPrimitives.ReadUInt64LittleEndian(table[40..48]),
            BinaryPrimitives.ReadUInt32LittleEndian(table[48..52]),
            BinaryPrimitives.ReadUInt32LittleEndian(table[52..56]), checksum == 0);
    }

}
