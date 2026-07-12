namespace AcpiDebugger.Models;

public sealed record CompilerDiagnostic(
    string File,
    int Line,
    string Severity,
    string Code,
    string Message);
