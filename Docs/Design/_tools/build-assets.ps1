
# Build the self-contained asset bundle for the H5 design doc.
# Sources: craftpix-871156-zombie-tds-2d-game-kit.  Output: Docs/Design/h5/assets/

$ErrorActionPreference = "Stop"
$KIT  = "D:\AI编程\成套资源_ZOMBIE TDS 2D GAME KIT\craftpix-871156-zombie-tds-2d-game-kit"
$OUT  = "D:\unity\XiangMu\Roguelike\Docs\Design\h5\assets"
$STRIP = "D:\unity\XiangMu\Roguelike\Docs\Design\_tools\make-strip.ps1"

$MAIN = "$KIT\zombie-tds-main-characters\Characters\PNG_Bodyparts&Animations\PNG Animations"
$ZOMB = "$KIT\tds-zombie-character-sprite\Zombies\PNG Animations"
$MONS = "$KIT\tds-monster-character-sprites\Monsters\PNG Animations"

function CopyTree($src, $dst) {
    if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Force $dst | Out-Null }
    Copy-Item "$src\*" $dst -Recurse -Force
}

# ---------- 1. UI kit (wholesale: every element gets catalogued) ----------
CopyTree "$KIT\zombie-tds-game-user-interface\PNG"          "$OUT\ui"
CopyTree "$KIT\zombie-tds-game-user-interface\PNG_Window"   "$OUT\ui\window"

# ---------- 2. items / avatars / vehicles / tiles ----------
CopyTree "$KIT\zombie-tds-main-characters\Items\PNG"        "$OUT\item"
CopyTree "$KIT\zombie-tds-main-characters\Icons\PNG"        "$OUT\portrait"
CopyTree "$KIT\AVAs"                                        "$OUT\portrait\ava" -ErrorAction SilentlyContinue
CopyTree "$KIT\zombie-tds-machines-and-tanks\PNG"           "$OUT\vehicle"
CopyTree "$KIT\tds-monster-character-sprites\Icons\PNG"     "$OUT\portrait\enemy_monster"
CopyTree "$KIT\tds-zombie-character-sprite\Icons\PNG"       "$OUT\portrait\enemy_zombie"

# a representative scene-tileset sample (not the whole 3k-file library)
New-Item -ItemType Directory -Force "$OUT\tile" | Out-Null
Copy-Item "$KIT\zombie-tds-tilesets-soil-stones-plants-water-destroyed-cars\Tiles&Details\PNG\*_tiles\*" "$OUT\tile\" -Force -ErrorAction SilentlyContinue
Copy-Item "$KIT\zombie-tds-tilesets-buildings-and-furniture\Objects\PNG\*.png" "$OUT\tile\" -Force -ErrorAction SilentlyContinue

# ---------- 3. playable classes: 2 bodies x 5 weapons ----------
$weapons = @(
    @{ k = "knife";  idle = "Idle_knife";  walk = "Walk_knife";  atk = "Knife" },
    @{ k = "bat";    idle = "Idle_bat";    walk = "Walk_bat";    atk = "Bat" },
    @{ k = "gun";    idle = "Idle_gun";    walk = "Walk_gun";    atk = "Gun_Shot" },
    @{ k = "riffle"; idle = "Idle_riffle"; walk = "Walk_riffle"; atk = "Riffle" },
    @{ k = "flame";  idle = "Idle_firethrower"; walk = "Walk_firethrower"; atk = "FlameThrower" }
)
# the pack has two filename typos we have to honour
$walkOverride = @{ "Girl|flame" = "Walk_FireThrhrower" }

