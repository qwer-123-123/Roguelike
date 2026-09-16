using System.Linq;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>升级选项类型。</summary>
    public enum UpgradeType
    {
        AttackDamage,   // 攻击力 +1
        AttackSpeed,    // 攻速 +20%
        MoveSpeed       // 移速 +1
    }

    /// <summary>一个升级选项（三选一里的一项）。</summary>
    public struct UpgradeChoice
    {
        public UpgradeType Type;
        public string Name;
        public string Description;
    }

    /// <summary>
    /// 升级系统（系统层 ISystem）：监听经验变化，达标即升级并发出三选一事件。
    /// 业务逻辑纯 C#，不依赖 Unity 对象。
    /// </summary>
    public interface IUpgradeSystem : ISystem
    {
        /// <summary>升到下一级所需的经验阈值。</summary>
        int GetThreshold(int level);
    }

    public class UpgradeSystem : AbstractSystem, IUpgradeSystem
    {
        private static readonly UpgradeChoice[] AllChoices =
        {
            new UpgradeChoice { Type = UpgradeType.AttackDamage, Name = "攻击力提升", Description = "投射物伤害 +1" },
            new UpgradeChoice { Type = UpgradeType.AttackSpeed, Name = "攻速提升", Description = "攻击间隔 -20%" },
            new UpgradeChoice { Type = UpgradeType.MoveSpeed, Name = "移速提升", Description = "移动速度 +1" },
        };

        protected override void OnInit()
        {
            this.GetModel<IGameStateModel>().Experience.Register(OnExperienceChanged);
        }

        private void OnExperienceChanged(int exp)
        {
            var model = this.GetModel<IGameStateModel>();
            int threshold = GetThreshold(model.Level.Value);
            if (exp < threshold) return;

            // 保留溢出经验：只扣掉本次消耗的阈值，不清零。
            // 用 SetValueWithoutEvent 静默写回，避免再次触发本回调造成重入。
            model.Experience.SetValueWithoutEvent(exp - threshold);

            model.Level.Value += 1;

            // 发事件驱动 UI，而不是让面板监听 Level 属性——
            // 后者会在 Reset() 把 Level 置回时误弹面板（迭代 5 修复 3 的根因）。
            this.SendEvent(new OnLevelUpEvent { Choices = BuildChoices() });
        }

        private static UpgradeChoice[] BuildChoices()
        {
            return AllChoices.OrderBy(_ => Random.value).ToArray();
        }

        public int GetThreshold(int level) => 5 + (level - 1) * 3;
    }
}
