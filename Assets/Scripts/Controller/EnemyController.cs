using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 敌人（表现层 MonoBehaviour，IController）：向玩家追击、受击扣血、死亡掉落并回收对象池。
    /// 维护静态活跃敌人表，供玩家自动瞄准。
    /// </summary>
    public class EnemyController : MonoBehaviour, IController
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private int touchDamage = 1;
        [SerializeField] private GameObject expDropPrefab;

        private int currentHealth;
        private Transform player;
        private bool alive;

        private static readonly HashSet<EnemyController> ActiveEnemies = new();

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>返回离 position 最近的存活敌人（无则 null）。跳过被销毁/禁用的引用。</summary>
        public static EnemyController FindNearest(Vector3 position)
        {
            EnemyController nearest = null;
            float minSqr = float.MaxValue;
            foreach (var e in ActiveEnemies)
            {
                // 防御：场景重载后残留的销毁引用，直接跳过
                if (e == null || !e.isActiveAndEnabled) continue;
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
        }

        public void Init(Transform player)
        {
            this.player = player;
        }

        private void OnEnable()
        {
            currentHealth = maxHealth;
            alive = true;
            ActiveEnemies.Add(this);
        }

        private void OnDisable()
        {
            alive = false;
            ActiveEnemies.Remove(this);
        }

        private float attackCooldown;
        private const float TouchDamageInterval = 1f;

        private void Update()
        {
            if (player == null || !alive) return;
            // 阶段门禁：非局内阶段敌人停止追击与接触伤害（死亡后世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            Vector3 dir = (player.position - transform.position).normalized;
            transform.Translate(dir * (moveSpeed * Time.deltaTime));

            // 距离检测：接触到玩家时施加伤害（带冷却），无需物理组件
            attackCooldown -= Time.deltaTime;
            if (attackCooldown <= 0f &&
                (player.position - transform.position).sqrMagnitude < 0.6f * 0.6f)
            {
                var playerCtrl = player.GetComponent<PlayerController>();
                if (playerCtrl != null)
                {
                    playerCtrl.TakeDamage(touchDamage);
                    attackCooldown = TouchDamageInterval;
                }
            }
        }

        public void TakeDamage(int damage)
        {
            if (!alive) return;
            currentHealth -= damage;
            if (currentHealth <= 0)
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
