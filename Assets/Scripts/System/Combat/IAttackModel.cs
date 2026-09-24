using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 攻击模型眼中的「一个可打的敌人」。
    ///
    /// 为什么要有这层接口：System 层不能引用 <c>EnemyController</c>（Controller 属于业务表现层，
    /// 依赖方向会反）。所以由 System 定义接口、由 Controller 实现它 ——
    /// 依赖箭头是「Controller 实现 System 定义的接口」，方向正确。
    /// </summary>
    public interface IEnemyTarget
    {
        Vector2 Position { get; }
        /// <summary>受击半径（世界单位）。用于射程与接触判定。</summary>
        float Radius { get; }
        bool IsAlive { get; }

        void TakeDamage(float damage);
        /// <summary>施加击退。<paramref name="direction"/> 需为单位向量。</summary>
        void ApplyKnockback(Vector2 direction, float strength);
        /// <summary>叠加燃烧：每秒 <paramref name="dps"/> 点，持续 <paramref name="duration"/> 秒。</summary>
        void ApplyBurn(float dps, float duration);
    }

    /// <summary>
    /// 一次攻击的结算上下文。由 <c>PlayerCombatController</c> 每帧填好并复用，
    /// 避免每次攻击都分配对象。
    /// </summary>
    public class AttackContext
    {
        public Vector2 Origin;
        public float AimRadians;
        public WeaponDefinition Weapon;

        // 以下为「玩家最终值」，由 PlayerStatSystem 派生后填入 —— 攻击模型不自己读系统，
        // 这样模型是纯函数式的，便于单测与复用（例如以后给敌人也用同一套模型）
        public float Damage;
        public float Range;
        public int Pierce;
        public int Shots;
        public float Spread;
        public float ProjectileSpeedMul = 1f;
        public float BurnBonus;
        public float CritChance;

        public IReadOnlyList<IEnemyTarget> Enemies;

        /// <summary>生成投射物。参数：起点、角度（弧度）、是否暴击。</summary>
        public System.Action<Vector2, float, bool> SpawnProjectile;

        /// <summary>生成攻击特效。参数：扇形角度或锥形角度、射程。</summary>
        public System.Action<Vector2, float, float> SpawnArcVfx;

        /// <summary>距离最近的存活敌人；无目标返回 null。</summary>
        public IEnemyTarget NearestEnemy()
        {
            if (Enemies == null) return null;
            IEnemyTarget best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.IsAlive) continue;
                float sqr = (e.Position - Origin).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = e; }
            }
            return best;
        }

        /// <summary>射程内是否有存活敌人（近战/锥形要求有目标才出手）。</summary>
        public bool HasEnemyInRange()
        {
            if (Enemies == null) return false;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.IsAlive) continue;
                float reach = Range + e.Radius;
                if ((e.Position - Origin).sqrMagnitude <= reach * reach) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 攻击模型（业务逻辑层，纯 C#）。
    /// 5 把武器归成 4 个模型：扇形覆盖匕首与棒球棍，连发投射物覆盖步枪。
    /// </summary>
    public interface IAttackModel
    {
        AttackModel Kind { get; }

        /// <summary>是否满足出手条件。投射物类要求射程内有目标（与 H5 一致）。</summary>
        bool CanFire(AttackContext ctx);

        /// <summary>
        /// 结算一次攻击。
        /// 返回**延迟结算点**：0 表示本次调用已立即结算（投射物类把伤害交给飞行体，
        /// 锥形按 tick 直接扣血）；大于 0 表示要在攻击动画推进到该**归一化进度**时
        /// 再调一次 <see cref="Resolve"/>（扇形要等动画播到打击帧才出伤害）。
        ///
        /// 用归一化进度而不是秒数，是因为攻击动画的帧数与时长逐武器不同（手枪 5 帧、
        /// 棒球棍 12 帧），归一化后伤害时机与动画长度天然解耦 —— 这正是策划案里
        /// 「必须在动画上挂打击帧、不能按动画播完结算」那条要求的落地方式。
        /// </summary>
        float Fire(AttackContext ctx);

        /// <summary>延迟到点后的伤害结算。仅扇形模型有实现，其余为空操作。</summary>
        void Resolve(AttackContext ctx);
    }
}
