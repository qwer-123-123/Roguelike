# 项目身份文件（Project Fingerprint）

> 任何审查、分析、执行或子智能体 spawn 任务前，必须读取本文件确认项目根目录一致。

## 项目信息

| 字段 | 值 |
|------|-----|
| 项目名称 | Roguelike（游戏名「菌核狂潮」） |
| 项目根目录 | `D:\unity\XiangMu\Roguelike` |
| 引擎 | Unity（中国版 Tuanjie 变体），编辑器 2022.3.62t11 |
| 项目类型 | 2D 俯视角 Roguelike + 割草 |
| 版本控制 | **Git**，2026-09-16 建仓，2026-09-17 推送至 GitHub 公开仓库。`main` → `origin/main`，基线提交 `b2f0e67`。**本地跟踪 198 文件，24MB 第三方素材故意不入仓**（见「版本控制」节） |

## 技术栈签名

| 项 | 值 |
|----|-----|
| 脚本语言 | C#，命名空间统一为 `Game` |
| 架构 | QFramework 四层（Controller / System / Model / Utility + Command / Event），`Architecture<GameArchitecture>` 单例统一注册 |
| QFramework 版本 | 单文件 `Assets/QFramework/QFramework.cs`，**v1.0**；ResKit / UIKit / PoolKit 等 Toolkits **未引入** |
| 渲染管线 | **Built-in**（`ProjectSettings/GraphicsSettings.asset` 的 `m_CustomRenderPipeline` 为空，无 SRP） |
| UI 方案 | uGUI，使用 legacy `UnityEngine.UI.Text`，**不是 TextMeshPro** |
| 包管理 | Unity Package Manager（`Packages/manifest.json`） |
| 第三方工具 | `com.besty.unity-skills`（本地 file 引用 `../Unity-Skills-main/SkillsForUnity`），REST 服务位于 `127.0.0.1:8090` |
| 程序集 | **无 asmdef**，全部脚本编译进默认程序集 |

## 工程结构

```
Assets/
├── Scenes/
│   ├── Main.unity                唯一的玩法场景
│   └── SampleScene.scene         工程创建时的模板残留，未使用
├── Prefabs/                      7 个
│   ├── Enemy.prefab / Projectile.prefab / ExpDrop.prefab    局内实体
│   ├── StartMenuPanel.prefab / GameOverPanel.prefab         UI（迭代 7）
│   └── HudPanel.prefab / UpgradePanel.prefab                UI（迭代 9）
├── Scripts/                      23 个 .cs
│   ├── GameArchitecture.cs       架构单例，统一注册 Utility / Model / System
│   ├── GameBootstrap.cs          RuntimeInitializeOnLoadMethod 启动引导
│   ├── Controller/  (16)         表现层 MonoBehaviour
│   ├── System/      (2)          UpgradeSystem / DifficultySystem
│   ├── Model/       (1)          GameStateModel
│   ├── Utility/     (1)          PoolUtility（对象池）
│   ├── Command/     (4)          无状态命令
│   └── Event/       (1)          OnLevelUpEvent
├── Configs/                      **空**（数值硬编码在 prefab 的 SerializeField 上）
├── Art/                          **空**（全用占位色块，无美术资源）
└── QFramework/QFramework.cs
```

## 关键约定

以下几条是本工程踩过坑后确立的，改动前请先确认理由：

- **UI 一律用预制体。** 四个界面（开始 / 死亡 / HUD / 升级三选一）都是预制体实例直接放在 `Main.unity` 中，脚本只通过 `[SerializeField]` 引用。**工程内不存在代码创建的 UI**。
- **`EventSystem` 是场景常驻对象。** UI 脚本不再自行创建它，也不要在预制体里放。
- **游戏阶段是唯一判据。** `IGameStateModel.Phase`（`Ready` / `Playing` / `GameOver`）决定玩法逻辑是否推进；各 `Update` 据此早退，不用 `Time.timeScale` 做全局冻结（它已被升级面板的暂停占用）。
- **UI 文本用 legacy `Text`，不要用 TMP。** `unity-skills` 的 `ui_create_text` 在 TMP 可用时会默认建 TMP，但 TMP 自带字体无中文字形（会渲染成豆腐块），且工程既有 UI 全是 legacy `Text`。
- **无 sprite 的 `Image`，`type` 与 `fillAmount` 不生效。** Unity 会直接走 Simple 的全矩形绘制。需要进度条请改用锚点宽度（参考 `HudController.RefreshExp`）。
- **`GameStateModel.Reset()` 用 `SetValueWithoutEvent` 静默归零。** 这是为防止重开时误弹升级面板。需要响应重置的模块必须主动刷新，不能依赖监听（参考 `HudController.OnPhaseChanged`）。

## 版本控制

2026-09-16 建立本地仓库，2026-09-17 推送到 GitHub 公开仓库。以下几条是**踩过坑后确立的**，改 `.gitignore` 前先确认理由：

