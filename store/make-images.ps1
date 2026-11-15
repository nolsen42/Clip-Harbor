# Draws the store images in this folder (Windows PowerShell, uses System.Drawing).
#   1. Make a picture of the GIF download window with a neutral folder name:
#        $env:CLIPHARBOR_PREVIEW='gif'; $env:CLIPHARBOR_PREVIEW_FOLDER='C:\Users\you\Videos\ClipHarbor'
#        ClipHarbor.exe --screenshot shot.png      (this also writes shot.dialog.png)
#   2. powershell -File store\make-images.ps1 -Repo . -Dialog shot.dialog.png
param([string]$Repo, [string]$Dialog)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$out = Join-Path $Repo 'store'
New-Item -ItemType Directory -Force $out | Out-Null
$dot = [string][char]0x00B7
$icon = [System.Drawing.Image]::FromFile((Join-Path $Repo 'assets\ClipHarbor.png'))

function New-Canvas($w, $h) {
    $b = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    return @($b, $g)
}
function Color($hex, $a = 255) { $c = [System.Drawing.ColorTranslator]::FromHtml($hex); [System.Drawing.Color]::FromArgb($a, $c) }
function Background($g, $w, $h) {
    $r = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush($r, (Color '#3B7BF5'), (Color '#1C3F94'), 35)
    $g.FillRectangle($br, $r); $br.Dispose()
}
function RoundRect($x, $y, $w, $h, $rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $rad * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}
function Shadow($g, $x, $y, $w, $h, $rad) {
    for ($i = 1; $i -le 12; $i++) {
        $p = RoundRect ($x - $i + 2) ($y - $i + 8) ($w + 2 * $i - 4) ($h + 2 * $i - 4) ($rad + $i)
        $br = New-Object System.Drawing.SolidBrush (Color '#000000' 7)
        $g.FillPath($br, $p); $br.Dispose(); $p.Dispose()
    }
}
function Fill($g, $hex, $x, $y, $w, $h, $rad, $a = 255) {
    $p = RoundRect $x $y $w $h $rad; $br = New-Object System.Drawing.SolidBrush (Color $hex $a); $g.FillPath($br, $p); $br.Dispose(); $p.Dispose()
}
function Text($g, $s, $size, $hex, $x, $y, $style = 'Regular', $w = 0, $a = 255) {
    $f = New-Object System.Drawing.Font('Segoe UI', $size, [System.Drawing.FontStyle]$style, [System.Drawing.GraphicsUnit]::Pixel)
    $br = New-Object System.Drawing.SolidBrush (Color $hex $a)
    if ($w -gt 0) { $g.DrawString($s, $f, $br, (New-Object System.Drawing.RectangleF($x, $y, $w, 400))) } else { $g.DrawString($s, $f, $br, $x, $y) }
    $br.Dispose(); $f.Dispose()
}
function Save($b, $g, $name) { $g.Dispose(); $b.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose() }

# --- Store icon: 96 px artwork with 16 px transparent padding, as Chrome asks ---
$b, $g = New-Canvas 128 128
$g.DrawImage($icon, 16, 16, 96, 96)
Save $b $g 'store-icon-128.png'

