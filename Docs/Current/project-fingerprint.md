# 项目身份文件（Project Fingerprint）

> 任何审查、分析、执行或子智能体 spawn 任务前，必须读取本文件确认项目根目录一致。

## 项目信息

| 字段 | 值 |
|------|-----|
| 项目名称 | Roguelike（游戏名「菌核狂潮」） |
| 项目根目录 | `D:\unity\XiangMu\Roguelike` |
| 引擎 | Unity（中国版 Tuanjie 变体），编辑器 2022.3.62t11 |
| 项目类型 | 2D 俯视角 Roguelike + 割草 |
| 版本控制 | **无**（工程未初始化 git） |

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

## 校验规则

1. 确认本文件的「项目根目录」字段与当前工作目录一致。
2. 路径不匹配或文件不存在时，立即停止并提示用户确认工作目录。
3. 此项对审查类任务（team-review、parallel-analyze）为强制前置。

## 当前状态

- **MVP 已收口**：完整跑通「开始界面 → 开局 → 移动 → 杀怪 → 升级三选一 → 死亡 → 重开」一局。
- 迭代日志见 `Docs/IterationLogs/`（索引在同目录 `README.md`）；当前计划 `Docs/Plans/2026-09-10-计划-MVP收口与开始界面.md` 全部迭代已完成。
- 遗留项：无版本控制；`Configs/` 为空（无配置表）；三选一仍是「3 张固定牌打乱顺序」而非真随机池；无 asmdef。

## 维护提示

本文件是审查类任务与子智能体的**强制前置读取项**，内容必须与工程实际一致。工程结构、技术栈或关键约定发生变化时应同步更新。

> 历史教训：本文件曾长期停留在「项目处于初始化阶段，`Assets/` 目前仅含 `Scenes/`，尚未建立核心代码结构」——而当时工程已有 20+ 脚本与完整场景。过期内容会直接误导后续的所有审查与子智能体任务。
