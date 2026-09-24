using UnityEngine;

namespace Game
{
    /// <summary>
    /// 角色层配置（角色 = 兵种的「身体」维度）。
    /// 对应 H5 原型 <c>Docs/Design/h5/play/js/data.js</c> 的 <c>BODIES</c>。
    /// 兵种 = 角色 × 武器，最终属性由 <see cref="PlayerStatSystem"/> 组合派生，
    /// 不需要为 10 个兵种各写一份配置。
    /// </summary>
    [CreateAssetMenu(fileName = "Body_", menuName = "菌核狂潮/角色层 BodyDefinition")]
    public class BodyDefinition : ScriptableObject
    {
        [Header("标识")]
        public string bodyId = "man";
        public string displayName = "男性";

        [Header("基础属性")]
        public float baseHp = 120f;
        public float baseSpeed = 3.2f;
        [Tooltip("乘算在武器基础伤害上")]
        public float damageMultiplier = 1.1f;
        [Tooltip("除算攻击间隔（越大越快）")]
        public float attackRateMultiplier = 1f;

        [Header("体型")]
        [Tooltip("受击半径（世界单位）。与敌人半径相加后决定接触距离")]
        public float hitRadius = 0.42f;

        [Header("表现")]
        [Tooltip("基准条对应的世界高度（格）。与 CharacterSpriteSet.uniformScale 配套")]
        public float worldHeight = 1.75f;
        public Sprite portrait;
    }
}