# --- Screenshot 1: the right-click menu on a post ---
$b, $g = New-Canvas 1280 800
Background $g 1280 800
# caption
$g.DrawImage($icon, 70, 90, 72, 72)
Text $g 'Right-click.' 48 '#FFFFFF' 66 196 'Bold'
Text $g 'Send to ClipHarbor.' 48 '#FFFFFF' 66 254 'Bold'
Text $g 'Save any video, link or X post, or turn it into a GIF, straight from your browser.' 24 '#DCE6FF' 70 350 'Regular' 430
Text $g "Free and open source  $dot  Collects nothing" 19 '#B9CCFA' 70 470
# a generic post card
Shadow $g 560 110 560 560 18
Fill $g '#FFFFFF' 560 110 560 560 18
Fill $g '#C9D6EE' 590 140 52 52 26
Text $g 'Clip Collector' 20 '#14171A' 656 140 'Bold'
Text $g "@clipcollector  $dot  2h" 17 '#6B7785' 656 166
Text $g 'This is exactly how my cat reacts to Mondays' 20 '#14171A' 590 214
Fill $g '#2A3240' 590 256 500 300 14
$tri = [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF(820, 370)), (New-Object System.Drawing.PointF(820, 442)), (New-Object System.Drawing.PointF(880, 406)))
$wb = New-Object System.Drawing.SolidBrush (Color '#FFFFFF' 220); $g.FillPolygon($wb, $tri); $wb.Dispose()
Fill $g '#000000' 600 520 58 26 6 160
Text $g '0:07' 16 '#FFFFFF' 610 522
Fill $g '#E7ECF3' 590 580 70 12 6; Fill $g '#E7ECF3' 700 580 70 12 6; Fill $g '#E7ECF3' 810 580 70 12 6
# the right-click menu
$mx = 860; $my = 300; $mw = 360
Shadow $g $mx $my $mw 252 10
Fill $g '#FFFFFF' $mx $my $mw 252 10
Text $g 'Open link in new tab' 18 '#202124' ($mx + 44) ($my + 12)
Text $g 'Copy link address' 18 '#202124' ($mx + 44) ($my + 46)
$pen = New-Object System.Drawing.Pen (Color '#E3E6EA'), 1; $g.DrawLine($pen, $mx, $my + 86, $mx + $mw, $my + 86)
Fill $g '#E3ECFF' ($mx + 6) ($my + 94) ($mw - 12) 38 6
$g.DrawImage($icon, $mx + 14, $my + 102, 22, 22)
Text $g 'Download with ClipHarbor' 18 '#1B4FBF' ($mx + 44) ($my + 100) 'Bold'
$g.DrawImage($icon, $mx + 14, $my + 142, 22, 22)
Text $g 'Make a GIF with ClipHarbor' 18 '#202124' ($mx + 44) ($my + 140)
$g.DrawLine($pen, $mx, $my + 180, $mx + $mw, $my + 180); $pen.Dispose()
Text $g 'Inspect' 18 '#202124' ($mx + 44) ($my + 196)
Save $b $g 'screenshot-1.png'

# --- Screenshot 2: ClipHarbor's download choices, GIF picked ---
$b, $g = New-Canvas 1280 800
Background $g 1280 800
Text $g 'Choose video, audio' 50 '#FFFFFF' 66 200 'Bold'
Text $g 'or GIF.' 50 '#FFFFFF' 66 262 'Bold'
Text $g 'ClipHarbor opens with the link ready. Pick the quality, GIF size, file name and folder, or turn on Smart Mode to skip this step.' 23 '#DCE6FF' 70 350 'Regular' 440
$d = [System.Drawing.Image]::FromFile($Dialog)
$dx = 580; $dy = (800 - $d.Height) / 2
Shadow $g $dx $dy $d.Width $d.Height 10
Fill $g '#FFFFFF' $dx $dy $d.Width $d.Height 10
$g.DrawImage($d, $dx, $dy, $d.Width, $d.Height); $d.Dispose()
Save $b $g 'screenshot-2.png'

# --- Small promo tile 440x280 ---
$b, $g = New-Canvas 440 280
Background $g 440 280
$g.DrawImage($icon, 40, 52, 84, 84)
Text $g 'Send to' 30 '#DCE6FF' 140 54
Text $g 'ClipHarbor' 40 '#FFFFFF' 138 86 'Bold'
Text $g 'Right-click any video or X post to save it or make a GIF.' 20 '#DCE6FF' 42 168 'Regular' 360
Save $b $g 'promo-small-440x280.png'

# --- Marquee promo tile 1400x560 ---
$b, $g = New-Canvas 1400 560
Background $g 1400 560
$g.DrawImage($icon, 120, 180, 200, 200)
Text $g 'Send to ClipHarbor' 72 '#FFFFFF' 370 170 'Bold'
Text $g 'Right-click any video, link or X post to save it or turn it into a GIF.' 30 '#DCE6FF' 376 270 'Regular' 900
Text $g "Free  $dot  Open source  $dot  Collects nothing" 24 '#B9CCFA' 376 360
Save $b $g 'promo-marquee-1400x560.png'

$icon.Dispose()
Get-ChildItem $out -Filter *.png | ForEach-Object { $i = [System.Drawing.Image]::FromFile($_.FullName); "$($_.Name) $($i.Width)x$($i.Height)"; $i.Dispose() }
