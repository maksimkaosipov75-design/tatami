# Renders the brand mark to PNGs and packs a multi-size app icon.
#
# The geometry here mirrors docs/brand/icon.svg by hand rather than rasterising
# the SVG: .NET has no SVG renderer, and pulling in a converter for one icon
# would be a heavier dependency than redrawing a dozen rounded rectangles.
# If icon.svg changes, change the shapes below to match.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outputDir = Join-Path $PSScriptRoot '..\docs\brand'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Palette comes from theme/mocha.json - the single source of truth.
$theme = Get-Content (Join-Path $PSScriptRoot '..\theme\mocha.json') -Raw | ConvertFrom-Json
$c = $theme.colors
function Brush([string]$hex) { [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex)) }

function Add-RoundedRect {
    param($Path, [single]$X, [single]$Y, [single]$W, [single]$H, [single]$R)
    $d = $R * 2
    $Path.AddArc($X, $Y, $d, $d, 180, 90)
    $Path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $Path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $Path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $Path.CloseFigure()
}

function New-MarkBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Everything below is authored on a 512 grid and scaled to the target size.
    $s = $Size / 512.0
    function P([single]$v) { return [single]($v * $s) }

    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $bg 0 0 (P 512) (P 512) (P 116)
    $g.FillPath((Brush $c.base), $bg)

    # status bar
    $bar = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $bar (P 96) (P 86) (P 320) (P 26) (P 13)
    $g.FillPath((Brush $c.overlay0), $bar)

    # tiled panes - the uneven split is the point
    $left = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $left (P 96) (P 140) (P 176) (P 244) (P 22)
    $g.FillPath((Brush $c.mauve), $left)

    $topRight = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $topRight (P 288) (P 140) (P 128) (P 114) (P 22)
    $g.FillPath((Brush $c.blue), $topRight)

    $bottomRight = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $bottomRight (P 288) (P 270) (P 128) (P 114) (P 22)
    $g.FillPath((Brush $c.green), $bottomRight)

    # dock - dropped below 32px, where three 13/512 dots collapse into an
    # indistinct smudge and only add noise. The panes and the bar carry the mark
    # at those sizes.
    if ($Size -ge 32) {
        $dot = Brush $c.text
        foreach ($cx in 212, 256, 300) {
            $r = P 13
            $g.FillEllipse($dot, (P $cx) - $r, (P 428) - $r, $r * 2, $r * 2)
        }
    }

    $g.Dispose()
    return $bmp
}

# --- PNGs (README, GitHub social preview, anything that wants a raster) ---
foreach ($size in 32, 64, 128, 256, 512) {
    $bmp = New-MarkBitmap -Size $size
    $path = Join-Path $outputDir "icon-$size.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "wrote $path"
}

# --- .ico ---
# Built by hand: Icon.Save() only ever emits a single image, and an app icon
# wants every size Windows asks for (taskbar, alt-tab, explorer, properties).
# Entries are stored PNG-compressed, which Windows has supported since Vista.
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()

foreach ($size in $sizes) {
    $bmp = New-MarkBitmap -Size $size
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $images += , @{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose()
}

$icoPath = Join-Path $outputDir 'omarchy.ico'
$fs = [System.IO.File]::Create($icoPath)
$w = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$w.Write([uint16]0)                 # reserved
$w.Write([uint16]1)                 # type: icon
$w.Write([uint16]$images.Count)

# ICONDIRENTRY per image; offsets follow the directory
$offset = 6 + (16 * $images.Count)
foreach ($img in $images) {
    # 256 is stored as 0 in the single-byte width/height fields
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte]$dim)            # width
    $w.Write([byte]$dim)            # height
    $w.Write([byte]0)               # palette count
    $w.Write([byte]0)               # reserved
    $w.Write([uint16]1)             # colour planes
    $w.Write([uint16]32)            # bits per pixel
    $w.Write([uint32]$img.Bytes.Length)
    $w.Write([uint32]$offset)
    $offset += $img.Bytes.Length
}

foreach ($img in $images) { $w.Write($img.Bytes) }

$w.Flush(); $w.Dispose(); $fs.Dispose()
Write-Host "wrote $icoPath ($((Get-Item $icoPath).Length) bytes, $($images.Count) sizes)"
