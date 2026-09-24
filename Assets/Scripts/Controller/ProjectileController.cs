using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 投射物（表现层 MonoBehaviour，IController）：沿朝向直线飞行，命中造成伤害。
    ///
    /// 与旧版的区别：**不再追踪单个目标**，改为直线飞行 —— 与 H5 原型一致
    /// （直线弹道才能支撑散射、连发、穿透这三样，追踪弹做不到）。
    /// 命中判定仍是手写距离检测，不引入 Collider2D。
    /// </summary>
    public class ProjectileController : MonoBehaviour, IController
    {
        [SerializeField] private float hitRadius = 0.13f;
        [Tooltip("超时回收，防止打空的投射物永远留在场上")]
        [SerializeField] private float lifeSeconds = 2f;

        private Vector2 velocity;
        private float damage;
        private int pierce;
        private float burnDps;
        private float burnDuration;
        private bool crit;
        private float lifeLeft;

        // 穿透时不能对同一个敌人重复扣血
        private readonly List<EnemyController> alreadyHit = new();

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>由发射方注入全部参数（数值已在 PlayerStatSystem 里算成最终值）。</summary>
        public void Init(Vector2 origin, float angleRadians, float speed, float damage,
                         int pierce, float burnDps, float burnDuration, bool crit)
        {
            transform.position = origin;
            velocity = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * speed;
            this.damage = damage;
            this.pierce = Mathf.Max(0, pierce);
            this.burnDps = burnDps;
            this.burnDuration = burnDuration;
            this.crit = crit;
            lifeLeft = lifeSeconds;
            alreadyHit.Clear();
        }

        private void Update()
        {
            // 阶段门禁：非局内阶段投射物静止在空中（死亡后世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            float dt = Time.deltaTime;
            transform.Translate(velocity * dt);

            lifeLeft -= dt;
            if (lifeLeft <= 0f)
            {
                this.GetUtility<IPoolUtility>().Despawn(gameObject);
                return;
            }

            var enemies = EnemyController.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                if (alreadyHit.Contains(e)) continue;

                float reach = hitRadius + e.Radius;
                if ((e.Position - (Vector2)transform.position).sqrMagnitude > reach * reach) continue;

                alreadyHit.Add(e);

                float dealt = crit ? damage * 2f : damage;
                e.TakeDamage(dealt);
                if (burnDps > 0f) e.ApplyBurn(burnDps, burnDuration);

                if (alreadyHit.Count > pierce)
                {
                    this.GetUtility<IPoolUtility>().Despawn(gameObject);
                    return;
                }
            }
        }
    }
}
