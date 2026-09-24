using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 敌人（表现层 MonoBehaviour，IController）：向玩家追击、受击扣血、死亡掉落并回收对象池。
    /// 维护静态活跃敌人表，供玩家自动瞄准与攻击模型查询。
    ///
    /// 数值全部来自 <see cref="EnemyDefinition"/>（11 种敌人 = 11 份配置 + 11 个预制体），
    /// 本类不再持有任何可调数值 —— 消掉工程遗留项「数值硬编码在预制体的 SerializeField 上」。
    ///
    /// 实现 <see cref="IEnemyTarget"/> 供 System 层的攻击模型使用 —— 依赖方向是
    /// 「Controller 实现 System 定义的接口」，System 不反向引用本类。
    ///
    /// 碰撞全部是手写距离判定，没有任何 Collider / Rigidbody：
    /// 换成 Sprite 之后这套判定依然成立，因为它只读 <c>transform.position</c>。
    /// </summary>
    public class EnemyController : MonoBehaviour, IController, IEnemyTarget
    {
        [Header("配置")]
        [SerializeField] private EnemyDefinition definition;

        [Header("表现")]
        [SerializeField] private CharacterView view;
        [Tooltip("血条底（纯色 sprite，PPU=1，靠 localScale 定尺寸）")]
        [SerializeField] private SpriteRenderer hpBarBg;
        [SerializeField] private SpriteRenderer hpBarFill;

        [Header("掉落")]
        [SerializeField] private GameObject expDropPrefab;

        [Header("血条尺寸（世界单位）")]
        [SerializeField] private float barWidth = 0.9f;
        [SerializeField] private float barHeight = 0.09f;
        [SerializeField] private float barOffsetY = 0.85f;

        private float currentHealth;
        private float maxHealth;
        private Transform player;
        private bool alive;

        // 击退：一个快速衰减的速度，与追击速度分开累加
        private Vector2 knockback;

        // 燃烧 DOT
        private float burnTimeLeft;
        private float burnDps;
        private float burnTickAccum;

        private float attackCooldown;

        private static readonly HashSet<EnemyController> ActiveEnemies = new HashSet<EnemyController>();
        private static readonly List<EnemyController> ActiveList = new List<EnemyController>();

        /// <summary>当前所有活跃敌人（只读）。攻击模型通过它做范围查询。</summary>
        public static IReadOnlyList<EnemyController> All => ActiveList;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        public EnemyDefinition Definition => definition;

        // ================= IEnemyTarget =================

        public Vector2 Position => transform.position;
        public float Radius => definition != null ? definition.hitRadius : 0.4f;
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
            for (int i = 0; i < ActiveList.Count; i++)
            {
                var e = ActiveList[i];
                // 防御：场景重载后残留的销毁引用，直接跳过
                if (e == null || !e.isActiveAndEnabled || !e.alive) continue;
                float sqr = (e.transform.position - position).sqrMagnitude;
                if (sqr < minSqr) { minSqr = sqr; nearest = e; }
            }
            return nearest;
        }

        /// <summary>重开一局时清空活跃敌人表（移除场景重载前残留的引用）。</summary>
        public static void ClearActiveEnemies()
        {
            ActiveEnemies.Clear();
            ActiveList.Clear();
        }

        /// <summary>由刷怪器注入本实例代表哪种敌人。</summary>
        public void Init(EnemyDefinition def, Transform player)
        {
            definition = def;
            this.player = player;

            maxHealth = def != null ? def.maxHp : 10f;
            currentHealth = maxHealth;

            knockback = Vector2.zero;
            burnTimeLeft = 0f; burnDps = 0f; burnTickAccum = 0f;
            attackCooldown = 0f;

            if (view != null)
            {
                view.Configure(def);   // def 实现 ISpriteProvider
            }

            LayoutHealthBar();
            RefreshHealthBar();
        }

        private void OnEnable()
        {
            alive = true;
            if (ActiveEnemies.Add(this)) ActiveList.Add(this);

            // 对象池复用必须完整复位可视状态，不能依赖 OnEnable 的默认值
            // （工程约定：渲染更新必须完整清除并重绘动态层，禁止增量更新导致残影）
            view?.ResetView();
            if (hpBarFill != null) hpBarFill.enabled = false;
            if (hpBarBg != null) hpBarBg.enabled = false;
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
            transform.Translate(dir * (MoveSpeed * dt));

            ApplySeparation(dt);
            UpdateView(dir);
            TickContact(toPlayer, dt);
        }

        private float MoveSpeed => definition != null ? definition.moveSpeed : 1.5f;
        private float TouchDamage => definition != null ? definition.touchDamage : 5f;
        private float ContactInterval => definition != null ? definition.contactInterval : 1f;

        /// <summary>
        /// 与其它敌人互相推开，避免叠成一坨。
        /// H5 原型里同一问题出现过：初版分离系数 0.55 太弱，敌人全叠在玩家身上糊成一团，
        /// 调到 3.2 才散得开。系数在 GameConfig 里。
        /// </summary>
        private void ApplySeparation(float dt)
        {
            var cfg = ConfigHolder?.Config;
            float strength = cfg != null ? cfg.enemySeparation : 3.2f;
            float radiusFactor = cfg != null ? cfg.enemySeparationRadiusFactor : 1.18f;

            float sx = 0f, sy = 0f;
            for (int i = 0; i < ActiveList.Count; i++)
            {
                var o = ActiveList[i];
                if (o == null || o == this || !o.alive) continue;

                float ox = transform.position.x - o.transform.position.x;
                float oy = transform.position.y - o.transform.position.y;
                float rr = (Radius + o.Radius) * radiusFactor;
                float dd = ox * ox + oy * oy;
                if (dd <= 1e-6f || dd >= rr * rr) continue;

                float dl = Mathf.Sqrt(dd);
                float push = 1f - dl / rr;
                sx += ox / dl * push;
                sy += oy / dl * push;
            }

            if (sx != 0f || sy != 0f)
            {
                transform.Translate(new Vector3(sx, sy, 0f) * (strength * dt));
            }
        }

        private GameConfigHolder cachedHolder;

        private GameConfigHolder ConfigHolder
        {
            get
            {
                // 缓存：这一段每次分离判定都会走，不能每帧 FindObjectOfType
                if (cachedHolder == null) cachedHolder = Object.FindObjectOfType<GameConfigHolder>();
                return cachedHolder;
            }
        }

        private void UpdateView(Vector3 dir)
        {
            if (view == null) return;
            view.SetAim(Mathf.Atan2(dir.y, dir.x));
            view.Play(AnimState.Walk);
        }

        private void TickContact(Vector3 toPlayer, float dt)
        {
            attackCooldown -= dt;
            if (attackCooldown > 0f) return;

            // 接触距离 = 本敌半径 + 玩家半径 + 余量。
            // 之前是固定的 0.6²，那是照 Cube 的 1×1 视觉调的；不参数化的话
            // 兵种的受击半径差异形同虚设。
            var cfg = ConfigHolder?.Config;
            float padding = cfg != null ? cfg.contactPadding : 0.32f;
            var stats = this.GetSystem<IPlayerStatSystem>();
            float playerRadius = stats != null && stats.IsBound ? stats.HitRadius : 0.4f;

            float reach = Radius + playerRadius + padding;
            if (toPlayer.sqrMagnitude >= reach * reach) return;

            var playerCtrl = player.GetComponent<PlayerController>();
            if (playerCtrl == null) return;

            playerCtrl.TakeDamage(TouchDamage);
            attackCooldown = ContactInterval;
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

        // ================= 受限 / 死亡 =================

        public void TakeDamage(float damage)
        {
            if (!alive) return;
            currentHealth -= damage;
            view?.Flash(0.8f);
            RefreshHealthBar();
            if (currentHealth <= 0f) Die();
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

        // ================= 血条 =================
        // 用两张 PPU=1 的纯色 sprite 拼成：底条 + 填充条。
        // 填充条靠 localScale.x 与左端对齐的 localPosition 表达比例 ——
        // 不用 Image.fillAmount 是因为工程约定里那条「无 sprite 的 Image 上
        // fillAmount 不生效」，而且这是世界空间的 SpriteRenderer，不是 UI。

        private void LayoutHealthBar()
        {
            float w = definition != null && definition.IsBoss ? barWidth * 1.8f : barWidth;

            if (hpBarBg != null)
            {
                hpBarBg.transform.localPosition = new Vector3(0f, barOffsetY, 0f);
                hpBarBg.transform.localScale = new Vector3(w, barHeight, 1f);
            }
            if (hpBarFill != null)
            {
                hpBarFill.transform.localPosition = new Vector3(0f, barOffsetY, 0f);
                hpBarFill.transform.localScale = new Vector3(w, barHeight * 0.72f, 1f);
            }
        }

        private void RefreshHealthBar()
        {
            bool show = alive && maxHealth > 0f && currentHealth < maxHealth;
            // BOSS 始终显示血条（满血也显示，便于玩家找目标）
            if (definition != null && definition.IsBoss) show = alive;

            if (hpBarBg != null) hpBarBg.enabled = show;
            if (hpBarFill == null) return;
            hpBarFill.enabled = show;
            if (!show) return;

            float w = definition != null && definition.IsBoss ? barWidth * 1.8f : barWidth;
            float ratio = Mathf.Clamp01(currentHealth / maxHealth);
            float fillW = w * ratio;

            hpBarFill.transform.localScale = new Vector3(fillW, barHeight * 0.72f, 1f);
            // 从左端对齐：底条中心在 0，填充条中心应在 -w/2 + fillW/2
            hpBarFill.transform.localPosition = new Vector3(-w * 0.5f + fillW * 0.5f, barOffsetY, 0f);
        }
    }
}
