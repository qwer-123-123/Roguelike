<!-- STATUS -->
Epic: 菌核狂潮 MVP
Feature: MVP 收口
Task: MVP 收口计划全部完结，待人工试玩确认
<!-- /STATUS -->

<!-- REVIEW-CONFIG -->
auto-team-review: false
review-trigger-keyword: /mywork-review
review-trigger-fallback: prompt-user
<!-- /REVIEW-CONFIG -->

<!-- ACTIVE-CONSTRAINTS -->
当前无生效的专项约束包
<!-- /ACTIVE-CONSTRAINTS -->

## 当前任务

迭代 0~5 已交付，收口计划 `Docs/Plans/2026-09-10-计划-MVP收口与开始界面.md` 已批准。

**迭代 6 已完成**：新增 `GamePhase { Ready, Playing, GameOver }` 状态机（取代原 `IsGameOver`），新增开始界面 `StartMenuPanelController`，5 处玩法 `Update` 加阶段门禁以冻结世界，修复用户反馈的「玩家死亡后游戏界面仍在活动」。编译/场景挂载/开局冻结均已运行态验证，**死亡路径待人工试玩确认**。

**迭代 7 已完成**：开始界面与死亡重开界面改为预制体（`Prefabs/StartMenuPanel.prefab`、`Prefabs/GameOverPanel.prefab`，场景中为已连接实例），死亡面板新增「返回开始界面」按钮（与「重开」并排）。开始界面渲染已验证与代码版像素级一致；**死亡面板的点击效果待人工确认**。

**迭代 8 已完成**：HUD 补上经验条与等级显示（计划内缺失的交付物）；`EnemySpawner` 的重复刷怪公式删除，难度收敛到 `DifficultySystem` 单一来源；升级保留溢出经验；升级改用 `OnLevelUpEvent` 通道（该事件此前是死代码），面板不再监听 `Level` 属性——迭代 5 修复 3 的 bug 类就此消除。

**迭代 9 已完成**：HUD 与升级三选一面板改为预制体（`Prefabs/HudPanel.prefab`、`Prefabs/UpgradePanel.prefab`）。至此全工程四个界面统一由预制体实例驱动，**代码建 UI 的写法彻底退场**（全 `Scripts/` grep 建 UI 调用零命中）。附带在场景补了一个常驻 `EventSystem`——它原本由升级面板的建 UI 代码在运行时创建，那段代码删除后不补会让所有按钮失效。

**收尾已完成**：重写严重过期的 `project-fingerprint.md`（此前仍写着「项目处于初始化阶段，`Assets/` 仅含 `Scenes/`」，而它却是审查类任务与子智能体的强制前置读取项），补入踩坑后确立的关键约定；复核调试日志仅剩 2 条有意保留的启动标记。

**`2026-09-10-计划-MVP收口与开始界面.md` 全部迭代（6 / 7 / 8 / 9 / 收尾）已完成，计划完结。** 当前无进行中的计划，下一步需另立计划。

## 进度检查

- [x] 阅读 `AGENTS.md`、启动工作流、使用说明、`MySkills/` 技能
- [x] 验证项目身份（Roguelike，`D:\unity\XiangMu\Roguelike`）
- [x] 初始化 `Docs/` 最小骨架
- [x] 启用 `.claude/` 自动增强（hooks + 路径规则）
- [x] 阅读策划案 + 确认关键决策（QFramework / Built-in 2D / ResKit / MVP 范围）
- [x] 产出 MVP 架构设计与迭代计划
- [x] 确认关键决策（战斗模型=远程投射物、摄像机=跟随、QFramework 核心从 Gitee 下载）
- [x] 迭代 0：工程与架构骨架（编译通过 + 架构初始化验证）
- [x] 迭代 1：玩家移动 + 摄像机（编译通过 + 场景搭建 + Play 零错误）
- [x] 迭代 2：敌人 + 刷怪波次 + 对象池（编译通过 + 敌人刷出追击 + Play 零错误）
- [x] 迭代 3：战斗闭环（自动攻击 + 击杀 + 掉落，Play 跑通链路零错误）
- [x] 迭代 4：局内升级三选一（经验升级 + 面板弹出 + 技能生效）
- [x] 迭代 5：死亡/结束循环 + 难度 + UI（玩家血量/死亡 + 动态难度 + HUD + 死亡面板重开）
- [x] 用户试玩反馈收集 → 确认死亡后世界未冻结（缺陷成立）
- [x] 全量代码审读，对照 MVP 清单核对进度
- [x] 迭代 6：游戏阶段 + 开始界面 + 死亡冻结（编译/挂载/开局冻结已运行态验证，死亡路径待试玩确认）
- [x] 迭代 7：面板预制体化 + 死亡面板「返回开始界面」按钮（开始界面渲染已运行态验证，死亡面板点击待试玩确认）
- [x] 修复：开始界面下持续发射 projectile（根因：`UpgradePanel` 误挂 `EnemyController`/`PlayerController` + `PlayerCombatController` 缺阶段门禁；已实测 `Projectile(Clone)=0`）
- [x] 迭代 8：HUD 经验条 + 难度收敛 + 升级溢出与事件化（UI 构建与事件接通已验证，显示效果待试玩确认）
- [x] 迭代 9：HUD 与升级面板预制体化 + 补回 EventSystem（结构已验证，交互待试玩确认）
- [x] 修复：经验条不动 + 死亡重开 HUD 陈旧（根因：无 sprite 时 `Image.fillAmount` 不生效；`Reset()` 静默归零致 HUD 收不到通知）
- [x] 收尾：重写 `project-fingerprint.md`、复核调试日志与文档一致性（**计划完结**）

