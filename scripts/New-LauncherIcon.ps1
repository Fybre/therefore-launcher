$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconDirectory = Join-Path $PSScriptRoot '..\assets\icons'
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size * 4, $size * 4)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scale = $size * 4 / 256.0
    $graphics.ScaleTransform($scale, $scale)
    $background = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#353535'))
    $gold = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#E4B900'))
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(8, 8, 64, 64, 180, 90)
    $path.AddArc(184, 8, 64, 64, 270, 90)
    $path.AddArc(184, 184, 64, 64, 0, 90)
    $path.AddArc(8, 184, 64, 64, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath($background, $path)
    # Three dots echo the launcher's existing therefore symbol.
    $graphics.FillEllipse($gold, 88, 48, 44, 44)
    $graphics.FillEllipse($gold, 48, 112, 44, 44)
    $graphics.FillEllipse($gold, 128, 112, 44, 44)
    # A northeast arrow identifies this as the launcher rather than a client.
    $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#E4B900'), 16)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($pen, 164, 210, 212, 162)
    $graphics.DrawLine($pen, 178, 162, 212, 162)
    $graphics.DrawLine($pen, 212, 162, 212, 196)
    $output = [System.Drawing.Bitmap]::new($size, $size)
    $resizer = [System.Drawing.Graphics]::FromImage($output)
    $resizer.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $resizer.DrawImage($bitmap, 0, 0, $size, $size)
    $stream = [System.IO.MemoryStream]::new()
    $output.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@{ Size = $size; Bytes = $stream.ToArray() }
    if ($size -eq 256) { $output.Save((Join-Path $iconDirectory 'launcher.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $resizer.Dispose(); $output.Dispose()
    $pen.Dispose(); $path.Dispose(); $gold.Dispose(); $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$iconPath = Join-Path $iconDirectory 'launcher.ico'
$file = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length); $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output "Created launcher.ico with $($sizes -join ', ') pixel sizes and launcher.png preview."
