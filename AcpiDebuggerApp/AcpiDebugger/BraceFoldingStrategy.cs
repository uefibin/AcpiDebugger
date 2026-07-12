using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace AcpiDebugger;

internal sealed class BraceFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        manager.UpdateFoldings(CreateFoldings(document), -1);
    }

    private static IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        var result = new List<NewFolding>();
        var stack = new Stack<(int Offset, string Name)>();
        string text = document.Text;
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n') inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    index++;
                }
                continue;
            }

            if (!inString && current == '/' && next == '/')
            {
                inLineComment = true;
                index++;
                continue;
            }

            if (!inString && current == '/' && next == '*')
            {
                inBlockComment = true;
                index++;
                continue;
            }

            if (current == '"' && (index == 0 || text[index - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (current == '{')
            {
                stack.Push((index, GetFoldName(document, index)));
            }
            else if (current == '}' && stack.Count > 0)
            {
                var start = stack.Pop();
                int startLine = document.GetLineByOffset(start.Offset).LineNumber;
                int endLine = document.GetLineByOffset(index).LineNumber;
                if (endLine > startLine)
                {
                    result.Add(new NewFolding(start.Offset, index + 1)
                    {
                        Name = start.Name
                    });
                }
            }
        }

        return result.OrderBy(folding => folding.StartOffset);
    }

    private static string GetFoldName(TextDocument document, int braceOffset)
    {
        DocumentLine line = document.GetLineByOffset(braceOffset);
        string prefix = document.GetText(line.Offset, braceOffset - line.Offset).Trim();
        if (string.IsNullOrEmpty(prefix)) return "{...}";

        const int maxLength = 60;
        if (prefix.Length > maxLength)
            prefix = prefix[..maxLength] + "…";
        return prefix + " …";
    }
}
