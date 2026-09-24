using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 刷怪器（表现层 MonoBehaviour，IController）：在玩家视野外沿生成敌人。
    ///
    /// ★ 为什么是「1 个敌人预制体 + 11 份 EnemyDefinition」而不是 11 个预制体：
    ///   11 个预制体只是把同一套结构（根 + Visual + 血条）复制 11 遍，彼此只有
    ///   `definition` 字段不同。用一份预制体 + SO 注入，加一种敌人只需加一个 SO。
    ///   敌人本身仍然是**预制体实例**（不是代码搭建的），符合工程的 UI/实体约定。
    ///
    /// 刷新间隔的唯一来源仍是 <see cref="IDifficultySystem.GetSpawnInterval"/>，
    /// 本类不得自备公式 —— 那是工程里明确的「难度单一来源」约定。
    /// 波次构成表（哪种敌人、多少只）是迭代 3 的事，本轮先按权重随机挑。
    /// </summary>
    public class EnemySpawner : MonoBehaviour, IController
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform player;

        [Tooltip("可刷的敌人配置。迭代 3 会换成波次表驱动")]
        [SerializeField] private EnemyDefinition[] enemyPool = new EnemyDefinition[0];

        [Tooltip("每个敌人的抽取权重，与 enemyPool 一一对应；留空则等概率")]
        [SerializeField] private int[] enemyWeights = new int[0];

        private float timer;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        public void ResetForNewGame()
        {
            timer = 0f;
        }

        private void Update()
        {
            if (enemyPrefab == null || player == null) return;
            // 阶段门禁：非局内阶段停止刷怪（死亡后世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            float interval = this.GetSystem<IDifficultySystem>().GetSpawnInterval();
            timer += Time.deltaTime;
            if (timer < interval) return;

            timer = 0f;
            SpawnEnemy();
        }

        private void SpawnEnemy()
        {
            var def = PickDefinition();
            if (def == null) return;

            Vector3 position = SpawnPosition();

            var pool = this.GetUtility<IPoolUtility>();
            GameObject enemy = pool.Spawn(enemyPrefab, position, Quaternion.identity);
            if (enemy == null) return;

            enemy.GetComponent<EnemyController>()?.Init(def, player);
        }

        /// <summary>
        /// 沿视野矩形四条边、在外侧一点生成。
        ///
        /// 早先 H5 用「视野对角线的圆」生成，结果上下方向刷出的怪离玩家过远，
        /// 以敌人速度要走好几秒才进画面，开局十几秒场上空荡荡。改成贴边生成。
        /// </summary>
        private Vector3 SpawnPosition()
        {
            var cfg = Object.FindObjectOfType<GameConfigHolder>()?.Config;
            float pad = cfg != null ? cfg.spawnRingPadding : 1.4f;

            // 视野尺寸（世界单位）：需与相机设置一致。正交 size 5 → 视野高 10。
            var cam = Camera.main;
            float halfH = cam != null ? cam.orthographicSize : 5f;
            float halfW = halfH * (Screen.width > 0 ? (float)Screen.width / Screen.height : 16f / 9f);

            int side = Random.Range(0, 4);
            float x, y;
            switch (side)
            {
                case 0: x = Random.Range(-halfW, halfW); y = halfH + pad; break;   // 上
                case 1: x = Random.Range(-halfW, halfW); y = -halfH - pad; break;  // 下
                case 2: x = -halfW - pad; y = Random.Range(-halfH, halfH); break;  // 左
                default: x = halfW + pad; y = Random.Range(-halfH, halfH); break; // 右
            }

            var p = player.position;
            return new Vector3(p.x + x, p.y + y, 0f);
        }

        /// <summary>按权重从池里挑一种敌人。</summary>
        private EnemyDefinition PickDefinition()
        {
            if (enemyPool == null || enemyPool.Length == 0) return null;

            int total = 0;
            for (int i = 0; i < enemyPool.Length; i++)
            {
                if (enemyPool[i] == null) continue;
                total += WeightAt(i);
            }
            if (total <= 0) return null;

            int roll = Random.Range(0, total);
            for (int i = 0; i < enemyPool.Length; i++)
            {
                if (enemyPool[i] == null) continue;
                roll -= WeightAt(i);
                if (roll < 0) return enemyPool[i];
            }
            return enemyPool[enemyPool.Length - 1];
        }

        private int WeightAt(int i)
        {
            if (enemyWeights == null || i >= enemyWeights.Length) return 1;
            return Mathf.Max(1, enemyWeights[i]);
        }
    }
}