| 项 | 值 |
|----|-----|
| 远程 | `origin` → `https://github.com/qwer-123-123/Roguelike.git`（**公开**） |
| 分支 | `main`，已跟踪 `origin/main` |
| 基线提交 | `b2f0e67` —— 745 文件 / 27MB |
| 本地跟踪文件 | **198 个（约 1MB）**，不含第三方素材，见下 |
| git 可执行文件 | `C:\Program Files\Git\cmd\git.exe`（winget 装的 Git for Windows 2.55.0，已在机器 PATH 上，但**已在运行的进程不会自动继承新 PATH**） |
| 提交身份 | 全局 `张彬 <1379413408@qq.com>`。**仓库级无覆盖** —— 若在此仓库设 `git config --local user.*` 会盖掉全局，注意别无意中设回去 |
| 凭证 | Git Credential Manager（`credential.helper=manager`）。本机**无 SSH 密钥、无 `gh` CLI**，推送走 HTTPS + 浏览器授权 |

### ⚠ 本地与远程不一致（最容易踩的一个坑）

以下两处是**第三方版权内容**，推送到公开仓库等于再分发，且素材许可（`Docs/Design/h5` 风险 R11 / P0）至今未读，故**已从全部历史提交中摘除**：

| 路径 | 规模 | 内容 |
|------|------|------|
| `Docs/Design/h5/assets/` | 370 文件 / 24.3MB | craftpix Zombie TDS kit 子集 |
| `.claude/skills/` | 178 文件 / 2.1MB | Unity-Skills-main 技能文档副本 |

**这两处仍在本地磁盘上，但不在 git 里**（`git status` 看不到，`git clean -fdx` 会删掉）。后果：

- 本地 `Docs/Design/h5/index.html` 照常能双击打开；**克隆到新机器则打不开**，需按 `.gitignore` 注释另行获取
- **它们没有版本保护** —— 因为不在 git 里，误删无法用 git 恢复

### 已被忽略且必须有理由的路径

- `Library/`（392MB）、`Temp/`、`obj/`、`.vs/`、`Logs/`、`UserSettings/` —— Unity 开一次编辑器就重建
- `*.sln` / `*.csproj` —— Unity 打开工程时重新生成
- `.com-unity-codely.json` —— Codely 桥接的**心跳文件**，端口/`seq`/`last_heartbeat` 每次会话都变，入了仓会一直制造提交噪音
- `Unity-Skills-main/` —— 下载来的第三方工具快照（108MB），非本项目源码
- `Docs/Design/h5/assets/`、`.claude/skills/` —— 见上

**⚠ `Unity-Skills-main/` 被忽略的副作用：** `Packages/manifest.json` 用 `file:../Unity-Skills-main/SkillsForUnity` 引用它。把本仓库克隆到别处时该相对路径会断，Unity 会报包找不到，需另行获取该工具。

**`*.meta` 必须提交。** 丢了 `.meta` Unity 会重建 GUID，预制体与场景的引用会全部断开。当前 `Assets/` 下资源与 `.meta` 已全部配对。

### 历史重写终止线

仓库已于 2026-09-17 **公开**。**此后不得再重写历史**（对已推送提交做 `filter-branch` / `rebase` / `--amend`）—— 需要改正就往前加新提交。此前已重写两次（改提交身份、摘除第三方素材），均发生在推送之前。

### 第三方代码合规

- `Assets/QFramework/QFramework.cs` —— liangxiegame 的 **MIT** 授权代码，版权头完整保留，允许再分发
- 本仓库**暂无本项目自身的 LICENSE 文件**；公开仓库无 LICENSE 即默认「保留所有权利」

## 校验规则

1. 确认本文件的「项目根目录」字段与当前工作目录一致。
2. 路径不匹配或文件不存在时，立即停止并提示用户确认工作目录。
3. 此项对审查类任务（team-review、parallel-analyze）为强制前置。

## 当前状态

- **MVP 已收口**：完整跑通「开始界面 → 开局 → 移动 → 杀怪 → 升级三选一 → 死亡 → 重开」一局。
- 迭代日志见 `Docs/IterationLogs/`（索引在同目录 `README.md`）；当前计划 `Docs/Plans/2026-09-10-计划-MVP收口与开始界面.md` 全部迭代已完成。
- 遗留项：`Configs/` 为空（无配置表）；三选一仍是「3 张固定牌打乱顺序」而非真随机池；无 asmdef；无远程仓库（仅本地）。
- **版本控制已于 2026-09-16 建立**，此前「无版本控制」的遗留项已消除。规范见下节。

## 维护提示

本文件是审查类任务与子智能体的**强制前置读取项**，内容必须与工程实际一致。工程结构、技术栈或关键约定发生变化时应同步更新。

> 历史教训：本文件曾长期停留在「项目处于初始化阶段，`Assets/` 目前仅含 `Scenes/`，尚未建立核心代码结构」——而当时工程已有 20+ 脚本与完整场景。过期内容会直接误导后续的所有审查与子智能体任务。
