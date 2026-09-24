using UnityEngine;

namespace Game
{
    /// <summary>
    /// 扇形瞬时判定（匕首 / 棒球棍）。
    /// 与 H5 的 <c>Player.resolveArc</c> 一致：距离在射程内 **且** 与瞄准方向的夹角
    /// 在半张角内才算命中。伤害不立即结算 —— 返回归一化进度让调用方等到打击帧。
    /// </summary>
    public class ArcAttack : IAttackModel
    {
        public AttackModel Kind => AttackModel.Arc;

        public bool CanFire(AttackContext ctx) => ctx.HasEnemyInRange();

        public float Fire(AttackContext ctx)
        {
            // 出手先播特效（挥砍弧），伤害延后到打击帧
            ctx.SpawnArcVfx?.Invoke(ctx.Origin, ctx.AimRadians, ctx.Weapon.arcDegrees);
            return Mathf.Clamp01(ctx.Weapon.hitAt);
        }

        public void Resolve(AttackContext ctx)
        {
            var w = ctx.Weapon;
            float half = w.arcDegrees * 0.5f * Mathf.Deg2Rad;
            var enemies = ctx.Enemies;
            if (enemies == null) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;

                Vector2 delta = e.Position - ctx.Origin;
                float reach = ctx.Range + e.Radius;
                if (delta.sqrMagnitude > reach * reach) continue;

                // 夹角判定：注意零向量（敌人与玩家完全重合）时 atan2 为 0，直接算作命中
                if (delta.sqrMagnitude > 0.000001f)
                {
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, ctx.AimRadians * Mathf.Rad2Deg))
                        > w.arcDegrees * 0.5f) continue;
                }

                // 暴击只作用于投射物（与 H5 一致：resolveArc 与锥形都不掷暴击）
                e.TakeDamage(ctx.Damage);
                if (w.knockback > 0f && delta.sqrMagnitude > 0.000001f)
                {
                    e.ApplyKnockback(delta.normalized, w.knockback);
                }
            }
        }
    }

    /// <summary>单发投射物（手枪）。伤害由飞行体负责，这里只负责派发。</summary>
    public class ProjectileAttack : IAttackModel
    {
        // 必须是 virtual：连发模型要覆写它，否则通过 IAttackModel 接口取 Kind 时
        // 会拿到基类的 Projectile（用 new 隐藏属性在接口分派下不生效）
        public virtual AttackModel Kind => AttackModel.Projectile;

        public bool CanFire(AttackContext ctx) => ctx.HasEnemyInRange();

        public float Fire(AttackContext ctx)
        {
            FireVolley(ctx);
            return 0f;
        }

        public void Resolve(AttackContext ctx) { }

        /// <summary>按 shots / spread 散射一组投射物。</summary>
        protected static void FireVolley(AttackContext ctx)
        {
            int n = Mathf.Max(1, ctx.Shots);
            for (int i = 0; i < n; i++)
            {
                float offset = n == 1 ? 0f : (i - (n - 1) * 0.5f) * ctx.Spread;
                bool crit = ctx.CritChance > 0f && Random.value < ctx.CritChance;
                ctx.SpawnProjectile?.Invoke(ctx.Origin, ctx.AimRadians + offset, crit);
            }
        }
    }

    /// <summary>
    /// 连发投射物（步枪）。连发的「间隔」由控制器按 burstGap 逐发派发 ——
    /// 模型这边一次只发一发，避免在业务层引入计时状态。
    /// </summary>
    public class BurstProjectileAttack : ProjectileAttack
    {
        public override AttackModel Kind => AttackModel.BurstProjectile;
    }

    /// <summary>
    /// 锥形持续判定 + 燃烧（火焰喷射器）。
    /// 每次 tick 都对锥形范围内的敌人造成伤害并刷新燃烧 —— 不做「单发」概念，
    /// 所以 Fire 直接结算、返回 0。
    /// </summary>
    public class ConeAttack : IAttackModel
    {
        public AttackModel Kind => AttackModel.Cone;

        public bool CanFire(AttackContext ctx) => ctx.HasEnemyInRange();

        public float Fire(AttackContext ctx)
        {
            var w = ctx.Weapon;
            float half = w.coneDegrees * 0.5f;
            var enemies = ctx.Enemies;

            ctx.SpawnArcVfx?.Invoke(ctx.Origin, ctx.AimRadians, w.coneDegrees);

            if (enemies == null) return 0f;

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;

                Vector2 delta = e.Position - ctx.Origin;
                float reach = ctx.Range + e.Radius;
                if (delta.sqrMagnitude > reach * reach) continue;

                if (delta.sqrMagnitude > 0.000001f)
                {
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, ctx.AimRadians * Mathf.Rad2Deg)) > half)
                        continue;
                }

                e.TakeDamage(ctx.Damage);

                float burnDps = w.burnDps + ctx.BurnBonus;
                if (burnDps > 0f)
                {
                    e.ApplyBurn(burnDps, Mathf.Max(w.burnDuration, 2f));
                }
            }
            return 0f;
        }

        public void Resolve(AttackContext ctx) { }
    }

    /// <summary>按武器的攻击模型取对应实现。四种实现都是无状态的，复用单例即可。</summary>
    public static class AttackModelFactory
    {
        private static readonly IAttackModel Arc = new ArcAttack();
        private static readonly IAttackModel Projectile = new ProjectileAttack();
        private static readonly IAttackModel Burst = new BurstProjectileAttack();
        private static readonly IAttackModel Cone = new ConeAttack();

        public static IAttackModel Get(AttackModel kind)
        {
            switch (kind)
            {
                case AttackModel.Arc:             return Arc;
                case AttackModel.Projectile:      return Projectile;
                case AttackModel.BurstProjectile: return Burst;
                case AttackModel.Cone:            return Cone;
                default:                          return Projectile;
            }
        }
    }
}
