using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 敌人（表现层 MonoBehaviour，IController）：向玩家追击、受击扣血、死亡掉落并回收对象池。
    /// 维护静态活跃敌人表，供玩家自动瞄准与攻击模型查询。
    ///
    /// 实现 <see cref="IEnemyTarget"/> 供 System 层的攻击模型使用 —— 依赖方向是
    /// 「Controller 实现 System 定义的接口」，System 不反向引用本类。
    ///
    /// 碰撞全部是手写距离判定，没有任何 Collider / Rigidbody：
    /// 换成 Sprite 之后这套判定依然成立，因为它只读 <c>transform.position</c>。
    /// </summary>
    public class EnemyController : MonoBehaviour, IController, IEnemyTarget
    {
        [Header("数值（迭代 2 会迁到 EnemyDefinition 配置表）")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float maxHealth = 3f;
        [SerializeField] private float touchDamage = 1f;
        [Tooltip("接触判定的额外余量：实际接触距离 = 本敌半径 + 玩家半径 + 该值")]
        [SerializeField] private float contactPadding = 0.32f;
        [SerializeField] private float hitRadius = 0.4f;
        [SerializeField] private GameObject expDropPrefab;

        [Header("表现（迭代 2 接美术；为空时退化为占位图元）")]
        [SerializeField] private CharacterView view;

        private float currentHealth;
        private Transform player;
        private bool alive;

        // 击退：一个快速衰减的速度，与追击速度分开累加
        private Vector2 knockback;

        // 燃烧 DOT
        private float burnTimeLeft;
        private float burnDps;
        private float burnTickAccum;

        private float attackCooldown;
        private const float TouchDamageInterval = 1f;

        private static readonly HashSet<EnemyController> ActiveEnemies = new();
        private static readonly List<EnemyController> ActiveList = new();

        /// <summary>当前所有活跃敌人（只读）。攻击模型通过它做范围查询。</summary>
        public static IReadOnlyList<EnemyController> All => ActiveList;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        // ================= IEnemyTarget =================

        public Vector2 Position => transform.position;
        public float Radius => hitRadius;
        public bool IsAlive => alive;

        public void ApplyKnockback(Vector2 direction, float strength)
        {
            knockback += direction * strength;
        }

        public void ApplyBurn(float dps, float duration)
        {
            // 取更强的那个，不叠加层数（与 H5 一致：重复命中只刷新持续时间与强度）
            burnDps = Mathf.Max(burnDps, dps);
            burnTimeLeft = Mathf.Max(burnTimeLeft, duration);
        }

        // ================= 生命周期 =================

        /// <summary>返回离 position 最近的存活敌人（无则 null）。跳过被销毁/禁用的引用。</summary>
        public static EnemyController FindNearest(Vector3 position)
        {
            EnemyController nearest = null;
            float minSqr = float.MaxValue;
            foreach (var e in ActiveList)
            {
                // 防御：场景重载后残留的销毁引用，直接跳过
                if (e == null || !e.isActiveAndEnabled || !e.alive) continue;
                float sqr = (e.transform.position - position).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    nearest = e;
                }
            }

            return nearest;
        }

        /// <summary>重开一局时清空活跃敌人表（移除场景重载前残留的引用）。</summary>
        public static void ClearActiveEnemies()
        {
            ActiveEnemies.Clear();
            ActiveList.Clear();
        }

        public void Init(Transform player)
        {
            this.player = player;
        }

        private void OnEnable()
        {
            currentHealth = maxHealth;
            alive = true;
            knockback = Vector2.zero;
            burnTimeLeft = 0f;
            burnDps = 0f;
            burnTickAccum = 0f;
            attackCooldown = 0f;

            if (ActiveEnemies.Add(this)) ActiveList.Add(this);

            // 对象池复用的清残影：必须显式复位可视状态，不能依赖 OnEnable 的默认值
            // （工程约定：渲染更新必须完整清除并重绘动态层，禁止增量更新导致残影）
            view?.ResetView();
        }

        private void OnDisable()
        {
            alive = false;
            if (ActiveEnemies.Remove(this)) ActiveList.Remove(this);
        }

        // ================= 每帧 =================

        private void Update()
        {
            if (player == null || !alive) return;
            // 阶段门禁：非局内阶段敌人停止追击与接触伤害（死亡后世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            float dt = Time.deltaTime;

            TickBurn(dt);

            // 击退优先：与追击分开，衰减很快
            if (knockback.sqrMagnitude > 0.0001f)
            {
                transform.Translate(knockback * dt);
                knockback *= Mathf.Pow(0.0015f, dt);
            }

            Vector3 toPlayer = player.position - transform.position;
            Vector3 dir = toPlayer.normalized;
            transform.Translate(dir * (moveSpeed * dt));

            UpdateView(dir);

            // 距离检测：接触到玩家时施加伤害（带冷却），无需物理组件。
            // 半径改成「本敌半径 + 玩家半径 + 余量」—— 原先是固定的 0.6²，
            // 那是照 Cube 的 1×1 视觉调的，不参数化的话兵种的受击半径差异形同虚设。
            attackCooldown -= dt;
            if (attackCooldown <= 0f)
            {
                float reach = hitRadius + PlayerRadius() + contactPadding;
                if (toPlayer.sqrMagnitude < reach * reach)
                {
                    var playerCtrl = player.GetComponent<PlayerController>();
                    if (playerCtrl != null)
                    {
                        playerCtrl.TakeDamage(touchDamage);
                        attackCooldown = TouchDamageInterval;
                    }
                }
            }
        }

        private float PlayerRadius()
        {
            var stats = this.GetSystem<IPlayerStatSystem>();
            return stats != null && stats.IsBound ? stats.HitRadius : 0.4f;
        }

        private void UpdateView(Vector3 dir)
        {
            if (view == null) return;
            view.SetAim(Mathf.Atan2(dir.y, dir.x));
            view.Play(AnimState.Walk);
        }

        private void TickBurn(float dt)
        {
            if (burnTimeLeft <= 0f) return;

            burnTimeLeft -= dt;

            // 按 0.5 秒一跳结算，避免每帧扣血导致飘字刷屏
            burnTickAccum += dt;
            while (burnTickAccum >= 0.5f)
            {
                burnTickAccum -= 0.5f;
                TakeDamage(burnDps * 0.5f);
                if (!alive) return;
            }

            if (burnTimeLeft <= 0f) burnDps = 0f;
        }

        // ================= 受击 / 死亡 =================

        public void TakeDamage(float damage)
        {
            if (!alive) return;
            currentHealth -= damage;
            view?.Flash(0.8f);
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            alive = false;
            this.GetModel<IGameStateModel>().Kills.Value += 1;

            var pool = this.GetUtility<IPoolUtility>();

            if (expDropPrefab != null)
            {
                GameObject drop = pool.Spawn(expDropPrefab, transform.position, Quaternion.identity);
                drop.GetComponent<ExperienceDropController>()?.Init(player);
            }

            pool.Despawn(gameObject);
        }
    }
}
