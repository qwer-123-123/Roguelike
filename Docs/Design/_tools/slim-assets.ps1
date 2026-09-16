
# Slim the bundle: drop the huge PSD avatars, flatten the layered vehicle art,
# and cut the tile library down to a representative set.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$OUT = "D:\unity\XiangMu\Roguelike\Docs\Design\h5\assets"

# ---- 1. avatars: 60MB of PSD + full-res originals; portraits come from the character icons instead
if (Test-Path "$OUT\portrait\ava") { Remove-Item "$OUT\portrait\ava" -Recurse -Force; "dropped portrait\ava" }

# ---- 2. vehicles: the pack ships them as stacked layers. Flatten each car into one PNG.
$vehRoot = "$OUT\vehicle"
foreach ($dir in Get-ChildItem $vehRoot -Directory) {
    $layers = Get-ChildItem $dir.FullName -File -Filter *.png |
        Where-Object { $_.Name -match 'Layer-(\d+)' } |
        Sort-Object { [int]($_.Name -replace '.*Layer-(\d+).*', '$1') }
    if ($layers.Count -eq 0) { continue }

    $imgs = @($layers | ForEach-Object { [System.Drawing.Image]::FromFile($_.FullName) })
    $cw = ($imgs | Measure-Object Width  -Maximum).Maximum
    $ch = ($imgs | Measure-Object Height -Maximum).Maximum

    $bmp = New-Object System.Drawing.Bitmap($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    foreach ($img in $imgs) {
        # layer exports are already registered to a shared canvas, so centre them
        $dx = [int](($cw - $img.Width) / 2); $dy = [int](($ch - $img.Height) / 2)
        $g.DrawImage($img, $dx, $dy, $img.Width, $img.Height)
        $img.Dispose()
    }
    $g.Dispose()
    $target = "$OUT\vehicle\$($dir.Name).png"
    $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Remove-Item $dir.FullName -Recurse -Force
    # the shadow sheet ships separately
    $shadowSrc = "D:\AI编程\成套资源_ZOMBIE TDS 2D GAME KIT\craftpix-871156-zombie-tds-2d-game-kit\zombie-tds-machines-and-tanks\PNG\$($dir.Name)\Shadow.png"
    if (Test-Path $shadowSrc) { Copy-Item $shadowSrc "$OUT\vehicle\$($dir.Name)_shadow.png" -Force }
    "flattened vehicle -> $($dir.Name).png  ($($layers.Count) layers, ${cw}x${ch})"
}

# ---- 3. tiles: keep one representative sheet per category, not the whole library
$tileDir = "$OUT\tile"
$keep = @(
    'ground_tiles', 'sand_tiles', 'stones_tiles', 'water_tiles',
    'asphalt_tiles', 'grass_tiles', 'cracks_ground', 'objects_house'
)
Get-ChildItem $tileDir -File | Where-Object {
    $n = $_.BaseName.ToLower(); -not ($keep | Where-Object { $n -like "*$_*" })
} | Remove-Item -Force
"tile files kept: $((Get-ChildItem $tileDir -File).Count)"

# ---- report
Write-Output ""
Get-ChildItem $OUT -Directory | ForEach-Object {
    $f = Get-ChildItem $_.FullName -Recurse -File
    "{0,-12} files={1,-5} {2,7:N0}KB" -f $_.Name, $f.Count, (($f | Measure-Object Length -Sum).Sum / 1KB)
}
$all = Get-ChildItem $OUT -Recurse -File
"TOTAL        files=$($all.Count)  $([Math]::Round(($all | Measure-Object Length -Sum).Sum/1MB,1))MB"
