using UnityEngine;

namespace Game
{
    /// <summary>
    /// 全局玩法参数。把原先散在各 Controller 里的硬编码常量集中到一处
    /// （对应工程约定「禁止硬编码魔法数」与遗留项「数值硬编码在预制体 SerializeField 上」）。
    ///
    /// 这些值原本都写死在代码里，改动前请先确认理由：
    ///   - <c>contactPadding</c>：原 <c>EnemyController</c> 用固定的 <c>0.6 × 0.6</c>
    ///     距离平方判定，那是照 Cube 的 1×1 视觉调的。改成「敌半径 + 我方半径 + 该常数」
    ///     之后，接触距离会随兵种与敌人等级变化（BOSS 会明显更早开始打人）——这是
    ///     引入兵种系统的必然结果，不是 bug。
    ///   - <c>enemySeparation</c>：H5 初版给 0.55，实测敌人全叠在玩家身上糊成一团，
    ///     调到 3.2 才散得开。
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "菌核狂潮/全局参数 GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("场地")]
        [Tooltip("竞技场半宽（格）。相机视野高 10 格，场地应显著大于视野")]
        public float arenaHalfWidth = 28f;
        [Tooltip("竞技场半高（格）")]
        public float arenaHalfHeight = 28f;
        [Tooltip("刷怪点在视野矩形外再外扩多少格")]
        public float spawnRingPadding = 1.4f;

        [Header("敌人")]
        [Tooltip("分离力强度。避免敌人叠成一坨")]
        public float enemySeparation = 3.2f;
        [Tooltip("距离小于「两者半径和 × 该系数」就开始互推")]
        public float enemySeparationRadiusFactor = 1.18f;
        [Tooltip("接触判定的额外余量（格）：实际接触距离 = 敌半径 + 玩家半径 + 该值")]
        public float contactPadding = 0.32f;

        [Header("经验曲线")]
        [Tooltip("升级所需经验 = expBase + level × expPerLevel + level² × expQuadratic")]
        public float expBase = 12f;
        public float expPerLevel = 7f;
        public float expQuadratic = 1.05f;

        [Header("拾取")]
        [Tooltip("经验球的吸附半径（格）")]
        public float pickupRadius = 0.9f;

        [Header("确定性")]
        [Tooltip("逻辑帧率。策划案要求核心战斗必须确定性计算，故用固定步长驱动而非 deltaTime")]
        public int tickHz = 60;

        /// <summary>升到 level+1 所需经验（level 从 1 起）。</summary>
        public int ExpNeeded(int level)
        {
            return Mathf.RoundToInt(expBase + level * expPerLevel + level * level * expQuadratic);
        }
    }
}
