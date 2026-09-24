using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 角色表现（挂在实体根节点下的 <c>Visual</c> 子节点上）。
    /// 承担：帧动画、朝向旋转、水平翻转、垂直锚点、受击闪色。
    ///
    /// 数据来源是 <see cref="ISpriteProvider"/> —— 玩家兵种与敌人各实现一份，
    /// 本组件不关心加载的是哪一边。见该接口的注释说明为什么不用共用同一个 SO 类型。
    ///
    /// ★ 为什么是脚本逐帧，而不是 Animator + AnimationClip：
    ///   1) <c>unity-skills</c> 没有创建 AnimationClip 的技能（<c>animator_create_clip</c>
    ///      实测不存在），只能写编辑器脚本批量生成 clip
    ///   2) H5 原型本就是「帧号状态机」，逐帧可 1:1 复刻，各状态帧时长本来就不同
    ///   3) 同屏 80 敌人时 80 个 Animator 状态机开销明显
    ///   4) 打击帧本就该按归一化时间轮询，不必挂 Animation Event
    ///
    /// ★ 为什么旋转落在 Visual 而不是根节点：
    ///   根节点的移动用 <c>transform.Translate</c>（默认 <c>Space.Self</c>）。根一旦被
    ///   旋转，移动方向会被自身旋转**二次旋转** —— 表现为敌人绕圈、WASD 方向随瞄准
    ///   漂移。这个现象极易被误判成 AI 或输入 bug。
    /// </summary>
    public class CharacterView : MonoBehaviour, IController
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("目标朝向与精灵正面的角度补偿。素材正面朝 +Y，Unity 的 Y 轴向上，故取 +90°。\n" +
                 "H5 用的是 -90°（画布 Y 向下），符号相反，不能照抄 —— 必须用 4 方向截图矩阵实测确认。")]
        [SerializeField] private float aimRotationOffsetDeg = 90f;

        [Tooltip("受击闪色衰减速度（每秒）")]
        [SerializeField] private float flashDecay = 6f;

        private ISpriteProvider provider;
        /// <summary>持有 SpriteRenderer 的那个节点。offsetY 写在它上面（见 ApplyTransform）。</summary>
        private Transform spriteNode;

        private Sprite[] frames;
        private float frameDuration = 0.1f;
        private float clipOffsetY;

        private AnimState state = AnimState.Idle;
        private float timer;
        private int frameIndex;
        private bool looping = true;
        private bool playing;
        private bool finished;

        private float uniformScale = 1f;
        private bool flip;
        private float aimRadians = -Mathf.PI / 2f;
        private Color baseColor = Color.white;
        private float flash;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>当前动画状态。</summary>
        public AnimState State => state;

        /// <summary>当前帧号（自检与调试用）。</summary>
        public int FrameIndex => frameIndex;

        /// <summary>当前动画是否已播完（仅非循环动画会置位）。</summary>
        public bool IsFinished => finished;

        /// <summary>当前 clip 的归一化进度 0~1（打击帧判定与自检用）。</summary>
        public float NormalizedTime
        {
            get
            {
                if (frames == null || frames.Length == 0) return 0f;
                if (frames.Length == 1) return 1f;
                float per = Mathf.Max(0.0001f, frameDuration);
                float total = per * frames.Length;
                return Mathf.Clamp01((frameIndex * per + timer) / total);
            }
        }

        /// <summary>当前 clip 的垂直锚点（世界单位）。自检断言「idle 与 attack 脚底一致」用。</summary>
        public float OffsetY => clipOffsetY;

        /// <summary>当前朝向（弧度）。</summary>
        public float AimRadians => aimRadians;

        private void Awake()
        {
            // 先在自身找，再往下找子节点 —— 正确的预制体结构是 SpriteRenderer
            // 挂在 Visual 的子节点上（见 ApplyTransform 的注释）
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
                spriteNode = spriteRenderer.transform;
            }
        }

        // ================= 数据绑定 =================

        /// <summary>绑定动画数据源（兵种动画集或敌人配置）。绑定时立刻切到 Idle。</summary>
        public void Configure(ISpriteProvider source)
        {
            provider = source;
            uniformScale = source != null ? source.UniformScale : 1f;
            Play(AnimState.Idle, true);
        }

        // ================= 播放控制 =================

        /// <summary>
        /// 切换动画状态。<paramref name="restart"/> 为 true 时即使已是该状态也从头播
        /// （攻击连发时需要）。
        /// </summary>
        public void Play(AnimState next, bool restart = false)
        {
            if (!restart && state == next && playing) return;

            state = next;
            playing = provider != null
                      && provider.TryGetClip(next, out frames, out frameDuration, out clipOffsetY);
            if (!playing) { frames = null; clipOffsetY = 0f; }

            timer = 0f;
            frameIndex = 0;
            finished = false;
            // 只有 Idle / Walk 循环；Attack / Death 播完停在最后一帧
            looping = next == AnimState.Idle || next == AnimState.Walk;

            ApplyFrame();
            ApplyTransform();
        }

        /// <summary>完整复位（对象池复用时必须调 —— 约定要求禁止增量更新导致残影）。</summary>
        public void ResetView()
        {
            state = AnimState.Idle;
            frames = null;
            timer = 0f;
            frameIndex = 0;
            playing = false;
            finished = false;
            looping = true;
            flash = 0f;
            flip = false;
            aimRadians = -Mathf.PI / 2f;
            clipOffsetY = 0f;
            uniformScale = provider != null ? provider.UniformScale : 1f;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
                spriteRenderer.sprite = null;
            }
            ApplyTransform();
        }

        // ================= 表现参数 =================

        /// <summary>设置朝向（弧度）。俯视角精灵按朝向整体旋转。</summary>
        public void SetAim(float radians)
        {
            aimRadians = radians;
            flip = Mathf.Cos(radians) < 0f;
            ApplyTransform();
        }

        /// <summary>受击闪色（0~1）。会随时间自动衰减。</summary>
        public void Flash(float strength)
        {
            flash = Mathf.Clamp01(Mathf.Max(flash, strength));
        }

        // ================= 每帧 =================

        private void Update()
        {
            // 阶段门禁：开始界面与死亡后世界冻结（这是全工程统一的冻结机制）。
            // 例外：死亡动画在 GameOver 阶段允许播完，否则死亡瞬间动画会僵在半路。
            var phase = this.GetModel<IGameStateModel>().Phase.Value;
            bool allow = phase == GamePhase.Playing ||
                         (phase == GamePhase.GameOver && state == AnimState.Death);
            if (!allow) return;

            Advance(Time.deltaTime);

            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - flashDecay * Time.deltaTime);
                if (spriteRenderer != null)
                    spriteRenderer.color = Color.Lerp(baseColor, Color.white, flash);
            }
        }

        /// <summary>推进动画。<paramref name="deltaTime"/> 由调用方给，便于自检用固定步长驱动。</summary>
        public void Advance(float deltaTime)
        {
            if (!playing || frames == null || frames.Length == 0) return;

            int count = frames.Length;
            if (count == 1) { finished = !looping; return; }

            float per = Mathf.Max(0.0001f, frameDuration);
            timer += deltaTime;
            while (timer >= per)
            {
                timer -= per;
                frameIndex++;
                if (frameIndex >= count)
                {
                    if (looping) frameIndex = 0;
                    else { frameIndex = count - 1; playing = false; finished = true; break; }
                }
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0) return;
            spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }

        /// <summary>
        /// 把缩放 / 旋转 / 垂直锚点写到 Transform 上。
        ///
        /// ★ 锚点必须落在**旋转之内**：offsetY 要写在持有 SpriteRenderer 的那个子节点上，
        ///   不能写成 Visual 自己的 localPosition。
        ///
        ///   原因：`localPosition` 是**父空间**坐标，不受自身 `localRotation` 影响 ——
        ///   写成 Visual.localPosition 的话，补偿量永远朝世界里固定的「上」，
        ///   而帧内容的偏移是跟着角色朝向转的。攻击帧比待机帧高（手枪 204px vs 135px），
        ///   补偿量约 0.45 格，于是**角色一转朝向身体中心就偏出去近半格**，
        ///   表现就是「攻击时向后/侧向位移」。H5 原型是对的（偏移写在 rotate 之后），
        ///   这是移植到 Unity 时踩的坑。
        ///
        ///   正确结构：Visual（旋转/翻转）→ Sprite（SpriteRenderer，承载 offsetY）
        /// </summary>
        private void ApplyTransform()
        {
            transform.localScale = new Vector3(flip ? -uniformScale : uniformScale, uniformScale, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, aimRadians * Mathf.Rad2Deg + aimRotationOffsetDeg);

            Vector3 offset = new Vector3(0f, clipOffsetY, 0f);
            if (spriteNode != null && spriteNode != transform)
            {
                // 正确路径：偏移写在子节点上，随 Visual 一起旋转
                spriteNode.localPosition = offset;
            }
            else
            {
                // 退路：SpriteRenderer 与 CharacterView 同节点（旧结构）。
                // 锚点会随朝向失准，仅为不破坏既有预制体而保留。
                transform.localPosition = offset;
            }
        }
    }
}
