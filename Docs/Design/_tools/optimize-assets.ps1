
# Second pass: trim the tile library and cap the resolution of every oversized PNG.
# Nothing here changes what an asset depicts, only how many pixels ship with the doc.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$OUT = "D:\unity\XiangMu\Roguelike\Docs\Design\h5\assets"

function Shrink([string]$path, [int]$maxLong) {
    $img = [System.Drawing.Image]::FromFile($path)
    try {
        $long = [Math]::Max($img.Width, $img.Height)
        if ($long -le $maxLong) { return $false }
        $s  = $maxLong / $long
        $w  = [Math]::Max(1, [int][Math]::Round($img.Width  * $s))
        $h  = [Math]::Max(1, [int][Math]::Round($img.Height * $s))
        $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($img, 0, 0, $w, $h)
        $g.Dispose()
        $img.Dispose()
        # write to a temp file first: .NET still holds a handle on the source
        $tmp = "$path.tmp"
        $bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Move-Item $tmp $path -Force
        return $true
    } finally {
        try { $img.Dispose() } catch { }
    }
}

# ---- 1. tiles: keep the four terrain sheets, drop the 61 building props
Get-ChildItem "$OUT\tile" -File | Where-Object { $_.Name -like 'objects_*' } | ForEach-Object { Remove-Item $_.FullName -Force }

# ---- 2. resolution caps, per category
$rules = @(
    @{ dir = "$OUT\ui\window";   max = 900 },
    @{ dir = "$OUT\ui";          max = 700 },
    @{ dir = "$OUT\portrait";    max = 220 },
    @{ dir = "$OUT\vehicle";     max = 640 },
    @{ dir = "$OUT\tile";        max = 260 },
    @{ dir = "$OUT\item";        max = 160 }
)

foreach ($r in $rules) {
    $n = 0
    $files = Get-ChildItem $r.dir -File -Filter *.png | Sort-Object Length -Descending
    foreach ($f in $files) { if (Shrink $f.FullName $r.max) { $n++ } }
    "shrunk {0,-22} {1} files (cap {2}px)" -f (Split-Path $r.dir -Leaf), $n, $r.max
}

Write-Output ""
Get-ChildItem $OUT -Directory | ForEach-Object {
    $f = Get-ChildItem $_.FullName -Recurse -File
    "{0,-12} files={1,-5} {2,7:N0}KB" -f $_.Name, $f.Count, (($f | Measure-Object Length -Sum).Sum / 1KB)
}
$all = Get-ChildItem $OUT -Recurse -File
"TOTAL        files=$($all.Count)  $([Math]::Round(($all | Measure-Object Length -Sum).Sum/1MB,2))MB"
