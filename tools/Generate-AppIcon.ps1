param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\LiangTu.App\Assets')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $scale = $Size / 256.0
    $graphics.ScaleTransform($scale, $scale)

    $backgroundPath = New-RoundedRectanglePath 8 8 240 240 54
    $backgroundBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#252A30'))
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $edgePen = [System.Drawing.Pen]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#3A4047'),
        2)
    $graphics.DrawPath($edgePen, $backgroundPath)

    $ropeShadowPen = [System.Drawing.Pen]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#0F1411'),
        11)
    $ropeShadowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ropeShadowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ropeShadowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $ropeShadowPath.AddBezier(34, 78, 83, 104, 173, 104, 222, 78)
    $graphics.DrawPath($ropeShadowPen, $ropeShadowPath)

    $ropePen = [System.Drawing.Pen]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#E7E4D8'),
        7)
    $ropePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ropePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ropePath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $ropePath.AddBezier(34, 73, 83, 99, 173, 99, 222, 73)
    $graphics.DrawPath($ropePen, $ropePath)

    $cardShadowPath = New-RoundedRectanglePath 78 93 134 112 15
    $cardShadowBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(64, 9, 14, 12))
    $graphics.FillPath($cardShadowBrush, $cardShadowPath)

    $cardPath = New-RoundedRectanglePath 78 88 134 112 15
    $cardBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#F5F5F2'))
    $graphics.FillPath($cardBrush, $cardPath)
    $cardEdgePen = [System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(210, 255, 255, 255),
        2)
    $graphics.DrawPath($cardEdgePen, $cardPath)

    $photoPath = New-RoundedRectanglePath 87 97 116 82 8
    $photoBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#DDE8E1'))
    $graphics.FillPath($photoBrush, $photoPath)

    $sunBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#E2C36D'))
    $graphics.FillEllipse($sunBrush, 171, 108, 22, 22)

    $mountainBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#4D715F'))
    $mountainPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $mountainPath.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(91, 179),
        [System.Drawing.PointF]::new(126, 128),
        [System.Drawing.PointF]::new(156, 179)))
    $graphics.FillPath($mountainBrush, $mountainPath)

    $frontMountainBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#6E957D'))
    $frontMountainPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $frontMountainPath.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(132, 179),
        [System.Drawing.PointF]::new(171, 137),
        [System.Drawing.PointF]::new(201, 179)))
    $graphics.FillPath($frontMountainBrush, $frontMountainPath)

    $clipShadowPath = New-RoundedRectanglePath 134 65 20 34 7
    $clipShadowBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(72, 8, 13, 10))
    $graphics.FillPath($clipShadowBrush, $clipShadowPath)

    $clipPath = New-RoundedRectanglePath 132 60 20 34 7
    $clipBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#F0EFE8'))
    $graphics.FillPath($clipBrush, $clipPath)
    $clipEdgePen = [System.Drawing.Pen]::new(
        [System.Drawing.ColorTranslator]::FromHtml('#AEB5B0'),
        1.5)
    $graphics.DrawPath($clipEdgePen, $clipPath)
    $clipSeamPen = [System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(100, 79, 91, 83),
        2)
    $graphics.DrawLine($clipSeamPen, 142, 69, 142, 85)

    $graphics.Dispose()
    $backgroundPath.Dispose()
    $backgroundBrush.Dispose()
    $edgePen.Dispose()
    $ropeShadowPen.Dispose()
    $ropeShadowPath.Dispose()
    $ropePen.Dispose()
    $ropePath.Dispose()
    $cardShadowPath.Dispose()
    $cardShadowBrush.Dispose()
    $cardPath.Dispose()
    $cardBrush.Dispose()
    $cardEdgePen.Dispose()
    $photoPath.Dispose()
    $photoBrush.Dispose()
    $sunBrush.Dispose()
    $mountainBrush.Dispose()
    $mountainPath.Dispose()
    $frontMountainBrush.Dispose()
    $frontMountainPath.Dispose()
    $clipShadowPath.Dispose()
    $clipShadowBrush.Dispose()
    $clipPath.Dispose()
    $clipBrush.Dispose()
    $clipEdgePen.Dispose()
    $clipSeamPen.Dispose()
    return $bitmap
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    if ($size -eq 256) {
        [System.IO.File]::WriteAllBytes(
            (Join-Path $OutputDirectory 'liangtu-256.png'),
            $bytes)
    }

    $bitmap.Dispose()
    [pscustomobject]@{
        Size = $size
        Bytes = $bytes
    }
}

$iconPath = Join-Path $OutputDirectory 'liangtu.ico'
$file = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Output "Generated $iconPath with $($frames.Count) sizes."
