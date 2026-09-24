using UnityEngine;

namespace Game
{
    /// <summary>攻击模型。决定这一把武器走哪条判定路径。</summary>
    public enum AttackModel
    {
        /// <summary>扇形瞬时判定（匕首 / 棒球棍）。</summary>
        Arc,
        /// <summary>单发投射物（手枪）。</summary>
        Projectile,
        /// <summary>连发投射物（步枪）。</summary>
        BurstProjectile,
        /// <summary>锥形持续判定 + 燃烧（火焰喷射器）。</summary>
        Cone,
    }

    /// <summary>
    /// 武器层配置（兵种的「武器」维度）。
    /// 对应 H5 原型 <c>data.js</c> 的 <c>WEAPONS</c>。
    /// 字段默认值直接抄自 H5，新建资产即等于原型数值。
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "菌核狂潮/武器层 WeaponDefinition")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("标识")]
        public string weaponId = "gun";
        public string displayName = "手枪";
        public string role = "远程·均衡";
        public Sprite icon;

        [Header("攻击模型")]
        public AttackModel model = AttackModel.Projectile;
        [Tooltip("每秒攻击次数（扇形/投射物）或每秒 tick 数（锥形）")]
        public float rate = 1.6f;
        [Tooltip("射程（格）。超出则不进入攻击")]
        public float range = 6f;
        public float damage = 12f;

        [Header("扇形（Arc）")]
        [Tooltip("扇形张开角度（度）")]
        public float arcDegrees = 100f;
        [Tooltip("击退强度，0 表示不击退")]
        public float knockback = 0f;

        [Header("投射物（Projectile / BurstProjectile）")]
        public float projectileSpeed = 13f;
        [Tooltip("可穿透的敌人数量，0 表示命中即消失")]
        public int pierce = 0;
        [Tooltip("单次发射几发（散射）")]
        public int shots = 1;
        public float spread = 0f;
        [Tooltip("连发发数，1 表示不连发")]
        public int burst = 1;
        [Tooltip("连发间隔（秒）")]
        public float burstGap = 0.055f;

        [Header("锥形（Cone）")]
        public float coneDegrees = 38f;
        [Tooltip("命中附加的燃烧持续时间（秒），0 表示不燃烧")]
        public float burnDuration = 0f;
        [Tooltip("燃烧每秒伤害")]
        public float burnDps = 0f;

        [Header("弹药")]
        [Tooltip("弹匣容量，0 表示无限（近战/手枪）")]
        public int maxAmmo = 0;
        [Tooltip("每秒回复的弹药量")]
        public float ammoRegen = 0f;

        [Header("动画时序")]
        [Tooltip("攻击动画总帧数——用来自查：必须与 CharacterSpriteSet.atk 的帧数一致")]
        public int atkFrames = 5;
        [Tooltip("打击帧出现在动作进度的第几成（0~1）。伤害结算挂在这个归一化时间上，与动画长度解耦")]
        [Range(0f, 1f)] public float hitAt = 0.22f;
    }
}
