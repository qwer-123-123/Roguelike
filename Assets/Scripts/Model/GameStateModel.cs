using QFramework;

namespace Game
{
    /// <summary>游戏阶段：开始界面 / 选兵种 / 局内进行 / 已结束。</summary>
    public enum GamePhase
    {
        /// <summary>开始界面，世界静止。</summary>
        Ready,
        /// <summary>兵种选择，世界静止（尚未开局）。</summary>
        ClassSelect,
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

        /// <summary>
        /// 当前选中的兵种索引（0 ~ 兵种数-1，见 <see cref="ClassCatalog"/> 的索引约定）。
        /// 与 <see cref="Phase"/> 同类：**重开不清它** —— 重开一局沿用同一兵种是预期行为。
        /// </summary>
        BindableProperty<int> SelectedClassIndex { get; }

        /// <summary>重开一局时把本局数据归零（不含 Phase 与 SelectedClassIndex，两者分别由命令与玩家选择驱动）。</summary>
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
        public BindableProperty<int> SelectedClassIndex { get; } = new(0);

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
            // SelectedClassIndex 也不在此重置：重开沿用同一兵种（H5 的「重开」行为一致）。
            //   ⚠ 别顺手补上 —— 补了会让玩家每次重开都被打回第一个兵种。
        }
    }
}
