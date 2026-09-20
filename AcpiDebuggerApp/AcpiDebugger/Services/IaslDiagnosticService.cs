using AcpiDebugger.Models;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AcpiDebugger.Services;

public static class IaslDiagnosticService
{
    private static readonly Regex SourceLocationRegex = new(
        @"(?im)^(?<file>[^\r\n:]+\.dsl)\s+(?<line>\d+):.*$",
        RegexOptions.Compiled);

    private static readonly Regex DiagnosticMessageRegex = new(
        @"(?im)^(?<severity>Error|Warning|Remark)\s+(?<code>\d+)\s+-\s+(?<message>.*)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<CompilerDiagnostic> Parse(string output)
    {
        MatchCollection locations = SourceLocationRegex.Matches(output);
        MatchCollection messages = DiagnosticMessageRegex.Matches(output);
        int count = Math.Min(locations.Count, messages.Count);
        var diagnostics = new List<CompilerDiagnostic>(count);
        for (int index = 0; index < count; index++)
        {
            Match location = locations[index];
            Match message = messages[index];
            diagnostics.Add(new CompilerDiagnostic(
                location.Groups["file"].Value.Trim(),
                int.Parse(location.Groups["line"].Value),
                message.Groups["severity"].Value,
                message.Groups["code"].Value,
                message.Groups["message"].Value.Trim()));
        }
        return diagnostics;
    }

    public static IReadOnlyList<ExternalRepair> CreateExternalRepairs(string filePath, string compilerOutput)
    {
        int[] lineNumbers = Parse(compilerOutput)
            .Where(item => item.Code == "6163")
            .Select(item => item.Line)
            .Distinct()
            .ToArray();

        if (lineNumbers.Length == 0) return Array.Empty<ExternalRepair>();

        string[] lines = File.ReadAllLines(filePath);
        var repairs = new List<ExternalRepair>();
        foreach (int lineNumber in lineNumbers)
        {
            int index = lineNumber - 1;
            if (index < 0 || index >= lines.Length) continue;

            string original = lines[index];
            if (!original.TrimStart().StartsWith("External (", StringComparison.Ordinal)) continue;

            string updated = Regex.Replace(
                original,
                @"(\.[A-Za-z0-9_]{4})(\.[A-Za-z0-9_]{4},\s*[A-Za-z]+Obj)",
                "$2");
            if (updated != original)
                repairs.Add(new ExternalRepair(lineNumber, original, updated));
        }

        return repairs;
    }

    public static void ApplyExternalRepairs(string filePath, IReadOnlyList<ExternalRepair> repairs)
    {
        if (repairs.Count == 0) return;
        string[] lines = File.ReadAllLines(filePath);
        foreach (ExternalRepair repair in repairs)
            lines[repair.Line - 1] = repair.Updated;
        File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
    }

    public static IReadOnlyList<ReservedNameRepair> CreateReservedNameRepairs(
        string filePath,
        string compilerOutput)
    {
        int[] lineNumbers = Parse(compilerOutput)
            .Where(item => item.Code == "6105")
            .Select(item => item.Line)
            .Distinct()
            .ToArray();
        if (lineNumbers.Length == 0) return Array.Empty<ReservedNameRepair>();

        string[] lines = File.ReadAllLines(filePath);
        var repairs = new List<ReservedNameRepair>();
        foreach (int lineNumber in lineNumbers)
        {
            int index = lineNumber - 1;
            if (index < 0 || index >= lines.Length) continue;
            string original = lines[index];
            string? updated = CreateDepReferenceRepair(original)
                ?? CreateClsPackageRepair(original);
            if (updated != null && updated != original)
                repairs.Add(new ReservedNameRepair(lineNumber, original, updated));
        }
        return repairs;
    }

    public static void ApplyReservedNameRepairs(
        string filePath,
        IReadOnlyList<ReservedNameRepair> repairs)
    {
        if (repairs.Count == 0) return;
        string[] lines = File.ReadAllLines(filePath);
        foreach (ReservedNameRepair repair in repairs)
            lines[repair.Line - 1] = repair.Updated;
        File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
    }

    public static IReadOnlyList<UnresolvedPathRepair> CreateUnresolvedPathRepairs(
        string filePath,
        string compilerOutput)
    {
        int[] lineNumbers = Parse(compilerOutput)
            .Where(item => item.Code == "6161")
            .Select(item => item.Line)
            .Distinct()
            .ToArray();
        if (lineNumbers.Length == 0) return Array.Empty<UnresolvedPathRepair>();

        string[] lines = File.ReadAllLines(filePath);
        var repairs = new List<UnresolvedPathRepair>();
        foreach (int lineNumber in lineNumbers)
        {
            int index = lineNumber - 1;
            if (index < 0 || index >= lines.Length) continue;

            string original = lines[index];
            string updated = Regex.Replace(
                original,
                @"(?<![\\A-Za-z0-9_])_SB(?=\.[A-Za-z0-9_]{4})",
                @"\_SB");
            if (updated != original)
                repairs.Add(new UnresolvedPathRepair(lineNumber, original, updated));
        }
        return repairs;
    }

    public static void ApplyUnresolvedPathRepairs(
        string filePath,
        IReadOnlyList<UnresolvedPathRepair> repairs)
    {
        if (repairs.Count == 0) return;
        string[] lines = File.ReadAllLines(filePath);
        foreach (UnresolvedPathRepair repair in repairs)
            lines[repair.Line - 1] = repair.Updated;
        File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
    }

    private static string? CreateDepReferenceRepair(string line)
    {
        string indentation = line[..(line.Length - line.TrimStart().Length)];
        string trimmed = line.Trim();
        string suffix = trimmed.EndsWith(",", StringComparison.Ordinal) ? "," : string.Empty;
        string token = trimmed.TrimEnd(',').TrimEnd();
        if (token.Length < 5 || token[0] != '"' || token[^1] != '"') return null;

        string value = token[1..^1];
        if (!value.StartsWith("\\\\_", StringComparison.Ordinal)) return null;
        return indentation + value[1..] + suffix;
    }

    private static string? CreateClsPackageRepair(string line)
    {
        Match match = Regex.Match(
            line,
            @"Name\s*\(\s*_CLS\s*,\s*0x(?<value>[0-9A-Fa-f]{1,16})\s*\)");
        if (!match.Success || !ulong.TryParse(
                match.Groups["value"].Value,
                System.Globalization.NumberStyles.HexNumber,
                null,
                out ulong encoded))
            return null;

        int shift = encoded > 0xFFFFFF ? 32 : 0;
        byte baseClass = (byte)(encoded >> (shift + 16));
        byte subClass = (byte)(encoded >> (shift + 8));
        byte programmingInterface = (byte)(encoded >> shift);
        string replacement =
            $"Name (_CLS, Package (0x03) {{ 0x{baseClass:X2}, 0x{subClass:X2}, 0x{programmingInterface:X2} }})";
        return line[..match.Index] + replacement + line[(match.Index + match.Length)..];
    }

    public static IReadOnlyList<string> GetCompilerGuidance(string compilerOutput)
    {
        var guidance = new List<string>();
        if (compilerOutput.Contains("Error    6105", StringComparison.Ordinal)
            || compilerOutput.Contains("Error 6105", StringComparison.Ordinal))
        {
            guidance.Add(
                "Guidance for Error 6105: the firmware AML uses an invalid type for an ACPI reserved name. "
                + "Review the ASL manually; _DEP must return a Package of object references and _CLS must "
                + "return Package (3) { base class, subclass, programming interface }. Only recognized "
                + "_DEP and _CLS patterns are offered for review before any rewrite.");
        }

        if (compilerOutput.Contains("Error    6161", StringComparison.Ordinal)
            || compilerOutput.Contains("Error 6161", StringComparison.Ordinal))
        {
            guidance.Add(
                "Guidance for Error 6161: the pathname was not resolved. Re-dump all ACPI tables and "
                + "decompile the DSDT with its SSDTs as external tables, or add an External declaration "
                + "with a verified object type. UnknownObj is not inserted automatically because an incorrect "
                + "type can cause scope and namespace errors. A bare _SB.x path on the diagnostic line is "
                + "offered for review as \\_SB.x because it is an unambiguous missing root prefix.");
        }

        if ((compilerOutput.Contains("Error    6010", StringComparison.Ordinal)
                || compilerOutput.Contains("Error 6010", StringComparison.Ordinal))
            && (compilerOutput.Contains("Error    6313", StringComparison.Ordinal)
                || compilerOutput.Contains("Error 6313", StringComparison.Ordinal)))
        {
            guidance.Add(
                "Guidance for data-table compiler errors 6010/6313: the table layout emitted by this iASL "
                + "cannot be compiled back by the same data-table template. Keep the original .dat file, "
                + "upgrade iASL, and re-dump/re-test the table. No binary fallback or silent rewrite was applied.");
        }

        return guidance;
    }
}

public sealed record ExternalRepair(int Line, string Original, string Updated);
public sealed record ReservedNameRepair(int Line, string Original, string Updated);
public sealed record UnresolvedPathRepair(int Line, string Original, string Updated);
