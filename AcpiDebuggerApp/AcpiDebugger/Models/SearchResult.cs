namespace AcpiDebugger.Models;

public sealed record SearchResult(
    string File,
    int Line,
    string Preview);
