namespace AcpiDebugger.Models;

public sealed record TableNode(
    string Filename,
    bool IsSource,
    object? TableItem);
