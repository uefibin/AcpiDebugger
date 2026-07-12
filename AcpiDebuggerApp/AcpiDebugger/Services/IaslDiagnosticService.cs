using AcpiDebugger.Models;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AcpiDebugger.Services;

public static class IaslDiagnosticService
{
    private static readonly Regex DiagnosticRegex = new(
        @"(?im)^(?<file>[^\r\n:]+\.dsl)\s+(?<line>\d+):.*(?:\r?\n)(?<severity>Error|Warning|Remark)\s+(?<code>\d+)\s+-\s+(?<message>.*)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<CompilerDiagnostic> Parse(string output)
    {
        return DiagnosticRegex.Matches(output)
            .Select(match => new CompilerDiagnostic(
                match.Groups["file"].Value.Trim(),
                int.Parse(match.Groups["line"].Value),
                match.Groups["severity"].Value,
                match.Groups["code"].Value,
                match.Groups["message"].Value.Trim()))
            .ToList();
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
}

public sealed record ExternalRepair(int Line, string Original, string Updated);
