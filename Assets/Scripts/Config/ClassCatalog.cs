using UnityEngine;

namespace Game
{
    /// <summary>
    /// 兵种目录：10 个兵种 = 角色(2) × 武器(5) 的笛卡尔积。
    /// 刻意**不为 10 个兵种各写一份配置** —— 组合派生即可，加一个角色或一把武器
    /// 只需往对应数组里加一项。
    ///
    /// 索引约定（全工程统一，改动会影响存档与命令参数）：
    ///     index = bodyIndex * weapons.Length + weaponIndex
    ///     即「角色为主序、武器为次序」，与 H5 的 CLASSES 构造顺序一致。
    /// </summary>
    [CreateAssetMenu(fileName = "ClassCatalog", menuName = "菌核狂潮/兵种目录 ClassCatalog")]
    public class ClassCatalog : ScriptableObject
    {
        [Tooltip("角色数组，顺序即兵种索引的主序")]
        public BodyDefinition[] bodies = new BodyDefinition[0];

        [Tooltip("武器数组，顺序即兵种索引的次序")]
        public WeaponDefinition[] weapons = new WeaponDefinition[0];

        [Tooltip("按 index = bodyIndex * weapons.Length + weaponIndex 展平的动画集数组")]
        public CharacterSpriteSet[] spriteSets = new CharacterSpriteSet[0];

        /// <summary>兵种总数。</summary>
        public int Count => bodies.Length * weapons.Length;

        public BodyDefinition BodyAt(int index)
        {
            if (weapons.Length == 0) return null;
            return bodies[index / weapons.Length];
        }

        public WeaponDefinition WeaponAt(int index)
        {
            if (weapons.Length == 0) return null;
            return weapons[index % weapons.Length];
        }

        public CharacterSpriteSet SpritesAt(int index)
        {
            if (index < 0 || index >= spriteSets.Length) return null;
            return spriteSets[index];
        }

        /// <summary>兵种显示名，如「男性 · 手枪」。</summary>
        public string DisplayNameAt(int index)
        {
            var b = BodyAt(index);
            var w = WeaponAt(index);
            if (b == null || w == null) return "?";
            return b.displayName + " · " + w.displayName;
        }

        /// <summary>
        /// 自查：数组长度与每一格的归属是否对得上。
        /// 展平数组最容易出的错是「顺序摆错」，而那会让兵种选到别人的动画。
        /// 供编辑器工具与自检调用，不在运行时热路径。
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;
            if (bodies.Length == 0)  { error = "bodies 为空"; return false; }
            if (weapons.Length == 0) { error = "weapons 为空"; return false; }

            int expected = Count;
            if (spriteSets.Length != expected)
            {
                error = $"spriteSets 长度 {spriteSets.Length}，应为 {bodies.Length} × {weapons.Length} = {expected}";
                return false;
            }

            for (int i = 0; i < expected; i++)
            {
                var set = spriteSets[i];
                if (set == null) { error = $"spriteSets[{i}] 为空（应为 {DisplayNameAt(i)}）"; return false; }
                if (set.body != BodyAt(i))
                {
                    error = $"spriteSets[{i}].body 应为 {BodyAt(i).displayName}，实际是 {(set.body == null ? "空" : set.body.displayName)}";
                    return false;
                }
                if (set.weapon != WeaponAt(i))
                {
                    error = $"spriteSets[{i}].weapon 应为 {WeaponAt(i).displayName}，实际是 {(set.weapon == null ? "空" : set.weapon.displayName)}";
                    return false;
                }
            }
            return true;
        }
    }
}
