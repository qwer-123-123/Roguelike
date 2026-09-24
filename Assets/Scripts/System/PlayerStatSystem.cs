using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// <see cref="IPlayerStatSystem"/> 的实现。
    /// 派生子：最终值 = 基准值(角色 × 武器) × 加成项；限时 buff 再叠一层。
    /// </summary>
    public class PlayerStatSystem : AbstractSystem, IPlayerStatSystem
    {
        private ClassCatalog catalog;
        private GameConfig config;

        // ---------- 加成项 ----------
        private float damageMul = 1f;
        private float rateMul = 1f;
        private float rangeMul = 1f;
        private float speedMul = 1f;
        private float armor;
        private float critChance;
        private int pierce;
        private int extraShots;
        private float extraSpread;
        private float burnBonus;
        private float vampPerKill;
        private float thorns;
        private float expMul = 1f;
        private float ammo;
        private float powerBuffTime;
        private float armorBuffTime;
        private float speedBuffTime;

        public bool IsBound => catalog != null && config != null;

        protected override void OnInit()
        {
            // 兵种切换时重建基准。
            // 注意：Model.Reset() 走 SetValueWithoutEvent 不会触发这里，
            // 所以 StartNewGameCommand 必须显式调一次 ResetStats()（与 HUD 的刷新约定同理）。
            var model = this.GetModel<IGameStateModel>();
            model.SelectedClassIndex.Register(_ => ResetStats());
        }

        public void Bind(ClassCatalog c, GameConfig g)
        {
            catalog = c;
            config = g;
            ResetStats();
        }

        // ================= 兵种 =================

        public int ClassIndex => this.GetModel<IGameStateModel>().SelectedClassIndex.Value;

        public BodyDefinition Body
        {
            get
            {
                if (catalog == null) return null;
                int i = Mathf.Clamp(ClassIndex, 0, Mathf.Max(0, catalog.Count - 1));
                return catalog.BodyAt(i);
            }
        }

        public WeaponDefinition Weapon
        {
            get
            {
                if (catalog == null) return null;
                int i = Mathf.Clamp(ClassIndex, 0, Mathf.Max(0, catalog.Count - 1));
                return catalog.WeaponAt(i);
            }
        }

        public CharacterSpriteSet Sprites
        {
            get
            {
                if (catalog == null) return null;
                return catalog.SpritesAt(Mathf.Clamp(ClassIndex, 0, Mathf.Max(0, catalog.Count - 1)));
            }
        }

        public string ClassDisplayName => catalog != null ? catalog.DisplayNameAt(ClassIndex) : "?";

        // ================= 基准值 =================

        public float MaxHp => Body != null ? Body.baseHp : 100f;
        public float BaseSpeed => Body != null ? Body.baseSpeed : 3.2f;
        public float HitRadius => Body != null ? Body.hitRadius : 0.4f;

        /// <summary>角色伤害修正乘在武器基础伤害上。</summary>
        public float BaseDamage => Weapon != null ? Weapon.damage * (Body != null ? Body.damageMultiplier : 1f) : 10f;

        /// <summary>角色攻速修正作用于攻击次数。</summary>
        public float BaseRate => Weapon != null ? Weapon.rate * (Body != null ? Body.attackRateMultiplier : 1f) : 1f;

        public float BaseRange => Weapon != null ? Weapon.range : 5f;

        // ================= 加成项 =================

        public float DamageMul { get => damageMul; set => damageMul = value; }
        public float RateMul { get => rateMul; set => rateMul = value; }
        public float RangeMul { get => rangeMul; set => rangeMul = value; }
        public float SpeedMul { get => speedMul; set => speedMul = Mathf.Max(0.1f, value); }
        public float Armor { get => armor; set => armor = Mathf.Clamp(value, -0.5f, 0.75f); }
        public float CritChance { get => critChance; set => critChance = Mathf.Clamp01(value); }
        public int Pierce { get => pierce; set => pierce = Mathf.Max(0, value); }
        public int ExtraShots { get => extraShots; set => extraShots = Mathf.Max(0, value); }
        public float ExtraSpread { get => extraSpread; set => extraSpread = Mathf.Max(0f, value); }
        public float BurnBonus { get => burnBonus; set => burnBonus = Mathf.Max(0f, value); }
        public float VampPerKill { get => vampPerKill; set => vampPerKill = Mathf.Max(0f, value); }
        public float Thorns { get => thorns; set => thorns = Mathf.Max(0f, value); }
        public float ExpMul { get => expMul; set => expMul = Mathf.Max(0.1f, value); }
        public float Ammo { get => ammo; set => ammo = Mathf.Max(0f, value); }
        public float PowerBuffTime { get => powerBuffTime; set => powerBuffTime = Mathf.Max(0f, value); }
        public float ArmorBuffTime { get => armorBuffTime; set => armorBuffTime = Mathf.Max(0f, value); }
        public float SpeedBuffTime { get => speedBuffTime; set => speedBuffTime = Mathf.Max(0f, value); }

        // ================= 最终值 =================

        public float Damage => BaseDamage * damageMul * (powerBuffTime > 0f ? 2f : 1f);

        public float AttackInterval
        {
            get
            {
                float rate = BaseRate * rateMul;
                return rate > 0.0001f ? 1f / rate : 999f;
            }
        }

        public float Speed => BaseSpeed * speedMul * (speedBuffTime > 0f ? 1.3f : 1f);

        public float Range => BaseRange * rangeMul;

        /// <summary>减伤 = 常驻护甲 + 限时护甲，封顶 75%。</summary>
        public float EffectiveArmor => Mathf.Clamp(armor + (armorBuffTime > 0f ? 0.15f : 0f), -0.5f, 0.75f);

        // ================= 复位 =================

        /// <summary>
        /// 清掉全部加成与限时 buff，按**当前兵种**重建基准。
        /// **刻意不动 <c>SelectedClassIndex</c>** —— 重开一局沿用同一兵种是预期行为
        /// （H5 的「重开」也是沿用当前兵种）。别顺手把它一并复位。
        /// </summary>
        public void ResetStats()
        {
            damageMul = 1f;
            rateMul = 1f;
            rangeMul = 1f;
            speedMul = 1f;
            armor = 0f;
            critChance = 0f;
            pierce = 0;
            extraShots = 0;
            extraSpread = 0f;
            burnBonus = 0f;
            vampPerKill = 0f;
            thorns = 0f;
            expMul = 1f;
            powerBuffTime = 0f;
            armorBuffTime = 0f;
            speedBuffTime = 0f;

            ammo = Weapon != null ? Weapon.maxAmmo : 0f;
        }
    }
}
