# Docs/Design — 策划文档

本目录存放策划案。**不是计划**（计划在 `Docs/Plans/`），也**不是迭代日志**（在 `Docs/IterationLogs/`）。
计划完成后的状态变更仍按既有规则写独立迭代日志。

## 目录内容

| 路径 | 说明 |
|------|------|
| `h5/index.html` | **菌核狂潮 · H5 策划案（美术落地版）** —— 单文件可直接双击打开，无需服务器 |
| `h5/catalog.js` | 素材词典数据（184 个 UI 零件 + 图标 / 道具 / 载具 / 地形 / 特效的逐件标注） |
| `h5/assets/` | 从素材包提取并优化后的自包含素材（24MB / 370 文件） |
| `_tools/` | 生成 `h5/assets/` 的 PowerShell 脚本，可重跑 |

## 策划案包含的 7 个部分

1. **总览与差距** —— 工程现状基线、素材包能力清单、能力缺口矩阵（策划需求 × 工程现状 × 素材是否已备）
2. **界面拼接** —— 用素材原始 PNG 拼出的 HUD / 升级三选一 / 兵种选择 / 死亡结算四个界面，每个附逐层拆解表（图层 → 素材文件 → 拼接方式 → 落到哪个预制体字段）；另有素材包自带 6 张整屏稿与界面流转图
3. **兵种系统** —— 角色（2）× 武器（5）= 10 兵种，三层拆解（BodySO / WeaponSO / UpgradeSO）、矩阵总表、动画卡片、状态机、资源标注表、接入现有代码的改造点
4. **敌人图鉴** —— 1~5 级共 11 种敌人，总表 + 动画卡片 + 波次接入表
5. **系统拆解** —— 9 个系统（阶段机 / 战斗 / 刷怪 / 升级 / 掉落 / 对象池 / 难度 / 元进度 / 场景）的现状、改动、数据流、素材依赖
6. **素材词典** —— 可搜索的素材全表
7. **风险与依赖** —— 12 条风险（4 条 P0 详述）+ 依赖顺序建议

## 素材来源

`D:\AI编程\成套资源_ZOMBIE TDS 2D GAME KIT\craftpix-871156-zombie-tds-2d-game-kit`
（craftpix「Zombie TDS 2D Game Kit」）

`h5/assets/` 里的文件是从中提取的**子集 + 派生**：

- 原始 PNG 按需降采样（UI 零件 700px、头像 128px、载具 640px 等），避免文档体积失控（130MB → 24MB）
- 载具的**分层 PSD 导出已合成为整车图**
- 角色 / 敌人的**逐帧 PNG 已合成为横向精灵条**，供网页用 CSS `steps()` 播放
- 未收录：`AVAs/`（含 45MB PSD）、`tds-desert-tileset-*`、建筑 Object 素材、以及各子目录的 `.ai` 源文件

> 素材许可（`License.txt`）**尚未逐份阅读**，已列为风险 R11（P0）。正式使用前必须确认商用 / 修改 / 分发三项授权。

## 重新生成素材

```powershell
# 脚本内含中文路径，PowerShell 5.1 需以 UTF-8 BOM 保存
$d = "D:\unity\XiangMu\Roguelike\Docs\Design\_tools"
foreach ($f in Get-ChildItem $d -Filter *.ps1) {
    $c = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($f.FullName, $c, (New-Object System.Text.UTF8Encoding($true)))
}

& "$d\build-assets.ps1"      # 提取 + 合成精灵条
& "$d\slim-assets.ps1"       # 砍掉大 PSD、合成载具分层图、精简瓦片
& "$d\optimize-assets.ps1"   # 分辨率上限
```

辅助脚本：`make-contactsheet.ps1`（把零件拼成带索引的接触表，用于逐张识别）、`make-strip.ps1`（把逐帧 PNG 合成精灵条，被 `build-assets.ps1` 调用）。

## 注意

- 策划案中的**全部数值都是首版基准，未经实测**（风险 R12）。进 Play 模式逐项调。
- 界面拼接图里的按钮、进度条是**用 `clip-path` 裁切 + 拉伸**模拟的。素材零件不是九宫格，正式接入需先做切片工程（风险 R3，P0）。
