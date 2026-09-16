
param(
    [Parameter(Mandatory=$true)][string]$SrcDir,
    [Parameter(Mandatory=$true)][string]$OutFile,
    [int]$TargetH = 200,
    [string]$Filter = "*.png",
    [switch]$Uniform
)

Add-Type -AssemblyName System.Drawing

$files = Get-ChildItem -Path $SrcDir -File -Filter $Filter | Sort-Object Name
if ($files.Count -eq 0) { Write-Output "SKIP (empty) $SrcDir"; exit }

$imgs = @()
foreach ($f in $files) {
    try { $imgs += [System.Drawing.Image]::FromFile($f.FullName) } catch { }
}
if ($imgs.Count -eq 0) { Write-Output "SKIP (unreadable) $SrcDir"; exit }

# common canvas = max width/height across frames (uniform anims yield same box)
$cw = ($imgs | Measure-Object Width  -Maximum).Maximum
$ch = ($imgs | Measure-Object Height -Maximum).Maximum

$scale = 1.0
if ($TargetH -gt 0 -and $ch -gt $TargetH) { $scale = $TargetH / $ch }
$fw = [int][Math]::Ceiling($cw * $scale)
$fh = [int][Math]::Ceiling($ch * $scale)

$bmp = New-Object System.Drawing.Bitmap(($fw * $imgs.Count), $fh, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

for ($i = 0; $i -lt $imgs.Count; $i++) {
    $img = $imgs[$i]
    $dw = [int][Math]::Ceiling($img.Width  * $scale)
    $dh = [int][Math]::Ceiling($img.Height * $scale)
    # centre horizontally inside the frame box, bottom-align (ground contact stays stable)
    $dx = $i * $fw + [int](($fw - $dw) / 2)
    $dy = $fh - $dh
    $g.DrawImage($img, $dx, $dy, $dw, $dh)
    $img.Dispose()
}
$g.Dispose()

$dir = Split-Path $OutFile -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$size = [Math]::Round((Get-Item $OutFile).Length / 1KB, 1)
Write-Output ("OK {0,-52} frames={1,-3} {2}x{3} ({4}KB)" -f (Split-Path $OutFile -Leaf), $imgs.Count, ($fw*$imgs.Count), $fh, $size)
