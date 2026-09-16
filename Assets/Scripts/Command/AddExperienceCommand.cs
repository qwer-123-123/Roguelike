using QFramework;

namespace Game
{
    /// <summary>
    /// 增加经验（无状态命令）：表现层拾取经验掉落时，通过命令改 Model 状态。
    /// </summary>
    public class AddExperienceCommand : AbstractCommand
    {
        private readonly int amount;

        public AddExperienceCommand(int amount)
        {
            this.amount = amount;
        }

        protected override void OnExecute()
        {
            this.GetModel<IGameStateModel>().Experience.Value += amount;
        }
    }
}
