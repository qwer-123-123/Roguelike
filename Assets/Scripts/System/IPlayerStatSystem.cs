using QFramework;

namespace Game
{
    /// <summary>
    /// 玩家局内数值的**唯一权威**（业务逻辑层，纯 C#，不引用任何 Unity 对象）。
    ///
    /// 为什么要有这一层：原先数值散在三个 Controller 的字段里
    /// （<c>PlayerController.moveSpeed/maxHealth</c>、<c>PlayerCombatController.damage/fireInterval</c>），
    /// 升级走「推」接口去改它们，于是**重开必须逐项复位**，漏一项就残留上一局的数值。
    /// 收敛到这里之后：
    ///   - 基准值由 (角色 × 武器) 派生，只有一个来源
    ///   - 重开只需 <see cref="ResetStats"/> 一次
    ///   - Controller 每帧「拉」最终值，不再持有可变数值
    ///
    /// 兵种索引存放在 <see cref="IGameStateModel.SelectedClassIndex"/>，本系统只负责派生。
    /// 这样重开时索引不受 <c>Model.Reset()</c> 影响（重开沿用同一兵种）。
    /// </summary>
    public interface IPlayerStatSystem : ISystem
    {
        /// <summary>是否已绑定配置表。未绑定时所有派生值为安全的兜底值，消费方需判此。</summary>
        bool IsBound { get; }

        /// <summary>注入配置表。由 <see cref="GameConfigHolder"/> 在 Awake 时调用一次。</summary>
        void Bind(ClassCatalog catalog, GameConfig config);

        /// <summary>重开一局：清掉全部加成与限时 buff，按当前兵种重建基准。由 StartNewGameCommand 调用。</summary>
        void ResetStats();

        // ---------- 当前兵种 ----------
        int ClassIndex { get; }
        BodyDefinition Body { get; }
        WeaponDefinition Weapon { get; }
        CharacterSpriteSet Sprites { get; }
        string ClassDisplayName { get; }

        // ---------- 基准值（由 角色 × 武器 派生，只读） ----------
        float MaxHp { get; }
        float BaseSpeed { get; }
        float BaseDamage { get; }
        float BaseRate { get; }
        float BaseRange { get; }
        float HitRadius { get; }

        // ---------- 加成项（升级卡与限时 buff 改这些） ----------
        float DamageMul { get; set; }
        float RateMul { get; set; }
        float RangeMul { get; set; }
        float SpeedMul { get; set; }
        /// <summary>减伤比例，0~0.75 封顶。</summary>
        float Armor { get; set; }
        float CritChance { get; set; }
        int Pierce { get; set; }
        int ExtraShots { get; set; }
        float ExtraSpread { get; set; }
        float BurnBonus { get; set; }
        float VampPerKill { get; set; }
        float Thorns { get; set; }
        float ExpMul { get; set; }
        /// <summary>弹药（仅有限弹药的武器有意义）。</summary>
        float Ammo { get; set; }
        /// <summary>限时伤害翻倍（超级力量），>0 表示生效中。</summary>
        float PowerBuffTime { get; set; }
        /// <summary>限时减伤（护甲 buff）。</summary>
        float ArmorBuffTime { get; set; }
        /// <summary>限时加速。</summary>
        float SpeedBuffTime { get; set; }

        // ---------- 最终值（Controller 每帧拉取） ----------
        float Damage { get; }
        /// <summary>两次攻击的间隔秒数。</summary>
        float AttackInterval { get; }
        float Speed { get; }
        float Range { get; }
        /// <summary>受伤害时的实际减伤比例（含限时护甲）。</summary>
        float EffectiveArmor { get; }
    }
}
