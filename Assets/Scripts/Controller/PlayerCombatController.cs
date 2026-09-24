using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 玩家自动攻击（表现层 MonoBehaviour，IController）。
    ///
    /// 职责边界：
    ///   - 每帧把 <see cref="IPlayerStatSystem"/> 的最终值与敌人表填进 <see cref="AttackContext"/>
    ///   - 按武器的攻击模型派发攻击（<see cref="AttackModelFactory"/>）
    ///   - 管弹药、连发节奏、打击帧延迟结算
    ///   **不含任何伤害数值** —— 数值全部来自系统层。
    ///
    /// 打击帧机制：扇形攻击在 <c>Fire</c> 时只播动画，返回一个「归一化进度」；
    /// 本类等角色动画推进到该进度才调 <c>Resolve</c> 结算伤害。这样伤害时机与
    /// 动画长度解耦 —— 手枪 5 帧、棒球棍 12 帧，攻速加成不会把动作压变形。
    /// </summary>
    public class PlayerCombatController : MonoBehaviour, IController
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private PlayerController player;

        private readonly AttackContext ctx = new AttackContext();
        private readonly List<IEnemyTarget> targetBuffer = new List<IEnemyTarget>();

        private float attackTimer;
        private int burstLeft;
        private float burstTimer;

        private bool pendingResolve;
        private float pendingProgress;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private IPlayerStatSystem Stats => this.GetSystem<IPlayerStatSystem>();

        /// <summary>当前使用的攻击模型类型（自检与调试用）。</summary>
        public AttackModel CurrentModel => Stats.Weapon != null ? Stats.Weapon.model : AttackModel.Projectile;

        public void ResetForNewGame()
        {
            attackTimer = 0f;
            burstLeft = 0;
            burstTimer = 0f;
            pendingResolve = false;
        }

        private void Start()
        {
            if (player == null) player = GetComponent<PlayerController>();
            ResetForNewGame();
        }

        private void Update()
        {
            if (projectilePrefab == null || player == null) return;
            // 阶段门禁：开始界面未开始、或玩家已死亡时不发射（与其余玩法逻辑一致）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            float dt = Time.deltaTime;
            var weapon = Stats.Weapon;
            if (weapon == null) return;

            BuildContext();

            TickAmmo(weapon, dt);
            TickBurst(dt);
            TickPendingResolve(dt);

            // 攻击计时
            attackTimer += dt;
            if (attackTimer >= Stats.AttackInterval)
            {
                var model = AttackModelFactory.Get(weapon.model);
                if (model.CanFire(ctx))
                {
                    attackTimer = 0f;
                    StartAttack(model, weapon);
                }
            }

            // 攻击动画播完回到待机/行走
            if (player.IsAttackAnimFinished && !pendingResolve)
            {
                player.SetAim(ctx.AimRadians);
            }
        }

        // ================= 上下文 =================

        private void BuildContext()
        {
            var weapon = Stats.Weapon;

            // 敌人表：每帧重建，避免持有失效引用。
            // 用重填复用的 List 而不是每次 new，割草时这个循环每秒要跑几十次。
            targetBuffer.Clear();
            var all = EnemyController.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e != null && e.IsAlive) targetBuffer.Add(e);
            }

            var origin = (Vector2)transform.position;

            // 瞄准：优先最近敌人；没有敌人时朝移动方向
            float aim = ctx.AimRadians;
            EnemyController nearest = EnemyController.FindNearest(transform.position);
            if (nearest != null)
            {
                Vector2 d = nearest.Position - origin;
                aim = Mathf.Atan2(d.y, d.x);
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (h * h + v * v > 0.0001f) aim = Mathf.Atan2(v, h);
            }

            ctx.Origin = origin;
            ctx.AimRadians = aim;
            ctx.Weapon = weapon;
            ctx.Damage = Stats.Damage;
            ctx.Range = Stats.Range;
            ctx.Pierce = Stats.Pierce;
            ctx.Shots = (weapon.shots <= 0 ? 1 : weapon.shots) + Stats.ExtraShots;
            ctx.Spread = weapon.spread + Stats.ExtraSpread;
            ctx.ProjectileSpeedMul = 1f;
            ctx.BurnBonus = Stats.BurnBonus;
            ctx.CritChance = Stats.CritChance;
            ctx.Enemies = targetBuffer;
            ctx.SpawnProjectile = SpawnProjectile;
            ctx.SpawnArcVfx = null;

            player.SetAim(aim);
        }

        // ================= 攻击 =================

        private void StartAttack(IAttackModel model, WeaponDefinition weapon)
        {
            // 有限弹药的武器：没弹就不出手
            if (weapon.maxAmmo > 0 && Stats.Ammo < 1f) return;

            player.PlayAttackAnim();

            if (weapon.model == AttackModel.Arc)
            {
                // 扇形：先播动作，伤害等动画推进到打击帧再结算
                pendingProgress = model.Fire(ctx);
                pendingResolve = pendingProgress > 0f;
                if (!pendingResolve) model.Resolve(ctx);
                return;
            }

            model.Fire(ctx);

            if (weapon.maxAmmo > 0)
            {
                Stats.Ammo = Mathf.Max(0f, Stats.Ammo - 1f);
            }

            // 连发：把剩余的发数排进 burst 队列，由 TickBurst 逐发派发
            if (weapon.burst > 1)
            {
                burstLeft = weapon.burst - 1;
                burstTimer = weapon.burstGap;
            }
        }

        private void TickBurst(float dt)
        {
            if (burstLeft <= 0) return;

            var weapon = Stats.Weapon;
            if (weapon == null) { burstLeft = 0; return; }

            burstTimer -= dt;
            if (burstTimer > 0f) return;

            burstTimer = weapon.burstGap;
            burstLeft--;
            AttackModelFactory.Get(weapon.model).Fire(ctx);
        }

        private void TickPendingResolve(float dt)
        {
            if (!pendingResolve) return;

            var weapon = Stats.Weapon;
            if (weapon == null) { pendingResolve = false; return; }

            if (player.AttackAnimProgress >= pendingProgress)
            {
                pendingResolve = false;
                AttackModelFactory.Get(weapon.model).Resolve(ctx);
            }
        }

        private void TickAmmo(WeaponDefinition weapon, float dt)
        {
            if (weapon.maxAmmo <= 0) return;
            if (weapon.ammoRegen <= 0f) return;
            Stats.Ammo = Mathf.Min(weapon.maxAmmo, Stats.Ammo + weapon.ammoRegen * dt);
        }

        private void SpawnProjectile(Vector2 origin, float angle, bool crit)
        {
            var weapon = Stats.Weapon;
            if (weapon == null) return;

            var pool = this.GetUtility<IPoolUtility>();
            GameObject go = pool.Spawn(projectilePrefab, origin, Quaternion.identity);
            if (go == null) return;

            var proj = go.GetComponent<ProjectileController>();
            proj?.Init(origin, angle, weapon.projectileSpeed * ctx.ProjectileSpeedMul, ctx.Damage,
                       Stats.Pierce, weapon.burnDps + Stats.BurnBonus, weapon.burnDuration, crit);
        }
    }
}