foreach ($side in @("Man", "Girl")) {
    $sd = $side.ToLower()
    foreach ($w in $weapons) {
        $key = "$side|$($w.k)"
        $walkName = $w.walk
        if ($walkOverride.ContainsKey($key)) { $walkName = $walkOverride[$key] }

        & $STRIP -SrcDir "$MAIN\$side\$($w.idle)" -OutFile "$OUT\unit\idle_${sd}_$($w.k).png" -TargetH 190
        & $STRIP -SrcDir "$MAIN\$side\$walkName"  -OutFile "$OUT\unit\walk_${sd}_$($w.k).png" -TargetH 190
        & $STRIP -SrcDir "$MAIN\$side\$($w.atk)"  -OutFile "$OUT\unit\atk_${sd}_$($w.k).png"  -TargetH 150
    }
    & $STRIP -SrcDir "$MAIN\$side\Death" -OutFile "$OUT\unit\death_$sd.png" -TargetH 150
}

# muzzle flashes / fire cones
& $STRIP -SrcDir "$MAIN\..\Spriter_Animations&Bodyparts\shots&fire" -OutFile "$OUT\vfx\shots_fire.png" -TargetH 130

# ---------- 4. enemies ----------
$enemies = @(
    @{ id="z1"; src="$ZOMB\1LVL\Zombie1_female"; name="1LVL\Zombie1_female" },
    @{ id="z2"; src="$ZOMB\1LVL\Zombie2_female"; name="1LVL\Zombie2_female" },
    @{ id="z3"; src="$ZOMB\1LVL\Zombie3_male";   name="1LVL\Zombie3_male" },
    @{ id="z4"; src="$ZOMB\1LVL\Zombie4_male";   name="1LVL\Zombie4_male" },
    @{ id="z5"; src="$ZOMB\2LVL\Army_zombie";    name="2LVL\Army_zombie" },
    @{ id="z6"; src="$ZOMB\2LVL\Cop_Zombie";     name="2LVL\Cop_Zombie" },
    @{ id="z7"; src="$MONS\3LVL\Zombie_big_hands"; name="3LVL\Zombie_big_hands" },
    @{ id="z8"; src="$MONS\3LVL\Zpmbie_big_head";  name="3LVL\Zpmbie_big_head" },
    @{ id="boss1"; src="$MONS\4LVL\Boss1"; name="4LVL\Boss1" },
    @{ id="boss2"; src="$MONS\4LVL\Boss2"; name="4LVL\Boss2" },
    @{ id="mega";  src="$MONS\5LVL";       name="5LVL (Megaboss)" }
)
foreach ($e in $enemies) {
    & $STRIP -SrcDir "$($e.src)\Walk" -OutFile "$OUT\enemy\walk_$($e.id).png" -TargetH 210
    $atkDirs = Get-ChildItem "$($e.src)" -Directory | Where-Object { $_.Name -like "Attack*" } | Sort-Object Name
    if ($atkDirs.Count -gt 0) { & $STRIP -SrcDir $atkDirs[0].FullName -OutFile "$OUT\enemy\atk_$($e.id).png" -TargetH 150 }
    if (Test-Path "$($e.src)\Death") { & $STRIP -SrcDir "$($e.src)\Death" -OutFile "$OUT\enemy\death_$($e.id).png" -TargetH 150 }
}

# ---------- 5. vfx ----------
& $STRIP -SrcDir "$KIT\zombie-tds-main-characters\Explosion\PNG" -OutFile "$OUT\vfx\explosion.png" -TargetH 170

# ---------- report ----------
Write-Output ""
Write-Output "=== OUTPUT TREE ==="
Get-ChildItem $OUT -Directory | ForEach-Object {
    $c = (Get-ChildItem $_.FullName -Recurse -File).Count
    $kb = [Math]::Round(((Get-ChildItem $_.FullName -Recurse -File | Measure-Object Length -Sum).Sum / 1KB), 0)
    "{0,-14} files={1,-5} {2}KB" -f $_.Name, $c, $kb
}
$tc = (Get-ChildItem $OUT -Recurse -File).Count
$tk = [Math]::Round(((Get-ChildItem $OUT -Recurse -File | Measure-Object Length -Sum).Sum / 1KB), 0)
"TOTAL          files=$tc  ${tk}KB"
