using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 玩家自动攻击（表现层 MonoBehaviour，IController）：自动瞄准最近敌人发射投射物。
    /// 持有当前伤害/攻速，供升级系统增强。
    /// </summary>
    public class PlayerCombatController : MonoBehaviour, IController
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float fireInterval = 0.8f;
        [SerializeField] private int damage = 1;

        private float timer;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>重开一局：伤害、攻速恢复默认，射击计时清零。</summary>
        public void ResetForNewGame()
        {
            damage = 1;
            fireInterval = 0.8f;
            timer = 0f;
        }

        private void Update()
        {
            if (projectilePrefab == null) return;
            // 阶段门禁：开始界面未开始、或玩家已死亡时不发射（与其余玩法逻辑一致）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            timer += Time.deltaTime;
            if (timer < fireInterval) return;

            var target = EnemyController.FindNearest(transform.position);
            if (target == null) return;

            timer = 0f;
            Fire(target);
        }

        private void Fire(EnemyController target)
        {
            var pool = this.GetUtility<IPoolUtility>();
            GameObject projectile = pool.Spawn(projectilePrefab, transform.position, Quaternion.identity);
            projectile.GetComponent<ProjectileController>()?.Init(target, damage);
        }

        // 升级接口
        public void AddDamage(int amount) => damage += amount;
        public void IncreaseFireRate(float percent) => fireInterval = Mathf.Max(0.1f, fireInterval * (1f - percent));
    }
}
