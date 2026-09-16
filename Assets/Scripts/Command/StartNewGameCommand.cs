using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 重置一局（无状态命令）：软重启，不重载场景。
    /// 在内存内彻底重置：数据模型归零 + 回收所有运行时生成对象 + 玩家/刷怪器复位。
    /// 规避了 SceneManager.LoadScene 不重建 QFramework 静态单例、导致状态残留的问题。
    /// </summary>
    public class StartNewGameCommand : AbstractCommand
    {
        private readonly GamePhase targetPhase;

        /// <summary>重开一局并直接进入局内（死亡面板「重开」、开始界面「开始游戏」用）。</summary>
        public StartNewGameCommand() : this(GamePhase.Playing)
        {
        }

        /// <summary>
        /// 重置一局后切到指定阶段。传 <see cref="GamePhase.Ready"/> 即「返回开始界面」：
        /// 场地已清空、玩家归位，但停在开始界面等玩家点开始。
        /// </summary>
        public StartNewGameCommand(GamePhase targetPhase)
        {
            this.targetPhase = targetPhase;
        }

        protected override void OnExecute()
        {
            // 1. 数据模型归零
            this.GetModel<IGameStateModel>().Reset();

            // 2. 回收所有运行时生成的对象（敌人/投射物/掉落物回对象池）—— 返回开始界面时清空场地
            this.GetUtility<IPoolUtility>().ReleaseAll();

            // 3. 清空静态活跃敌人表（残留引用）
            EnemyController.ClearActiveEnemies();

            // 4. 复位场景内的管理器
            Object.FindObjectOfType<PlayerController>()?.ResetForNewGame();
            Object.FindObjectOfType<PlayerCombatController>()?.ResetForNewGame();
            Object.FindObjectOfType<EnemySpawner>()?.ResetForNewGame();

            // 5. 隐藏升级面板并恢复时间缩放（防止重开时面板仍开着/游戏被暂停）
            Object.FindObjectOfType<UpgradePanelController>()?.HideForNewGame();

            // 6. 切到目标阶段（开始界面 / 死亡面板据此显隐，玩法逻辑据 Playing 恢复推进）
            this.GetModel<IGameStateModel>().Phase.Value = targetPhase;
        }
    }
}
