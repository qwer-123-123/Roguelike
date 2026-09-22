
param(
    [Parameter(Mandatory=$true)][string]$SrcDir,
    [Parameter(Mandatory=$true)][string]$OutFile,
    [double]$Scale = 0,          # >0 时用固定缩放比（同一角色的所有动画必须传同一个值）
    [int]$TargetH = 200,         # Scale=0 时才生效：把最高帧缩到 TargetH
    [string]$Filter = "*.png"
)

Add-Type -AssemblyName System.Drawing

$files = Get-ChildItem -Path $SrcDir -File -Filter $Filter | Sort-Object Name
if ($files.Count -eq 0) { Write-Output "SKIP (empty) $SrcDir"; exit }

$imgs = @()
foreach ($f in $files) {
    try { $imgs += [System.Drawing.Image]::FromFile($f.FullName) } catch { }
}
if ($imgs.Count -eq 0) { Write-Output "SKIP (unreadable) $SrcDir"; exit }

# 所有帧共用一个画布（同目录帧尺寸一致；Death 等不一致的会被居中放进最大框）
$cw = ($imgs | Measure-Object Width  -Maximum).Maximum
$ch = ($imgs | Measure-Object Height -Maximum).Maximum

# 缩放比：显式指定优先。**必须让同一角色的 idle/walk/atk/death 用同一个值**，
# 否则原始帧更高的动画（枪口火焰、刀光）会被压得更小，游戏里表现为「攻击时缩小」。
$s = if ($Scale -gt 0) { $Scale } elseif ($TargetH -gt 0 -and $ch -gt $TargetH) { $TargetH / $ch } else { 1.0 }

$fw = [int][Math]::Ceiling($cw * $s)
$fh = [int][Math]::Ceiling($ch * $s)

$bmp = New-Object System.Drawing.Bitmap(($fw * $imgs.Count), $fh, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

for ($i = 0; $i -lt $imgs.Count; $i++) {
    $img = $imgs[$i]
    $dw = [int][Math]::Ceiling($img.Width  * $s)
    $dh = [int][Math]::Ceiling($img.Height * $s)
    # 水平居中、**底部对齐**（脚底位置在各动画间保持一致，绘制端据此对齐）
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
Write-Output ("OK {0,-46} frames={1,-3} {2}x{3} scale={4:N3} ({5}KB)" -f `
    (Split-Path $OutFile -Leaf), $imgs.Count, ($fw*$imgs.Count), $fh, $s, $size)
