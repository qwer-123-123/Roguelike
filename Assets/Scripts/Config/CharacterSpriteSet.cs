using UnityEngine;

namespace Game
{
    /// <summary>角色动画的四个状态。与 H5 的 idle / walk / atk / death 一一对应。</summary>
    public enum AnimState
    {
        Idle,
        Walk,
        Attack,
        Death,
    }

    /// <summary>
    /// 一个兵种的全部表现资源（10 个兵种 = 10 个本资产）。
    /// 由 <c>Docs/Design/_tools/split-frames.ps1</c> 产出的 Manifest.csv 提供
    /// <c>uniformScale</c> 与各 clip 的 <c>offsetY</c>，不要手算。
    ///
    /// ★ 锚点约定（这是 H5 commit 851b49b「攻击时人物缩小」那条修复的 Unity 版）：
    ///   - <c>uniformScale</c> 对**同一角色的所有动画必须相同** —— 否则原始帧更高的
    ///     攻击动画会被压小，表现为「攻击时人物缩小」。
    ///   - <c>offsetY</c> 逐 clip 不同，用来把脚底钉在同一高度：帧越高，
    ///     偏移越大，角色只是向上延伸、脚不动。
    /// </summary>
    [CreateAssetMenu(fileName = "Sprites_", menuName = "菌核狂潮/角色动画集 CharacterSpriteSet")]
    public class CharacterSpriteSet : ScriptableObject, ISpriteProvider
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

        [Header("归属")]
        public BodyDefinition body;
        public WeaponDefinition weapon;

        [Header("表现")]
        [Tooltip("落在 Visual.localScale。同一角色的所有动画共用同一个值")]
        public float uniformScale = 1f;

        public Clip idle = new Clip();
        public Clip walk = new Clip();
        public Clip attack = new Clip();
        public Clip death = new Clip();

        /// <summary><see cref="ISpriteProvider"/> 实现。</summary>
        public float UniformScale => uniformScale;

        public bool TryGetClip(AnimState state, out Sprite[] frames, out float frameDuration, out float offsetY)
        {
            var c = GetClip(state);
            frames = c != null ? c.frames : null;
            frameDuration = c != null ? c.frameDuration : 0.1f;
            offsetY = c != null ? c.offsetY : 0f;
            return c != null && c.IsValid;
        }

        /// <summary>取某个状态的动画数据；未配置时返回 null。</summary>
        public Clip GetClip(AnimState state)
        {
            switch (state)
            {
                case AnimState.Idle:   return idle;
                case AnimState.Walk:   return walk;
                case AnimState.Attack: return attack;
                case AnimState.Death:  return death;
                default:               return null;
            }
        }

        /// <summary>
        /// 自查：攻击帧数必须与武器的 atkFrames 一致。
        /// 不一致时 <see cref="PlayerStatSystem"/> 的打击帧换算会失准（伤害在错误的时机结算）。
        /// 供编辑器工具与自检调用，不在运行时热路径。
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;
            if (weapon == null) { error = "未绑定 WeaponDefinition"; return false; }
            if (!idle.IsValid)   { error = "idle 没有帧"; return false; }
            if (!walk.IsValid)   { error = "walk 没有帧"; return false; }
            if (!attack.IsValid) { error = "attack 没有帧"; return false; }
            if (attack.frames.Length != weapon.atkFrames)
            {
                error = $"attack 帧数 {attack.frames.Length} 与 weapon.atkFrames {weapon.atkFrames} 不一致";
                return false;
            }
            return true;
        }
    }
}
