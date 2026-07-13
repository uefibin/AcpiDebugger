param(
    [string]$OutputDirectory = $PSScriptRoot
)

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function New-RoundedPath {
    param(
        [single]$X,
        [single]$Y,
        [single]$Width,
        [single]$Height,
        [single]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-AppIconBitmap {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 256.0
    function S([double]$Value) { return [single]($Value * $scale) }

    $navy = [System.Drawing.Color]::FromArgb(255, 39, 57, 76)
    $navyLight = [System.Drawing.Color]::FromArgb(255, 58, 78, 99)
    $teal = [System.Drawing.Color]::FromArgb(255, 0, 176, 178)
    $tealLight = [System.Drawing.Color]::FromArgb(255, 46, 206, 205)
    $shadow = [System.Drawing.Color]::FromArgb(36, 15, 23, 34)

    $shadowPen = [System.Drawing.Pen]::new($shadow, (S 9))
    $shadowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $shadowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $shadowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $gridPen = [System.Drawing.Pen]::new($navy, (S 10))
    $gridPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $gridPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $gridPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $gridHighlightPen = [System.Drawing.Pen]::new($navyLight, (S 2))
    $gridHighlightPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $gridHighlightPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $routePen = [System.Drawing.Pen]::new($teal, (S 11))
    $routePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $routePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $routePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $routeHighlightPen = [System.Drawing.Pen]::new($tealLight, (S 2))
    $routeHighlightPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $routeHighlightPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $routeHighlightPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $nodeBrush = [System.Drawing.SolidBrush]::new($teal)
    $nodeInnerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::Transparent)

    # Slight offset shadow keeps the icon readable on both light and dark taskbars.
    $outerShadow = New-RoundedPath (S 41) (S 44) (S 178) (S 170) (S 20)
    $graphics.DrawPath($shadowPen, $outerShadow)
    $outerShadow.Dispose()

    # Main abstract ACPI table grid.
    $outer = New-RoundedPath (S 37) (S 39) (S 178) (S 170) (S 20)
    $graphics.DrawPath($gridPen, $outer)
    $graphics.DrawPath($gridHighlightPen, $outer)

    # Grid divisions: three columns and three rows.
    foreach ($x in @(96, 154)) {
        $graphics.DrawLine($gridPen, (S $x), (S 42), (S $x), (S 206))
        $graphics.DrawLine($gridHighlightPen, (S $x), (S 42), (S $x), (S 206))
    }
    foreach ($y in @(98, 153)) {
        $graphics.DrawLine($gridPen, (S 40), (S $y), (S 212), (S $y))
        $graphics.DrawLine($gridHighlightPen, (S 40), (S $y), (S 212), (S $y))
    }

    # Hide the lower-left inner edge to reproduce the stepped silhouette in the reference image.
    $eraser = [System.Drawing.Pen]::new([System.Drawing.Color]::Transparent, (S 16))
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawLine($eraser, (S 39), (S 157), (S 39), (S 205))
    $graphics.DrawLine($eraser, (S 39), (S 205), (S 93), (S 205))
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $eraser.Dispose()

    # Highlighted namespace/debug path.
    $route = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $route.StartFigure()
    $route.AddLine((S 61), (S 72), (S 111), (S 72))
    $route.AddLine((S 111), (S 72), (S 111), (S 177))
    $route.AddLine((S 111), (S 177), (S 188), (S 177))
    $graphics.DrawPath($shadowPen, $route)
    $graphics.DrawPath($routePen, $route)
    $graphics.DrawPath($routeHighlightPen, $route)
    $route.Dispose()

    foreach ($point in @(@(61,72), @(188,177))) {
        $graphics.FillEllipse($nodeBrush, (S ($point[0] - 14)), (S ($point[1] - 14)), (S 28), (S 28))
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.FillEllipse($nodeInnerBrush, (S ($point[0] - 6)), (S ($point[1] - 6)), (S 12), (S 12))
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    }

    $outer.Dispose()
    $shadowPen.Dispose()
    $gridPen.Dispose()
    $gridHighlightPen.Dispose()
    $routePen.Dispose()
    $routeHighlightPen.Dispose()
    $nodeBrush.Dispose()
    $nodeInnerBrush.Dispose()
    $graphics.Dispose()

    return $bitmap
}

$master = New-AppIconBitmap 512
$pngPath = Join-Path $OutputDirectory 'AppIcon.png'
$master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$master.Dispose()

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bitmap = New-AppIconBitmap $size
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    $stream.Dispose()
    $bitmap.Dispose()
}

$icoPath = Join-Path $OutputDirectory 'AppIcon.ico'
$file = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $dimension = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Dispose()
$file.Dispose()

Write-Host "Generated $pngPath"
Write-Host "Generated $icoPath"
