using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 选择升级（无状态命令）：把玩家选中的升级应用到对应表现层组件。
    /// </summary>
    public class ChooseUpgradeCommand : AbstractCommand
    {
        private readonly UpgradeType type;

        public ChooseUpgradeCommand(UpgradeType type)
        {
            this.type = type;
        }

        protected override void OnExecute()
        {
            switch (type)
            {
                case UpgradeType.AttackDamage:
                    Object.FindObjectOfType<PlayerCombatController>()?.AddDamage(1);
                    break;
                case UpgradeType.AttackSpeed:
                    Object.FindObjectOfType<PlayerCombatController>()?.IncreaseFireRate(0.2f);
                    break;
                case UpgradeType.MoveSpeed:
                    Object.FindObjectOfType<PlayerController>()?.AddMoveSpeed(1f);
                    break;
            }
        }
    }
}
