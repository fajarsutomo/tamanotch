# Membuat ikon aplikasi TAMANOTCH (Assets\app.ico dan Assets\app.png) dari kode:
# kotak membulat gelap bertepi neon dengan dua mata menyala, turunan dari tampilan notch idle.
# Pemakaian: powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1 [-GlowHex 35D6FF] [-EyeHex E6F8FF]
param(
    [string]$OutDir = (Join-Path $PSScriptRoot "..\src\TamaNotch\Assets"),
    [string]$GlowHex = "35D6FF",
    [string]$EyeHex = "E6F8FF"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function ConvertTo-Color([string]$hex, [int]$alpha = 255) {
    $h = $hex.TrimStart('#')
    return [System.Drawing.Color]::FromArgb($alpha, [Convert]::ToInt32($h.Substring(0, 2), 16), [Convert]::ToInt32($h.Substring(2, 2), 16), [Convert]::ToInt32($h.Substring(4, 2), 16))
}

function New-RoundedRectPath([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [single]($r * 2)
    $p.AddArc([single]$x, [single]$y, $d, $d, 180, 90)
    $p.AddArc([single]($x + $w - $d), [single]$y, $d, $d, 270, 90)
    $p.AddArc([single]($x + $w - $d), [single]($y + $h - $d), $d, $d, 0, 90)
    $p.AddArc([single]$x, [single]($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Menggambar satu ikon berukuran $size px (dirender 4x lalu diperkecil agar tepinya halus).
function New-IconBitmap([int]$size) {
    $ss = 4
    $big = $size * $ss
    $bmp = New-Object System.Drawing.Bitmap($big, $big, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $small = $size -le 32
    $pad = $big * $(if ($small) { 0.07 } else { 0.10 })
    $body = $big - 2 * $pad
    $radius = $body * 0.27
    $path = New-RoundedRectPath $pad $pad $body $body $radius

    # Cahaya luar (lapisan garis tebal makin transparan)
    $glowSteps = $(if ($small) { 5 } else { 14 })
    $glowStep = $big * $(if ($small) { 0.012 } else { 0.0085 })
    for ($i = $glowSteps; $i -ge 1; $i--) {
        $a = [int](46 * [Math]::Pow(1 - ($i / ($glowSteps + 1)), 2.2))
        $pen = New-Object System.Drawing.Pen((ConvertTo-Color $GlowHex $a), [single]($big * 0.02 + $i * $glowStep * 2))
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $path)
        $pen.Dispose()
    }

    # Badan gelap dengan gradasi tipis
    $rect = New-Object System.Drawing.RectangleF([single]$pad, [single]$pad, [single]$body, [single]$body)
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, (ConvertTo-Color "0B1220"), (ConvertTo-Color "03050A"), 90)
    $g.FillPath($fill, $path)
    $fill.Dispose()

    # Tepi neon
    $borderW = [Math]::Max($ss * 1.3, $big * $(if ($small) { 0.045 } else { 0.03 }))
    $edge = New-Object System.Drawing.Pen((ConvertTo-Color $GlowHex 255), [single]$borderW)
    $edge.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($edge, $path)
    $edge.Dispose()

    # Mata: dua elips tegak dengan cahaya lembut
    $cx = $big / 2
    $cy = $big * 0.52
    $ew = $big * $(if ($small) { 0.15 } else { 0.115 })
    $eh = $big * $(if ($small) { 0.25 } else { 0.20 })
    $gap = $big * 0.125
    foreach ($sx in @(-1, 1)) {
        $ex = $cx + $sx * $gap
        $eyeSteps = $(if ($small) { 3 } else { 9 })
        for ($i = $eyeSteps; $i -ge 1; $i--) {
            $grow = $i * $big * $(if ($small) { 0.010 } else { 0.0075 })
            $a = [int](55 * [Math]::Pow(1 - ($i / ($eyeSteps + 1)), 2))
            $b = New-Object System.Drawing.SolidBrush((ConvertTo-Color $GlowHex $a))
            $g.FillEllipse($b, [single]($ex - $ew / 2 - $grow), [single]($cy - $eh / 2 - $grow), [single]($ew + 2 * $grow), [single]($eh + 2 * $grow))
            $b.Dispose()
        }
        $eyeBrush = New-Object System.Drawing.SolidBrush((ConvertTo-Color $EyeHex))
        $g.FillEllipse($eyeBrush, [single]($ex - $ew / 2), [single]($cy - $eh / 2), [single]$ew, [single]$eh)
        $eyeBrush.Dispose()
    }

    $path.Dispose()
    $g.Dispose()

    $out = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $og = [System.Drawing.Graphics]::FromImage($out)
    $og.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $og.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $og.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $og.Clear([System.Drawing.Color]::Transparent)
    $og.DrawImage($bmp, 0, 0, $size, $size)
    $og.Dispose()
    $bmp.Dispose()
    return $out
}

function ConvertTo-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return $bytes
}

$outDirFull = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force $outDirFull | Out-Null

# app.png (512 px)
$png = New-IconBitmap 512
$png.Save((Join-Path $outDirFull "app.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()

# app.ico: beberapa ukuran, tiap entri berisi PNG (format ICO modern)
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = New-Object System.Collections.Generic.List[object]
foreach ($s in $sizes) {
    $b = New-IconBitmap $s
    $pngBytes = [byte[]](ConvertTo-PngBytes $b)
    $images.Add([pscustomobject]@{ Size = $s; Data = $pngBytes })
    $b.Dispose()
}

$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($ico)
$w.Write([uint16]0)               # reserved
$w.Write([uint16]1)               # type: icon
$w.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = $(if ($img.Size -ge 256) { 0 } else { $img.Size })
    $w.Write([byte]$dim)
    $w.Write([byte]$dim)
    $w.Write([byte]0)             # palette
    $w.Write([byte]0)             # reserved
    $w.Write([uint16]1)           # planes
    $w.Write([uint16]32)          # bit depth
    $w.Write([uint32]$img.Data.Length)
    $w.Write([uint32]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $w.Write($img.Data) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $outDirFull "app.ico"), $ico.ToArray())
$w.Dispose()

Write-Host "Ikon dibuat di $outDirFull (app.png 512px; app.ico: $($sizes -join ', ') px)" -ForegroundColor Green
