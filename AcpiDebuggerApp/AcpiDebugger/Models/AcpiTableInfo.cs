namespace AcpiDebugger.Models;

public sealed record AcpiTableInfo(
    string Signature,
    string Description,
    string OemId,
    string TableId,
    uint OemRevision,
    string CreatorId,
    uint CreatorRevision,
    byte Revision,
    uint Length,
    byte Checksum,
    bool ChecksumValid,
    string Source);
