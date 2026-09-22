
# 生成 texture_set_settings_batch 的载荷：把 Assets/Art 下所有 PNG 设为 Sprite。
# 输出到 stdout 重定向的文件，供 curl --data-binary @file 使用。
#
# filterMode 保持默认 Bilinear —— 素材是手绘风不是像素风，Point 会让边缘锯齿。
# 真正必须改的是 textureType（默认 Default，不改成 Sprite 就没法当精灵用）。
param(
    [string]$Root = "D:\unity\XiangMu\Roguelike",
    [string]$Out  = "D:\unity\XiangMu\Roguelike\Docs\Design\_tools\out\import-payload.json"
)

$artRoot = Join-Path $Root "Assets\Art"
$files = Get-ChildItem $artRoot -Recurse -File -Filter *.png | Sort-Object FullName

$items = @()
foreach ($f in $files) {
    $rel = $f.FullName.Replace("$Root\", "").Replace("\", "/")
    $items += [ordered]@{
        assetPath   = $rel
        textureType = "Sprite"
        spriteMode  = "Single"
        mipmaps     = $false
        compression = "Uncompressed"
        maxSize     = 2048
    }
}

# items 是一个「JSON 数组的字符串」，所以整体再包一层
$inner = ($items | ForEach-Object { $_ | ConvertTo-Json -Compress }) -join ","
$payload = @{ items = "[$inner]" } | ConvertTo-Json -Compress

$dir = Split-Path $Out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
[System.IO.File]::WriteAllText($Out, $payload, (New-Object System.Text.UTF8Encoding($false)))

Write-Output "条目数: $($items.Count)"
Write-Output "载荷: $Out  ($([Math]::Round((Get-Item $Out).Length/1KB)) KB)"
