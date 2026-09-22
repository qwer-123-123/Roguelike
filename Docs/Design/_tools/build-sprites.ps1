
# 只重新生成「精灵条」（player / enemy / vfx）。
# 与 build-assets.ps1 的区别：那个是全量流程（还会复制 UI 素材），
# 这个只做条，用来在改了缩放规则后快速重跑。
#
# ★ 核心规则：**同一个角色的所有动画必须用同一个缩放比**。
#   早期版本按「帧高统一到固定值」归一化，而攻击动画的原始帧更高
#   （枪口火焰往上喷、刀挥得更开），于是攻击条被压到约一半大小，
#   游戏里表现为「人物攻击时缩小」。这里改为按基准动画算出缩放比后，
#   该角色的 idle/walk/atk/death 全部沿用同一个值。

$ErrorActionPreference = "Stop"
$KIT  = "D:\AI编程\成套资源_ZOMBIE TDS 2D GAME KIT\craftpix-871156-zombie-tds-2d-game-kit"
$OUT  = "D:\unity\XiangMu\Roguelike\Docs\Design\h5\assets"
$STRIP = Join-Path $PSScriptRoot "make-strip.ps1"

$MAIN = "$KIT\zombie-tds-main-characters\Characters\PNG_Bodyparts&Animations\PNG Animations"
$ZOMB = "$KIT\tds-zombie-character-sprite\Zombies\PNG Animations"
$MONS = "$KIT\tds-monster-character-sprites\Monsters\PNG Animations"

$PLAYER_REF_H = 190      # 玩家 idle 条的帧高基准
$ENEMY_REF_H  = 210      # 敌人 walk 条的帧高基准

Add-Type -AssemblyName System.Drawing

function MaxFrameH([string]$dir) {
    $f = Get-ChildItem $dir -File -Filter *.png -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $f) { return 0 }
    $i = [System.Drawing.Image]::FromFile($f.FullName)
    $h = $i.Height; $i.Dispose()
    return $h
}

$weapons = @(
    @{ k="knife";  idle="Idle_knife";  walk="Walk_knife";  atk="Knife" },
    @{ k="bat";    idle="Idle_bat";    walk="Walk_bat";    atk="Bat" },
    @{ k="gun";    idle="Idle_gun";    walk="Walk_gun";    atk="Gun_Shot" },
    @{ k="riffle"; idle="Idle_riffle"; walk="Walk_riffle"; atk="Riffle" },
    @{ k="flame";  idle="Idle_firethrower"; walk="Walk_firethrower"; atk="FlameThrower" }
)
$walkOverride = @{ "Girl|flame" = "Walk_FireThrhrower" }   # 素材包原文拼写如此

# ---------- 玩家：每个 body 一个缩放比 ----------
foreach ($side in @("Man", "Girl")) {
    $sd = $side.ToLower()

    # 基准 = 该角色所有 idle 动画里最高的那一帧
    $refH = 0
    foreach ($w in $weapons) {
        $h = MaxFrameH "$MAIN\$side\$($w.idle)"
        if ($h -gt $refH) { $refH = $h }
    }
    if ($refH -le 0) { Write-Output "!! 找不到 $side 的 idle 帧，跳过"; continue }
    $scale = $PLAYER_REF_H / $refH
    Write-Output "== $side  基准帧高 $refH -> scale $([Math]::Round($scale,4)) =="

    foreach ($w in $weapons) {
        $key = "$side|$($w.k)"
        $walkName = if ($walkOverride.ContainsKey($key)) { $walkOverride[$key] } else { $w.walk }
        & $STRIP -SrcDir "$MAIN\$side\$($w.idle)" -OutFile "$OUT\unit\idle_${sd}_$($w.k).png" -Scale $scale
        & $STRIP -SrcDir "$MAIN\$side\$walkName"  -OutFile "$OUT\unit\walk_${sd}_$($w.k).png" -Scale $scale
        & $STRIP -SrcDir "$MAIN\$side\$($w.atk)"  -OutFile "$OUT\unit\atk_${sd}_$($w.k).png"  -Scale $scale
    }
    & $STRIP -SrcDir "$MAIN\$side\Death" -OutFile "$OUT\unit\death_$sd.png" -Scale $scale
}

# ---------- 敌人：每种一个缩放比（BOSS 本来就该更大）----------
$enemies = @(
    @{ id="z1"; src="$ZOMB\1LVL\Zombie1_female" }, @{ id="z2"; src="$ZOMB\1LVL\Zombie2_female" },
    @{ id="z3"; src="$ZOMB\1LVL\Zombie3_male" },   @{ id="z4"; src="$ZOMB\1LVL\Zombie4_male" },
    @{ id="z5"; src="$ZOMB\2LVL\Army_zombie" },    @{ id="z6"; src="$ZOMB\2LVL\Cop_Zombie" },
    @{ id="z7"; src="$MONS\3LVL\Zombie_big_hands" }, @{ id="z8"; src="$MONS\3LVL\Zpmbie_big_head" },
    @{ id="boss1"; src="$MONS\4LVL\Boss1" }, @{ id="boss2"; src="$MONS\4LVL\Boss2" },
    @{ id="mega";  src="$MONS\5LVL" }
)
foreach ($e in $enemies) {
    $refH = MaxFrameH "$($e.src)\Walk"
    if ($refH -le 0) { Write-Output "!! 找不到 $($e.id) 的 Walk，跳过"; continue }
    $scale = $ENEMY_REF_H / $refH
    & $STRIP -SrcDir "$($e.src)\Walk" -OutFile "$OUT\enemy\walk_$($e.id).png" -Scale $scale
    $atkDirs = Get-ChildItem "$($e.src)" -Directory | Where-Object { $_.Name -like "Attack*" } | Sort-Object Name
    if ($atkDirs.Count -gt 0) { & $STRIP -SrcDir $atkDirs[0].FullName -OutFile "$OUT\enemy\atk_$($e.id).png" -Scale $scale }
    if (Test-Path "$($e.src)\Death") { & $STRIP -SrcDir "$($e.src)\Death" -OutFile "$OUT\enemy\death_$($e.id).png" -Scale $scale }
}

# ---------- 特效：单动画，沿用帧高归一化 ----------
& $STRIP -SrcDir "$MAIN\..\Spriter_Animations&Bodyparts\shots&fire" -OutFile "$OUT\vfx\shots_fire.png" -TargetH 130
& $STRIP -SrcDir "$KIT\zombie-tds-main-characters\Explosion\PNG" -OutFile "$OUT\vfx\explosion.png" -TargetH 170

Write-Output ""
Write-Output "=== 条尺寸核对（同一角色的 idle/walk/atk 帧高应接近）==="
foreach ($side in @("man","girl")) {
    foreach ($w in @("gun","knife","flame")) {
        foreach ($k in @("idle","walk","atk")) {
            $p = "$OUT\unit\${k}_${side}_$w.png"
            if (Test-Path $p) {
                $i = [System.Drawing.Image]::FromFile($p); $h = $i.Height; $i.Dispose()
                Write-Output ("  {0,-18} {1}px" -f "${k}_${side}_$w", $h)
            }
        }
    }
}
