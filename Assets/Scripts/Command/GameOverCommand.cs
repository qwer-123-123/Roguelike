using QFramework;

namespace Game
{
    /// <summary>游戏结束命令：玩家死亡时发出，把阶段切到 GameOver（冻结世界）。</summary>
    public class GameOverCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetModel<IGameStateModel>().Phase.Value = GamePhase.GameOver;
        }
    }
}
