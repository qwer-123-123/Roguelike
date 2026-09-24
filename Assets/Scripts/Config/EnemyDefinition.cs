using UnityEngine;

namespace Game
{
    /// <summary>敌人分级。与 H5 原型 <c>data.js</c> 的 kind 字段一致。</summary>
    public enum EnemyKind
    {
        /// <summary>1 级杂兵：堆密度用，单个无威胁。</summary>
        Minion,
        /// <summary>2 级精英：首次出现速度反超杂兵的敌人，逼玩家走位。</summary>
        Elite,
        /// <summary>3 级重装：高血量低速，移动路障。</summary>
        Heavy,
        /// <summary>4 级 BOSS：双招式循环。</summary>
        Boss,
        /// <summary>5 级终局：多套攻击 + 手臂再生。</summary>
        Final,
    }

    /// <summary>
    /// 敌人配置（11 种：1~5 级含 3 只 BOSS）。
    /// 对应 H5 原型 <c>data.js</c> 的 <c>ENEMIES</c>，数值原样搬运。
    ///
    /// 把数值从预制体的 [SerializeField] 挪到这里，是为了消掉工程遗留项
    /// 「数值硬编码在预制体上」——加一种敌人只需加一个本资产 + 一个预制体。
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_", menuName = "菌核狂潮/敌人 EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject, ISpriteProvider
    {
        [System.Serializable]
        public class Clip
        {
            public Sprite[] frames;
            [Tooltip("每帧持续秒数")]
            public float frameDuration = 0.1f;
            [Tooltip("相对根节点的垂直偏移（世界单位）。帧越高该值越大，保证脚底位置一致")]
            public float offsetY = 0f;

            public bool IsValid => frames != null && frames.Length > 0;
        }

        [Header("标识")]
        public string enemyId = "z1";
        [Tooltip("ASCII 占位名（工程尚无中文字体）")]
        public string displayName = "ZOMBIE A";
        [Range(1, 5)] public int level = 1;
        public EnemyKind kind = EnemyKind.Minion;

        [Header("数值")]
        public float maxHp = 30f;
        public float moveSpeed = 1.6f;
        public float touchDamage = 6f;
        [Tooltip("接触伤害的间隔秒数")]
        public float contactInterval = 1f;
        public int expReward = 5;
        public int moneyReward = 2;

        [Header("体型")]
        [Tooltip("受击半径（世界单位）。与玩家半径相加后决定接触距离")]
        public float hitRadius = 0.34f;
        [Tooltip("walk 基准条对应的世界高度（格）")]
        public float worldHeight = 1.35f;

        [Header("表现")]
        [Tooltip("落在 Visual.localScale。该敌人的所有动画共用同一个值")]
        public float uniformScale = 1f;
        [Tooltip("攻击动画的打击帧位置（归一化 0~1），动画播到这里才结算接触伤害")]
        [Range(0f, 1f)] public float hitAt = 0.35f;

        public Clip walk = new Clip();
        public Clip attack = new Clip();
        public Clip death = new Clip();

        [Header("波次（迭代 3 用）")]
        [Tooltip("击杀本敌折算成本波进度多少「只」。杂兵为 1，BOSS 为 12")]
        public int waveWorth = 1;
        public bool IsBoss => kind == EnemyKind.Boss || kind == EnemyKind.Final;

        /// <summary>
        /// <see cref="ISpriteProvider"/> 实现。敌人没有 idle（始终在走），Idle 复用 walk。
        /// </summary>
        public float UniformScale => uniformScale;

        public bool TryGetClip(AnimState state, out Sprite[] frames, out float frameDuration, out float offsetY)
        {
            Clip c;
            switch (state)
            {
                case AnimState.Attack: c = attack; break;
                case AnimState.Death:  c = death;  break;
                // Walk 与 Idle 都走 walk（敌人没有待机动作）
                default:               c = walk;   break;
            }

            frames = c != null ? c.frames : null;
            frameDuration = c != null ? c.frameDuration : 0.1f;
            offsetY = c != null ? c.offsetY : 0f;
            return c != null && c.IsValid;
        }

        /// <summary>自查：三套动画都要有帧，否则对象池复用时会出现空白。</summary>
        public bool Validate(out string error)
        {
            error = null;
            if (!walk.IsValid)   { error = "walk 没有帧"; return false; }
            if (!attack.IsValid) { error = "attack 没有帧"; return false; }
            if (!death.IsValid)  { error = "death 没有帧"; return false; }
            if (maxHp <= 0f)     { error = "maxHp 必须为正"; return false; }
            if (hitRadius <= 0f) { error = "hitRadius 必须为正"; return false; }
            return true;
        }
    }
}
