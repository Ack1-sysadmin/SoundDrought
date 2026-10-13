param([string]$Out, [string]$PreviewPng)
Add-Type -AssemblyName System.Drawing

# SoundDrought icon: a white sun on a hot orange square; the sun's disc is cut by equalizer bars.
function New-Bitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Background: rounded square, warm gradient
    $r = [single]($s * 0.22); $w = [single]($s - 1)
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bg.AddArc(0, 0, 2*$r, 2*$r, 180, 90)
    $bg.AddArc($w - 2*$r, 0, 2*$r, 2*$r, 270, 90)
    $bg.AddArc($w - 2*$r, $w - 2*$r, 2*$r, 2*$r, 0, 90)
    $bg.AddArc(0, $w - 2*$r, 2*$r, 2*$r, 90, 90)
    $bg.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255,255,176,32)), ([System.Drawing.Color]::FromArgb(255,255,72,0)), 60
    $g.FillPath($grad, $bg)

    $white = [System.Drawing.Brushes]::White
    $cx = $s / 2.0; $cy = $s / 2.0
    $big = $s -ge 32

    # Sun rays (only where there is room for them)
    $disc = if ($big) { $s * 0.25 } else { $s * 0.34 }
    if ($big) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]($s * 0.055))
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        for ($i = 0; $i -lt 8; $i++) {
            $a = [Math]::PI * 2 * $i / 8
            $r1 = $disc + $s * 0.07; $r2 = $disc + $s * 0.15
            $g.DrawLine($pen, [single]($cx + [Math]::Cos($a)*$r1), [single]($cy + [Math]::Sin($a)*$r1),
                              [single]($cx + [Math]::Cos($a)*$r2), [single]($cy + [Math]::Sin($a)*$r2))
        }
    }

    # Sun disc
    $g.FillEllipse($white, [single]($cx - $disc), [single]($cy - $disc), [single](2*$disc), [single](2*$disc))

    # Equalizer bars cut into the disc
    $barColor = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,255,96,0))
    $heights = 0.45, 0.85, 0.6
    $bw = [Math]::Max(1.0, $disc * 0.26)
    $gap = $bw * 0.55
    $x0 = $cx - (3*$bw + 2*$gap) / 2
    for ($i = 0; $i -lt 3; $i++) {
        $h = 2 * $disc * $heights[$i] * 0.8
        $x = $x0 + $i * ($bw + $gap)
        $y = $cy + $disc * 0.55 - $h
        $g.FillRectangle($barColor, [single]$x, [single]$y, [single]$bw, [single]$h)
    }
    $g.Dispose()
    return $bmp
}

function To-Png($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = @($sizes | ForEach-Object { $b = New-Bitmap $_; ,(To-Png $b) })
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $frames[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$d.Length); $bw.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $frames) { $bw.Write($d) }
$bw.Close()

if ($PreviewPng) {
    # Contact sheet: every size side by side on a dark background, for eyeballing
    $sheet = New-Object System.Drawing.Bitmap 600, 280
    $g = [System.Drawing.Graphics]::FromImage($sheet)
    $g.Clear([System.Drawing.Color]::FromArgb(255,32,32,32))
    $x = 8
    foreach ($s in $sizes) { $b = New-Bitmap $s; $g.DrawImage($b, $x, 8, $s, $s); $x += $s + 8 }
    $g.Dispose()
    $sheet.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png)
}

