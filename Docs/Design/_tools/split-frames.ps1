
# 把 H5 用的横向精灵条拆成逐帧 PNG，导入 Unity 的 Assets/Art/。
#
# 为什么拆帧而不是切片：unity-skills 没有切图操作（sprite_mode=Multiple 能设，
# 但定义子精灵矩形没有对应技能），而手写 .meta 在本引擎上必然失败 ——
# 该引擎的 .meta guid 被混淆过，与 YAML 里引用的 hex guid 无推导关系。
#
# 输出同时产出一份 Manifest.csv：它是"离线脚本"与"REST 批量导入"之间唯一的桥，
# 也是导入后可核对、可复跑的依据（526 个资产的参数不能散在人脑里）。
#
# 锚点换算（严格等价于 H5 play/js/assets.js 的 drawSprite，是"攻击时人物不缩小"
# 那条修复的 Unity 版）：
#     scale   = worldHeight * 100 / refH      # 落在 Visual.localScale
#     offsetY = (frameH / 100 * scale - worldHeight) / 2
#     worldHeight = 该角色 idle/walk 基准条对应的世界高度
#     refH        = 该角色基准条的帧高（像素）
# 核心断言：同一角色 idle ↔ atk 之间脚底不移动、身体不变小。

param(
    [string]$Src = "D:\unity\XiangMu\Roguelike\Docs\Design\h5\assets",
    [string]$Dst = "D:\unity\XiangMu\Roguelike\Assets\Art",
    [int]$PPU = 100
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

# ---------- 帧数表（与 H5 play/js/assets.js 的 SPRITE_DEFS 一致） ----------
$UNIT_WEAPONS = @{
    knife  = @{ idle=8; walk=6; atk=8  }
    bat    = @{ idle=8; walk=6; atk=12 }
    gun    = @{ idle=8; walk=6; atk=5  }
    riffle = @{ idle=8; walk=6; atk=9  }
    flame  = @{ idle=8; walk=6; atk=9  }
}
$ENEMY_FRAMES = @{
    z1=@{walk=9;atk=9;death=6};  z2=@{walk=9;atk=9;death=6}
    z3=@{walk=9;atk=9;death=6};  z4=@{walk=9;atk=9;death=6}
    z5=@{walk=9;atk=9;death=6};  z6=@{walk=9;atk=9;death=6}
    z7=@{walk=9;atk=9;death=6};  z8=@{walk=9;atk=9;death=6}
    boss1=@{walk=8;atk=14;death=10}; boss2=@{walk=8;atk=8;death=10}
    mega =@{walk=8;atk=16;death=14}
}
# 世界高度（H5 data.js 的 spriteH / 玩家 1.75 与 1.63）
$UNIT_WORLD_H = @{ man = 1.75; girl = 1.63 }
$UNIT_REF_STRIP = @{ man = "idle_man_gun"; girl = "idle_girl_gun" }
$ENEMY_WORLD_H = @{
    z1=1.35; z2=1.35; z3=1.40; z4=1.40; z5=1.65; z6=1.70
    z7=2.10; z8=2.15; boss1=3.40; boss2=3.50; mega=5.00
}
$VFX_FRAMES = @{ explosion = 6; shots_fire = 13 }
$VFX_WORLD_H = @{ explosion = 2.2; shots_fire = 1.6 }

$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add("assetPath,group,charId,state,frame,frameW,frameH,refH,worldHeight,scale,offsetY")

function StripInfo([string]$path, [int]$frames) {
    $img = [System.Drawing.Image]::FromFile($path)
    $w = $img.Width; $h = $img.Height; $img.Dispose()
    if ($w % $frames -ne 0) { throw "帧数不整除: $path 宽 $w / $frames 帧" }
    return @{ fw = [int]($w / $frames); fh = $h }
}

function SplitStrip([string]$srcFile, [int]$frames, [string]$outDir, [string]$assetGroup,
                    [string]$charId, [string]$state, [int]$refH, [double]$worldH) {
    $info = StripInfo $srcFile $frames
    $fw = [int]$info.fw
    $fh = [int]$info.fh
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }
    $img = [System.Drawing.Image]::FromFile($srcFile)
    $scale = $worldH * $PPU / $refH
    $offsetY = [Math]::Round(($fh / $PPU * $scale - $worldH) / 2, 4)

    # 用显式变量构造，不用 New-Object Type(...) 的行内形式 ——
    # 参数里带 :: 静态成员会让 PowerShell 的参数解析出错（op_Multiply MethodNotFound）
    $fmt = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    $unitPx = [System.Drawing.GraphicsUnit]::Pixel

    for ($i = 0; $i -lt $frames; $i++) {
        $bmp = New-Object System.Drawing.Bitmap($fw, $fh, $fmt)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $dstRect = New-Object System.Drawing.Rectangle(0, 0, $fw, $fh)
        $srcRect = New-Object System.Drawing.Rectangle(($i * $fw), 0, $fw, $fh)
        $g.DrawImage($img, $dstRect, $srcRect, $unitPx)
        $g.Dispose()
        $out = Join-Path $outDir ("{0:D3}.png" -f $i)
        $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $rel = $out.Replace("D:\unity\XiangMu\Roguelike\", "").Replace("\", "/")
        $manifest.Add("$rel,$assetGroup,$charId,$state,$i,$fw,$fh,$refH,$worldH,$([Math]::Round($scale,4)),$offsetY")
    }
    $img.Dispose()
    Write-Output ("  {0,-26} {1} 帧  {2}x{3}  scale={4:N3}  offsetY={5}" -f "$charId/$state", $frames, $fw, $fh, $scale, $offsetY)
}

# ================= 玩家 =================
Write-Output "== 玩家 =="
foreach ($body in @("man","girl")) {
    $refStrip = Join-Path $Src "unit/$($UNIT_REF_STRIP[$body]).png"
    $refH = (StripInfo $refStrip 8).fh
    $worldH = $UNIT_WORLD_H[$body]
    Write-Output "  $body 基准条帧高 ${refH}px -> 世界高 ${worldH}"
    foreach ($w in $UNIT_WEAPONS.Keys) {
        $f = $UNIT_WEAPONS[$w]
        SplitStrip (Join-Path $Src "unit/idle_${body}_$w.png") $f.idle "$Dst/Units/$body/${w}_idle" "units" $body "idle" $refH $worldH
        SplitStrip (Join-Path $Src "unit/walk_${body}_$w.png") $f.walk "$Dst/Units/$body/${w}_walk" "units" $body "walk" $refH $worldH
        SplitStrip (Join-Path $Src "unit/atk_${body}_$w.png")  $f.atk  "$Dst/Units/$body/${w}_atk"  "units" $body "atk"  $refH $worldH
    }
    SplitStrip (Join-Path $Src "unit/death_$body.png") 6 "$Dst/Units/$body/death" "units" $body "death" $refH $worldH
}

# ================= 敌人 =================
Write-Output "== 敌人 =="
foreach ($id in $ENEMY_FRAMES.Keys) {
    $f = $ENEMY_FRAMES[$id]
    $refH = (StripInfo (Join-Path $Src "enemy/walk_$id.png") $f.walk).fh
    $worldH = $ENEMY_WORLD_H[$id]
    SplitStrip (Join-Path $Src "enemy/walk_$id.png")  $f.walk  "$Dst/Enemies/$id/walk"  "enemies" $id "walk"  $refH $worldH
    SplitStrip (Join-Path $Src "enemy/atk_$id.png")   $f.atk   "$Dst/Enemies/$id/atk"   "enemies" $id "atk"   $refH $worldH
    SplitStrip (Join-Path $Src "enemy/death_$id.png") $f.death "$Dst/Enemies/$id/death" "enemies" $id "death" $refH $worldH
}

# ================= 特效 =================
Write-Output "== 特效 =="
foreach ($v in $VFX_FRAMES.Keys) {
    $srcFile = Join-Path $Src "vfx/$v.png"
    $refH = (StripInfo $srcFile $VFX_FRAMES[$v]).fh
    SplitStrip $srcFile $VFX_FRAMES[$v] "$Dst/Vfx/$v" "vfx" $v "play" $refH $VFX_WORLD_H[$v]
}

# ================= 单图资源（直接复制） =================
Write-Output "== UI / 道具 / 头像 / 地形 =="
$copyMap = @(
    @{ from = "ui";                  to = "UI/Elements"; keep = "element_*.png" },
    @{ from = "ui";                  to = "UI/Backdrops"; keep = "background*.png" },
    @{ from = "ui/window";           to = "UI/Windows";  keep = "*.png" },
    @{ from = "ui/Icons";            to = "UI/Icons";    keep = "*.png" },
    @{ from = "item";                to = "Items";       keep = "*.png" },
    @{ from = "portrait";            to = "Portraits";   keep = "*.png" },
    @{ from = "tile";                to = "Tiles";       keep = "*.png" }
)
foreach ($m in $copyMap) {
    $fromDir = Join-Path $Src $m.from
    $toDir = Join-Path $Dst $m.to
    if (-not (Test-Path $toDir)) { New-Item -ItemType Directory -Force $toDir | Out-Null }
    $files = Get-ChildItem $fromDir -File -Filter $m.keep
    Copy-Item "$fromDir\*" $toDir -Force
    Write-Output ("  {0,-18} {1} 个" -f $m.to, $files.Count)
}

# ================= 输出清单 =================
$manifestPath = Join-Path $Dst "Manifest.csv"
$manifest | Set-Content $manifestPath -Encoding UTF8
Write-Output ""
Write-Output "== 完成 =="
Write-Output "  逐帧 PNG：$($manifest.Count - 1) 张"
Write-Output "  清单：$manifestPath"
Write-Output "  提示：清单里的 scale / offsetY 要填进 Configs 的 CharacterSpriteSet"
