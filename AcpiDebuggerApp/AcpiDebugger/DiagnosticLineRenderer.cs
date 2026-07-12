using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Media;

namespace AcpiDebugger;

internal sealed class DiagnosticLineRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private readonly HashSet<int> _errorLines = new();
    private readonly HashSet<int> _warningLines = new();

    public DiagnosticLineRenderer(TextView textView)
    {
        _textView = textView;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void SetDiagnostics(IEnumerable<(int Line, bool IsError)> diagnostics)
    {
        _errorLines.Clear();
        _warningLines.Clear();

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.IsError) _errorLines.Add(diagnostic.Line);
            else _warningLines.Add(diagnostic.Line);
        }

        _textView.InvalidateLayer(Layer);
    }

    public void Clear() => SetDiagnostics(Array.Empty<(int, bool)>());

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;

        foreach (VisualLine visualLine in textView.VisualLines)
        {
            int line = visualLine.FirstDocumentLine.LineNumber;
            Brush? brush = _errorLines.Contains(line)
                ? new SolidColorBrush(Color.FromArgb(58, 239, 68, 68))
                : _warningLines.Contains(line)
                    ? new SolidColorBrush(Color.FromArgb(42, 245, 158, 11))
                    : null;

            if (brush == null) continue;
            var segment = new TextSegment
            {
                StartOffset = visualLine.FirstDocumentLine.Offset,
                EndOffset = visualLine.LastDocumentLine.EndOffset
            };

            foreach (var rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                drawingContext.DrawRectangle(brush, null, rectangle);
        }
    }
}
