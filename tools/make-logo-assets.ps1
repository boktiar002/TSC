# Rebuilds every logo asset in wwwroot from one master artwork.
# Run after replacing the master:  powershell -File tools/make-logo-assets.ps1 <master-image>
# System.Drawing is on the box already; nothing to install.

param([string]$Master = "wwwroot/img/tsc-logo-master.jpg")

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$img  = Join-Path $root "wwwroot\img"
$src  = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Master).Path)

# Brand ink, read off the master: deep navy ground, warm cream artwork.
$NavyR = 16; $NavyG = 33; $NavyB = 51
$navyLum = 31.0; $creamLum = 253.0

# --- read the master into a plain byte array (GetPixel over 2M pixels is far too slow) ---
$rect = [System.Drawing.Rectangle]::new(0, 0, $src.Width, $src.Height)
$data = $src.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $src.Height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$src.UnlockBits($data)
$stride = $data.Stride; $w = $src.Width; $h = $src.Height

# Luminance per pixel, normalised so ground = 0 and artwork = 1. That single number drives
# both the trim and the alpha of the cut-out, so the two can never disagree.
$lum = New-Object single[] ($w * $h)
for ($y = 0; $y -lt $h; $y++) {
  $row = $y * $stride
  for ($x = 0; $x -lt $w; $x++) {
    $i = $row + $x * 4
    $l = 0.0722 * $bytes[$i] + 0.7152 * $bytes[$i+1] + 0.2126 * $bytes[$i+2]
    $v = ($l - $navyLum) / ($creamLum - $navyLum)
    $lum[$y * $w + $x] = [Math]::Min(1.0, [Math]::Max(0.0, $v))
  }
}

# --- trim the master's own padding so every output can set its own margin ---
$x0 = $w; $y0 = $h; $x1 = 0; $y1 = 0
for ($y = 0; $y -lt $h; $y++) {
  for ($x = 0; $x -lt $w; $x++) {
    if ($lum[$y * $w + $x] -gt 0.25) {
      if ($x -lt $x0) { $x0 = $x }; if ($x -gt $x1) { $x1 = $x }
      if ($y -lt $y0) { $y0 = $y }; if ($y -gt $y1) { $y1 = $y }
    }
  }
}
$cw = $x1 - $x0 + 1; $ch = $y1 - $y0 + 1
"trimmed artwork: ${cw}x${ch} at ($x0,$y0)"

# The cut-out: artwork in navy ink on transparency, for light surfaces and for print.
$cut = New-Object System.Drawing.Bitmap($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cd = $cut.LockBits([System.Drawing.Rectangle]::new(0,0,$cw,$ch), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cb = New-Object byte[] ($cd.Stride * $ch)
for ($y = 0; $y -lt $ch; $y++) {
  for ($x = 0; $x -lt $cw; $x++) {
    $a = [byte][Math]::Round(255 * $lum[($y + $y0) * $w + ($x + $x0)])
    $i = $y * $cd.Stride + $x * 4
    $cb[$i] = $NavyB; $cb[$i+1] = $NavyG; $cb[$i+2] = $NavyR; $cb[$i+3] = $a
  }
}
[System.Runtime.InteropServices.Marshal]::Copy($cb, 0, $cd.Scan0, $cb.Length)
$cut.UnlockBits($cd)

# Clears every connected run of ink smaller than $MinShare of the biggest one. Four-way
# flood fill over an alpha mask; plenty fast enough for a one-off asset build.
function Remove-Specks {
  param($Source, [single]$MinShare)

  $w = $Source.Width; $h = $Source.Height
  $r = [System.Drawing.Rectangle]::new(0, 0, $w, $h)
  $d = $Source.LockBits($r, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $b = New-Object byte[] ($d.Stride * $h)
  [System.Runtime.InteropServices.Marshal]::Copy($d.Scan0, $b, 0, $b.Length)

  $label = New-Object int[] ($w * $h)       # 0 = not ink, -1 = ink unvisited, else group id
  for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
      if ($b[$y * $d.Stride + $x * 4 + 3] -gt 64) { $label[$y * $w + $x] = -1 }
    }
  }

  $sizes = New-Object System.Collections.Generic.List[int]
  $stack = New-Object System.Collections.Generic.Stack[int]

  for ($s = 0; $s -lt $label.Length; $s++) {
    if ($label[$s] -ne -1) { continue }

    $id = $sizes.Count + 1
    $n = 0
    $stack.Push($s)
    $label[$s] = $id

    while ($stack.Count -gt 0) {
      $p = $stack.Pop(); $n++
      $px = $p % $w; $py = [int][Math]::Floor($p / $w)

      foreach ($q in @(
        $(if ($px -gt 0)      { $p - 1 }  else { -1 }),
        $(if ($px -lt $w - 1) { $p + 1 }  else { -1 }),
        $(if ($py -gt 0)      { $p - $w } else { -1 }),
        $(if ($py -lt $h - 1) { $p + $w } else { -1 }))) {

        if ($q -ge 0 -and $label[$q] -eq -1) { $label[$q] = $id; $stack.Push($q) }
      }
    }
    $sizes.Add($n)
  }

  $floor = 0
  if ($sizes.Count -gt 0) { $floor = [int](($sizes | Measure-Object -Maximum).Maximum * $MinShare) }
  $dropped = 0

  for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
      $id = $label[$y * $w + $x]
      if ($id -gt 0 -and $sizes[$id - 1] -lt $floor) {
        $b[$y * $d.Stride + $x * 4 + 3] = 0
        $dropped++
      }
    }
  }

  [System.Runtime.InteropServices.Marshal]::Copy($b, 0, $d.Scan0, $b.Length)
  $Source.UnlockBits($d)
  Write-Host "  cleared $dropped speck px from $($sizes.Count) shapes (floor $floor)"
  return $Source
}

