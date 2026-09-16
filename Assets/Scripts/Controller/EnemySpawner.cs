using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 刷怪器（表现层 MonoBehaviour，IController）：按间隔在玩家周围刷怪。
    /// 通过 IPoolUtility 复用敌人对象。
    /// 刷怪间隔由 <see cref="IDifficultySystem"/> 统一计算，本类不自带难度公式。
    /// </summary>
    public class EnemySpawner : MonoBehaviour, IController
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private float spawnRadius = 8f;

        private float timer;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>重开一局：刷怪计时清零。</summary>
        public void ResetForNewGame()
        {
            timer = 0f;
        }

        private void Update()
        {
            if (enemyPrefab == null || player == null) return;
            // 阶段门禁：开始界面与死亡后都不刷怪
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            float interval = this.GetSystem<IDifficultySystem>().GetSpawnInterval();
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;

            SpawnEnemy();
        }

        private void SpawnEnemy()
        {
            var pool = this.GetUtility<IPoolUtility>();
            Vector2 offset = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 position = player.position + new Vector3(offset.x, offset.y, 0f);

            GameObject enemy = pool.Spawn(enemyPrefab, position, Quaternion.identity);
            var controller = enemy.GetComponent<EnemyController>();
            controller?.Init(player);
        }
    }
}
