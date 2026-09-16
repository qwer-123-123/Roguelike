# 2026-09-10-修复-开始界面下持续发射与 UpgradePanel 误挂组件

| 字段 | 内容 |
|------|------|
| 日期 | 2026-09-10 |
| 迭代目标 | 修复用户反馈「运行后不点开始会一直生成 projectile」 |
| 关联计划 | `Docs/Plans/2026-09-10-计划-MVP收口与开始界面.md` |

## 问题（用户反馈）

在开始界面不点「开始游戏」，Play 模式下持续生成 `Projectile(Clone)`。

## 复现

`editor_play` 后不点击开始，10 秒观察：场景中出现 8 个活跃 `Projectile(Clone)`，继续观察增长到 36 个。同时场景中**没有任何 `Enemy(Clone)`**，Console 无任何报错。

## 根因（两个叠加）

### 根因 A：`UpgradePanel` 场景对象上误挂了 `EnemyController` 与 `PlayerController`

`UpgradePanel` 本该只有 `UpgradePanelController`，实际组件为：Transform + `UpgradePanelController` + **`EnemyController`** + **`PlayerController`**（场景 YAML 中该 GameObject 挂着 3 个 MonoBehaviour 可证）。

连锁反应：

1. `EnemyController.OnEnable()` 把自己注册进静态表 `ActiveEnemies` → **升级面板被当成一个存活的「敌人」**。
2. `PlayerCombatController.Update()` 调 `EnemyController.FindNearest(playerPos)` → 命中这个假敌人 → `target != null` → 每隔 `fireInterval`（0.8s）发射一发。
3. 发射出的投射物因 `ProjectileController` 有阶段门禁，在非 `Playing` 阶段原地静止、既不移动也不命中、更不回收 → **持续累积**。观察到的 36 个与之吻合（约 1 发/0.8 秒）。

### 根因 B：`PlayerCombatController.Update()` 缺阶段门禁（迭代 6 的遗漏）

迭代 6 给 5 处玩法 `Update` 加了阶段门禁（玩家移动 / 敌人 / 刷怪 / 投射物 / 经验掉落），**独独漏了 `PlayerCombatController`**。所以即便在开始界面，发射逻辑照跑。

A 提供了「有目标」这个前提，B 让「不该跑的逻辑跑了」——两个缺陷叠加才暴露。缺任一，本问题都不会出现。

## 修复

1. 从 `UpgradePanel` 上移除误挂的 `EnemyController` 与 `PlayerController`（场景数据修正，非代码改动）。
2. `PlayerCombatController.Update()` 补上阶段门禁，与其余玩法逻辑一致。

## 影响范围

- 场景：`Scenes/Main.unity` 的 `UpgradePanel` 组件表
- 代码：`Scripts/Controller/PlayerCombatController.cs`

## 验证结果

- ✅ **编译通过**：0 错误 0 警告。
- ✅ **宿主复核**：`EnemyController` 在场景中命中数 **0**；`PlayerController` 仅命中 `Player` 一个。其余控制器（`EnemySpawner` / `UpgradePanelController` / `HudController` / `GameFlowController` / `CameraFollow` / `StartMenuPanelController` / `GameOverPanelController`）均为一对一，无残留。
- ✅ **实测修复**：`editor_play` 后不点开始，14 秒观察 → `Projectile(Clone) = 0`、`Enemy(Clone) = 0`（修复前为 36）。
- ✅ **运行零错误**。

## 更正：迭代 6 的一处验证结论不成立

迭代 6 日志写「✅ Ready 阶段世界冻结（运行态）：同一轮 Play 内 6 秒未刷出任何敌人」。该结论**当时就不成立**——开始界面底板是不透明度 0.92 的全屏 Image，敌人与投射物都被它挡住了；我以「截图里看不到」当作「没有生成」的证据，这是无效证据。实际当时投射物已在持续生成。

本次改用「查层级对象数量」这种可量化的方式验证，才是有效证据。**这条更正同时适用于迭代 6 与迭代 7 中所有以截图观感为依据的结论。**

## 同一根因的连带影响（已随之消除，建议试玩确认）

`UpgradePanel` 曾同时存在第二个 `PlayerController`，以下依赖 `FindObjectOfType<PlayerController>()` 的代码可能取到错误实例：

- `HudController.Start()` 取 `playerInput` → HUD 血量可能一直显示错误的值
- `StartNewGameCommand` 的 `PlayerController.ResetForNewGame()` → 重开时真正的玩家可能未被复位

两个组件移除后该类风险消除，但需试玩确认「重开后血量/位置确实复位」「HUD 血量随受伤下降」。

## 下一步状态

修复完成。迭代 6/7 的死亡路径与死亡面板按钮仍需人工试玩确认，之后进入迭代 8。
