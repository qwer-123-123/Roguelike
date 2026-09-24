using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 重建敌人预制体（编辑器工具，可重跑）：把内置 Cube 图元换成 Sprite + 血条。
    ///
    /// **原地覆盖** <c>Assets/Prefabs/Enemy.prefab</c> —— <c>SaveAsPrefabAsset</c> 会保留
    /// 原 `.meta` 的 GUID，所以场景里 `EnemySpawner.enemyPrefab` 的引用不会断。
    /// 若改成删除后新建，GUID 会变、引用全断。
    ///
    /// 只做一份预制体：11 种敌人的差异全在 `EnemyDefinition` 里，由刷怪器注入。
    ///
    /// 用法：Unity 菜单 Tools ▸ Game ▸ Build Enemy Prefab
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Enemy.prefab";
        private const string ExpDropPath = "Assets/Prefabs/ExpDrop.prefab";
        private const string WhiteSpritePath = "Assets/Art/Utility/white_1x1.png";

        [MenuItem("Tools/Game/Build Enemy Prefab")]
        public static void Build()
        {
            // 先用一个临时对象搭层级，再覆盖保存
            var root = new GameObject("Enemy", typeof(Transform));

            // ---- Visual（只负责旋转/翻转）→ Sprite（持有 SpriteRenderer，承载 offsetY）----
            // 分两层是必须的：offsetY 要随 Visual 的旋转一起转。
            // 若把 SpriteRenderer 与 CharacterView 放同一节点，localPosition 在父空间、
            // 不随旋转走，攻击时（攻击帧比待机帧高）会看出明显位移。
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(visualGo.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrders.Enemy;

            var view = visualGo.AddComponent<CharacterView>();
            SetRef(view, "spriteRenderer", sr);

            // ---- 血条：两张 PPU=1 的纯色 sprite，靠 localScale 定尺寸 ----
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (white == null)
            {
                Debug.LogError($"[菌核狂潮] 找不到 {WhiteSpritePath} —— 先跑工具生成纯色 sprite");
            }

            var bgGo = new GameObject("HpBarBg");
            bgGo.transform.SetParent(root.transform, false);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = white;
            bg.color = new Color(0.06f, 0.07f, 0.09f, 0.85f);
            bg.sortingOrder = SortingOrders.EnemyHealthBar;

            var fillGo = new GameObject("HpBarFill");
            fillGo.transform.SetParent(root.transform, false);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = white;
            fill.color = new Color(0.66f, 0.76f, 0.24f, 1f);
            fill.sortingOrder = SortingOrders.EnemyHealthBar + 1;

            // ---- 控制器 ----
            var ctrl = root.AddComponent<EnemyController>();
            SetRef(ctrl, "view", view);
            SetRef(ctrl, "hpBarBg", bg);
            SetRef(ctrl, "hpBarFill", fill);
            SetRef(ctrl, "expDropPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ExpDropPath));

            // ---- 保存（原地覆盖，保留 GUID）----
            EnsureFolder("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[菌核狂潮] 敌人预制体已重建：{PrefabPath}（Cube → Sprite + 血条）");
        }

        /// <summary>
        /// 重建经验掉落物预制体：同样是 Cube → Sprite。
        /// 不换掉的话，敌人被击杀处会留一坨白色方块（尤其是玩家不移动时越积越多）。
        /// </summary>
        [MenuItem("Tools/Game/Build ExpDrop Prefab")]
        public static void BuildExpDrop()
        {
            const string spritePath = "Assets/Art/UI/Elements/element_0045_Layer-47.png";

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogError($"[菌核狂潮] 找不到经验球精灵 {spritePath}");
                return;
            }

            var root = new GameObject("ExpDrop", typeof(Transform));
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrders.Pickup;

            var ctrl = root.AddComponent<ExperienceDropController>();
            // 掉落物是通用尺寸，不随敌人变化：世界里约 0.3 格
            root.transform.localScale = Vector3.one * 0.3f;

            PrefabUtility.SaveAsPrefabAsset(root, ExpDropPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _ = ctrl;
            Debug.Log($"[菌核狂潮] 经验掉落物预制体已重建：{ExpDropPath}（Cube → Sprite）");
        }

        private static void SetRef(Component target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[菌核狂潮] 找不到字段 {target.GetType().Name}.{field}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
