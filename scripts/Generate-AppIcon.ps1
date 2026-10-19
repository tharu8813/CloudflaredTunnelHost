# Generate multi-resolution ICO frames from the original SVG. Run in Windows PowerShell (STA).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$projectRoot = Split-Path -Parent $PSScriptRoot
$svg = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Assets\Branding\app.svg') -Raw)
$geometry = [Windows.Media.Geometry]::Parse($svg.svg.path.d)
$frames = @()
foreach ($size in @(16,32,48,256)) {
    $visual = New-Object Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    $drawing.DrawRoundedRectangle([Windows.Media.BrushConverter]::new().ConvertFromString('#10151C'), $null, [Windows.Rect]::new(0,0,$size,$size), ($size/5), ($size/5))
    $drawing.PushTransform([Windows.Media.TranslateTransform]::new(($size/8),($size/8)))
    $drawing.PushTransform([Windows.Media.ScaleTransform]::new(($size/32),($size/32)))
    $pen = [Windows.Media.Pen]::new([Windows.Media.BrushConverter]::new().ConvertFromString('#FFB26B'),1.7)
    $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
    $drawing.DrawGeometry($null,$pen,$geometry)
    $drawing.Pop(); $drawing.Pop(); $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [IO.MemoryStream]::new(); $encoder.Save($memory)
    $frames += ,@($size,$memory.ToArray()); $memory.Dispose()
}
$file = [IO.File]::Create((Join-Path $projectRoot 'Assets\Branding\app.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame[0] -eq 256) { 0 } else { $frame[0] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
        $offset += $frame[1].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
} finally { $writer.Dispose() }
