namespace AcpiDebugger.Models;

public sealed record AcpiSpecificField(string Name, string Value);

public sealed record AcpiTableSpecificInfo(
    string Summary,
    IReadOnlyList<AcpiSpecificField> Fields);
