namespace AcpiDebugger.Models;

public sealed record BgrtTableInfo(
    ushort Version,
    byte Status,
    byte ImageType,
    ulong ImageAddress,
    uint ImageOffsetX,
    uint ImageOffsetY,
    bool ChecksumValid)
{
    public bool ImageDisplayed => (Status & 0x01) != 0;
    public byte Orientation => (byte)((Status >> 1) & 0x03);
    public string OrientationDescription => Orientation switch
    {
        0 => "0 degrees",
        1 => "90 degrees clockwise",
        2 => "180 degrees",
        3 => "270 degrees clockwise",
        _ => "Unknown"
    };
}