function Save-Scaled {
  param($Source, [int]$Size, [single]$Pad, $Ground, $Ink, [single]$Alpha, [string]$Out)

  $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  if ($Ground) { $g.Clear($Ground) } else { $g.Clear([System.Drawing.Color]::Transparent) }

  $box = $Size * (1 - 2 * $Pad)
  $scale = [Math]::Min($box / $Source.Width, $box / $Source.Height)
  $dw = $Source.Width * $scale; $dh = $Source.Height * $scale
  $dest = [System.Drawing.Rectangle]::new([int][Math]::Round(($Size - $dw) / 2), [int][Math]::Round(($Size - $dh) / 2), [int][Math]::Round($dw), [int][Math]::Round($dh))

  $attr = New-Object System.Drawing.Imaging.ImageAttributes
  $m = New-Object System.Drawing.Imaging.ColorMatrix
  $m.Matrix33 = $Alpha

  # The cut-out is one flat ink, so recolouring it is just a translation. That lets a single
  # asset serve both the navy-on-light and the cream-on-navy lockups.
  if ($Ink) {
    $m.Matrix00 = 0; $m.Matrix11 = 0; $m.Matrix22 = 0
    $m.Matrix40 = $Ink.R / 255.0; $m.Matrix41 = $Ink.G / 255.0; $m.Matrix42 = $Ink.B / 255.0
  }
  $attr.SetColorMatrix($m)
  $g.DrawImage($Source, $dest, 0, 0, $Source.Width, $Source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)

  $g.Dispose()
  $path = Join-Path $img $Out
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Host "  wrote $Out ($Size px)"
  return $bmp
}

$navy  = [System.Drawing.Color]::FromArgb(255, $NavyR, $NavyG, $NavyB)
$cream = [System.Drawing.Color]::FromArgb(255, 254, 253, 243)

# The logo proper: crest in navy ink on transparency, for every light surface — the hero,
# the report masthead, the receipt and the PDF.
(Save-Scaled $cut 512 0.00 $null $null 1.0 "tsc-logo.png").Dispose()

# Below about 80px the banner lettering and the laurel detail collapse into a grey blob, so
# small sizes get the inner emblem — mortarboard over open book — on its own. Fractions of
# the trimmed crest; re-measure them if the artwork is ever redrawn.
$mx = [int]($cw * 0.262); $my = [int]($ch * 0.185)
$mark = $cut.Clone(
  [System.Drawing.Rectangle]::new($mx, $my, [int]($cw * 0.476), [int]($ch * 0.420)),
  [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# A rectangle cannot follow the crest's outline, so the crop also catches slivers of the
# surrounding ring and laurels, which read as scratches rather than as part of the mark.
# The cap and the book are far and away the biggest shapes in there, so keeping only the
# large connected components clears the debris without hand-tuning the box any further.
$mark = Remove-Specks $mark 0.06

# Navy ink for the cream brand chips in the sidebar, the public bar and the sign-in card.
(Save-Scaled $mark 256 0.02 $null $null 1.0 "tsc-mark.png").Dispose()

# Pre-faded on disk, because print engines drop opacity and a watermark that
# vanishes on paper is no watermark.
(Save-Scaled $cut 760 0.00 $null $null 0.07 "tsc-watermark.png").Dispose()

# Favicon and home-screen tile: the emblem on its own ground, because a transparent cut-out
# disappears into whatever chrome the browser or phone puts behind it.
$icon = Save-Scaled $mark 256 0.14 $navy $cream 1.0 "tsc-icon.png"

# ICO with PNG-compressed entries, which every browser since Vista reads.
$png = New-Object System.IO.MemoryStream
$icon.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$p = $png.ToArray()
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ico)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)      # reserved, type=icon, count
$bw.Write([byte]0); $bw.Write([byte]0)                                # 0 = 256px
$bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$p.Length); $bw.Write([uint32]22)
$bw.Write($p); $bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $root "wwwroot\favicon.ico"), $ico.ToArray())
"  wrote favicon.ico"

$icon.Dispose(); $mark.Dispose(); $cut.Dispose(); $src.Dispose()
