param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$masterPath = Join-Path $projectRoot 'assets/branding/app-icon-tight.png'
$source = [System.Drawing.Bitmap]::new($masterPath)

function Convert-IconPng([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = [System.IO.MemoryStream]::new()
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}

try {
    if ($source.Width -ne $source.Height) { throw 'The app icon master must be square.' }
    # The tightly framed edition keeps the visible tile large at taskbar sizes.
    [System.IO.File]::WriteAllBytes((Join-Path $projectRoot 'assets/ui/app-icon.png'), (Convert-IconPng 256))
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $frames = @($sizes | ForEach-Object { ,(Convert-IconPng $_) })
    $output = [System.IO.File]::Create((Join-Path $projectRoot 'assets/app.ico'))
    $writer = [System.IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $output.Dispose() }
    Write-Output 'Prepared the 256px header image and Windows icon (16–256px).'
} finally { $source.Dispose() }
