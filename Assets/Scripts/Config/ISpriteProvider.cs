using UnityEngine;

namespace Game
{
    /// <summary>
    /// 「能按动画状态提供帧序列」的东西 —— 玩家兵种（<see cref="CharacterSpriteSet"/>）
    /// 与敌人（<see cref="EnemyDefinition"/>）都实现它，于是 <see cref="CharacterView"/>
    /// 只认这一个接口，两边共用同一套表现逻辑。
    ///
    /// 抽这一层的动机：两者的动画数据结构完全相同（帧数组 + 帧时长 + 锚点 + 缩放），
    /// 但语义不同（兵种有 body/weapon，敌人有等级/血量）。若让敌人直接复用
    /// CharacterSpriteSet，它的 <c>Validate()</c> 会因为 weapon 为空而误报；
    /// 若各自写一套表现组件，又会把帧推进逻辑复制两份。
    /// </summary>
    public interface ISpriteProvider
    {
        /// <summary>落在 Visual.localScale。同一角色的所有动画共用同一个值。</summary>
        float UniformScale { get; }

        /// <summary>取某个状态的动画；没有该状态时返回 false。</summary>
        bool TryGetClip(AnimState state, out Sprite[] frames, out float frameDuration, out float offsetY);
    }
}
