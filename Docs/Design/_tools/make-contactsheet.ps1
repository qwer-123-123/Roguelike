param(
    [Parameter(Mandatory=$true)][string]$SrcDir,
    [Parameter(Mandatory=$true)][string]$OutFile,
    [int]$Cols = 10,
    [int]$Cell = 200,
    [int]$Start = 0,
    [int]$Count = 9999
)

Add-Type -AssemblyName System.Drawing

$files = Get-ChildItem -Path $SrcDir -File -Filter *.png | Sort-Object Name | Select-Object -Skip $Start -First $Count
$n = $files.Count
$rows = [Math]::Ceiling($n / $Cols)

$labelH = 22
$cellH  = $Cell + $labelH
$W = $Cols * $Cell
$H = $rows * $cellH

$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 28, 30, 34))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

$font  = New-Object System.Drawing.Font("Consolas", 9)
$brush = [System.Drawing.Brushes]::White
$pen   = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 70, 75, 85))

for ($i = 0; $i -lt $n; $i++) {
    $col = $i % $Cols
    $row = [Math]::Floor($i / $Cols)
    $x = $col * $Cell
    $y = $row * $cellH

    $img = $null
    try { $img = [System.Drawing.Image]::FromFile($files[$i].FullName) } catch { }
    if ($img -ne $null) {
        $pad = 6
        $boxW = $Cell - $pad * 2
        $boxH = $Cell - $pad * 2
        $scale = [Math]::Min($boxW / $img.Width, $boxH / $img.Height)
        if ($scale -gt 1) { $scale = 1 }
        $dw = [int]($img.Width * $scale)
        $dh = [int]($img.Height * $scale)
        $dx = $x + [int](($Cell - $dw) / 2)
        $dy = $y + [int](($Cell - $dh) / 2)
        # checkerboard behind so transparency is visible
        $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,44,46,52))), $x+2, $y+2, $Cell-4, $Cell-4)
        $g.DrawImage($img, $dx, $dy, $dw, $dh)
        $img.Dispose()
    }

    $g.DrawRectangle($pen, $x, $y, $Cell, $cellH)
    $name = $files[$i].BaseName
    if ($name.Length -gt 26) { $name = $name.Substring(0, 26) }
    $g.DrawString("$($files[$i].BaseName)", $font, $brush, $x + 4, $y + $Cell + 3)
}

$g.Dispose()
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "OK $OutFile  ($n files, $Cols x $rows)"
