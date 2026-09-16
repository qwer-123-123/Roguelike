using QFramework;

namespace Game
{
    /// <summary>游戏阶段：开始界面 / 局内进行 / 已结束。</summary>
    public enum GamePhase
    {
        /// <summary>开始界面，世界静止。</summary>
        Ready,
        /// <summary>局内进行中。</summary>
        Playing,
        /// <summary>玩家已死亡，世界冻结。</summary>
        GameOver
    }

    /// <summary>
    /// 游戏全局状态（数据层 Model）：经验、等级、波次、存活时间、游戏阶段等跨系统共享数据。
    /// </summary>
    public interface IGameStateModel : IModel
    {
        BindableProperty<int> Experience { get; }
        BindableProperty<int> Level { get; }
        BindableProperty<int> Wave { get; }
        BindableProperty<int> Kills { get; }
        BindableProperty<float> SurvivedTime { get; }
        /// <summary>当前游戏阶段。玩法逻辑据此决定是否推进（唯一事实来源）。</summary>
        BindableProperty<GamePhase> Phase { get; }
        /// <summary>重开一局时把本局数据归零（不含 Phase，Phase 由命令驱动）。</summary>
        void Reset();
    }

    public class GameStateModel : AbstractModel, IGameStateModel
    {
        public BindableProperty<int> Experience { get; } = new(0);
        public BindableProperty<int> Level { get; } = new(1);
        public BindableProperty<int> Wave { get; } = new(1);
        public BindableProperty<int> Kills { get; } = new(0);
        public BindableProperty<float> SurvivedTime { get; } = new(0f);
        public BindableProperty<GamePhase> Phase { get; } = new(GamePhase.Ready);

        protected override void OnInit()
        {
        }

        public void Reset()
        {
            // 用 SetValueWithoutEvent：重开时置回初始值，不触发各监听（否则 Level 从高设回 1 会误弹升级面板）
            Experience.SetValueWithoutEvent(0);
            Level.SetValueWithoutEvent(1);
            Wave.SetValueWithoutEvent(1);
            Kills.SetValueWithoutEvent(0);
            SurvivedTime.SetValueWithoutEvent(0f);
            // Phase 不在此重置：由 StartNewGameCommand / GameOverCommand 显式驱动
        }
    }
}
