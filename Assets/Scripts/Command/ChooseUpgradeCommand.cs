using QFramework;

namespace Game
{
    /// <summary>
    /// 选择升级（无状态命令）：把玩家选中的升级作用到 <see cref="IPlayerStatSystem"/>。
    ///
    /// 改造说明：原先这里用 <c>Object.FindObjectOfType&lt;PlayerCombatController&gt;()?.AddDamage(1)</c>
    /// 直接找具体实现类去改它的私有字段 —— 既与 <c>Docs/Architecture/项目约定.md</c> 第 6 条
    /// 「避免直接引用具体实现类」冲突，也是「重开必须逐项复位」的根源（数值散在各个
    /// Controller 的字段里）。现在统一改成写系统层的加成项。
    ///
    /// 数值口径也一并改了：旧值是加算在很小的基数上（伤害 1、移速 5），换成
    /// 「角色 × 武器」派生出的伤害（8~34）后，+1 点已无意义。现改为与 H5 升级卡
    /// 一致的乘法口径。迭代 7 会把这里换成 14 张卡的真随机池。
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
            var stats = this.GetSystem<IPlayerStatSystem>();

            switch (type)
            {
                case UpgradeType.AttackDamage:
                    stats.DamageMul += 0.20f;      // 伤害 +20%
                    break;
                case UpgradeType.AttackSpeed:
                    stats.RateMul *= 1.18f;        // 攻速 +18%
                    break;
                case UpgradeType.MoveSpeed:
                    stats.SpeedMul *= 1.15f;       // 移速 +15%
                    break;
            }
        }
    }
}