## 关键决策

| 决策 | 状态 | 影响 |
|------|------|------|
| auto-team-review | false | M/L 任务完成后手动询问是否审查 |
| review-trigger-keyword | /mywork-review | 主动触发审查的关键词 |
| 架构框架 | QFramework 四层 | 代码骨架 |
| 资源管理 | ResKit（AssetBundle） | 资源加载 |
| 渲染管线 | Built-in 2D | 工程渲染 |
| 专项约束包 | 无 | 当前无已注册约束包 |

## 活跃文件

- `Docs/Current/session-active.md`（本文件）
- `Docs/Current/project-fingerprint.md`（收尾已重写）
- `Docs/Plans/2026-09-10-计划-MVP收口与开始界面.md`（**计划已完结**，迭代 6/7/8/9/收尾全部完成）
- `Docs/Plans/2026-09-07-计划-菌核狂潮MVP架构与迭代计划.md`（前序计划，迭代 0~5 已交付）
- `Docs/IterationLogs/`（本轮共 7 篇日志：迭代 6/7/8/9、修复 ×2、收尾；索引见同目录 `README.md`）
- `Prefabs/StartMenuPanel.prefab`、`Prefabs/GameOverPanel.prefab`、`Prefabs/HudPanel.prefab`、`Prefabs/UpgradePanel.prefab`（四个 UI 预制体）
- `Docs/README.md`

## 待确认

**计划已全部完成，无待决策项。** 以下均为需人工试玩确认的验证项——`unity-skills` 无输入模拟能力，无法点「开始游戏」进入 Playing 阶段，这些只能在编辑器内实跑一局：

- 死亡瞬间世界是否冻结；死亡面板两个按钮（重开 / 返回开始界面）是否可点、返回后场地是否清空
- 四个界面各处按钮交互是否正常（迭代 9 改动了 `EventSystem` 归属，**重点回归**）
- 经验条是否随拾取增长、升级后回零、重开后同步归零；等级文本是否正确
- 升级三选一面板是否由事件正常弹出；溢出经验是否保留；刷怪间隔是否随时间递减
- HUD 血量是否随受伤下降、重开后是否正确复位（此前 `UpgradePanel` 误挂第二个 `PlayerController` 可能影响过这两处）

## 待处理（已发现未处理）

- `Scenes/SampleScene.scene` 是工程模板残留，从未使用。**未删除**，需你确认
- 工程无版本控制（无 `.git`），全过程改动无回滚能力

## 已确认（2026-09-10）

- 收口计划已批准
- 2026-09-09 的 `EnemyController.cs` 改动 = 新增击杀日志 debug，可直接移除（已在迭代 6 一并删除）
- git 暂不提交
- `unity-skills` 连通性已实测正常（`127.0.0.1:8090`，`surfaceProfile=full`，`currentMode=bypass`，Console 0 错误），迭代 6 的编译/挂载/运行态验证均经该协议完成

## 下一步入口

**MVP 收口计划已完结，当前无进行中的计划。**

下一步二选一：
1. 用户完整试玩一局，确认上列验证项 → 如有问题则修复
2. 另立计划进入后续迭代（`Configs/` 配置表 + 真随机技能池 / 精英怪与 BOSS / 局外元进度 / 美术音效 / 性能优化）
