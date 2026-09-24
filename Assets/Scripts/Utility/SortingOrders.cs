namespace Game
{
    /// <summary>
    /// 世界里所有 SpriteRenderer 的排序值。
    ///
    /// 为什么用 <c>sortingOrder</c> 而不是新建 SortingLayer：
    /// 本工程的 <c>TagManager.asset</c> 只有一条 <c>Default</c> 层，而
    /// <c>unity-skills</c> 没有创建 SortingLayer 的技能，只能手改 TagManager。
    /// 手改的风险不对称 —— <c>sortingLayerName</c> 指向不存在的层时 Unity 会
    /// **静默回落到 Default、不报错**，是典型的「只有肉眼能发现」的失败模式；
    /// 而 <c>sortingOrder</c> 永远合法。
    ///
    /// 顺序照搬 H5 原型 <c>game.js</c> 的绘制顺序（纯画家序），并留出间距给未来插入。
    /// 若将来要做 Y 轴排序，用
    /// <c>GraphicsSettings.transparencySortMode = CustomAxis(0,1,0)</c> 一行解决，
    /// **不要**手写 <c>sortingOrder = -y * 100</c>，那会毁掉这张表。
    /// </summary>
    public static class SortingOrders
    {
        /// <summary>地面烘焙图（迭代 4）。</summary>
        public const int Ground = -1000;

        /// <summary>地面贴花 / 瓦片。</summary>
        public const int GroundDetail = -800;

        /// <summary>实体脚下的阴影。</summary>
        public const int Shadow = -500;

        /// <summary>掉落物（经验球 / 道具）。</summary>
        public const int Pickup = 0;

        /// <summary>特效（命中火花 / 爆炸 / 火焰）。</summary>
        public const int Effect = 100;

        /// <summary>敌人本体。</summary>
        public const int Enemy = 200;

        /// <summary>敌人血条。</summary>
        public const int EnemyHealthBar = 230;

        /// <summary>投射物。</summary>
        public const int Projectile = 300;

        /// <summary>玩家本体。</summary>
        public const int Player = 320;

        /// <summary>挥砍弧 / 锥形火焰等贴地表现。</summary>
        public const int AttackVfx = 400;

        /// <summary>世界空间飘字。</summary>
        public const int DamagePopup = 900;

        /// <summary>BOSS 屏幕外指示箭头（迭代 3）。</summary>
        public const int BossIndicator = 1000;
    }
}
