using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 场景接线工具（编辑器，可重跑）。
    ///
    /// 为什么需要它：<c>unity-skills</c> 的 <c>component_set_property</c> 只能解析
    /// **场景对象**引用（<c>referencePath</c>），项目资产引用传 <c>assetPath</c> 会被判为
    /// 只读属性；<c>component_set_serialized_property</c> 的 <c>value</c> 又不被当作资产路径。
    /// 与其绕各种参数，不如用 <see cref="SerializedObject"/> 直接写私有序列化字段 ——
    /// 这是 Unity 官方推荐的编辑器期赋值方式，且本脚本可重跑。
    ///
    /// 用法：Unity 菜单 Tools ▸ Game ▸ Wire Scene
    /// </summary>
    public static class SceneWirer
    {
        private const string CatalogPath = "Assets/Configs/ClassCatalog.asset";
        private const string ConfigPath = "Assets/Configs/GameConfig.asset";
        private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        [MenuItem("Tools/Game/Wire Scene")]
        public static void WireScene()
        {
            var log = new System.Text.StringBuilder("[菌核狂潮] 场景接线\n");

            // ---- 1. 配置挂载点 ----
            var holder = Object.FindObjectOfType<GameConfigHolder>();
            if (holder == null)
            {
                var go = new GameObject("GameConfig");
                holder = go.AddComponent<GameConfigHolder>();
                log.AppendLine("  新建 GameConfig 对象并挂上 GameConfigHolder");
            }

            SetAssetRef(holder, "catalog", AssetDatabase.LoadAssetAtPath<ClassCatalog>(CatalogPath), log);
            SetAssetRef(holder, "config", AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath), log);

            // ---- 2. 玩家 ----
            var playerGo = GameObject.Find("Player");
            if (playerGo == null)
            {
                Debug.LogError("[菌核狂潮] 场景里找不到 Player");
                return;
            }

            var view = EnsureVisualNode(playerGo, log);
            WireRef(playerGo.GetComponent<PlayerController>(), "view", view, log);
            WireRef(playerGo.GetComponent<PlayerCombatController>(), "player", playerGo.GetComponent<PlayerController>(), log);
            WireRef(playerGo.GetComponent<PlayerCombatController>(), "projectilePrefab",
                    AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath), log);

            // ---- 3. 刷怪器的敌人池 ----
            WireSpawnerPool(log);

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(holder);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 把 11 种敌人配置装进刷怪池。
        /// **BOSS 不放进随机池** —— 它们由波次触发（迭代 3），随机刷出来会破坏节奏。
        /// 权重按 H5 早期波次的敌人构成比例：杂兵多、精英中、重装少。
        /// </summary>
        private static void WireSpawnerPool(System.Text.StringBuilder log)
        {
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[菌核狂潮] 场景里找不到 EnemySpawner，跳过敌人池接线");
                return;
            }

            // 权重表：与 EnemyTable 的 id 对应
            var weights = new System.Collections.Generic.Dictionary<string, int>
            {
                { "z1", 10 }, { "z2", 10 }, { "z3", 10 }, { "z4", 10 },
                { "z5", 5 },  { "z6", 5 },
                { "z7", 2 },  { "z8", 2 },
            };

            var pool = new System.Collections.Generic.List<EnemyDefinition>();
            var weightList = new System.Collections.Generic.List<int>();
            foreach (var kv in weights)
            {
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"Assets/Configs/Enemies/Enemy_{kv.Key}.asset");
                if (def == null)
                {
                    Debug.LogWarning($"[菌核狂潮] 缺少敌人配置 Enemy_{kv.Key}，跳过");
                    continue;
                }
                pool.Add(def);
                weightList.Add(kv.Value);
            }

            var so = new SerializedObject(spawner);
            var poolProp = so.FindProperty("enemyPool");
            var weightProp = so.FindProperty("enemyWeights");
            if (poolProp == null || weightProp == null)
            {
                Debug.LogWarning("[菌核狂潮] EnemySpawner 上找不到 enemyPool / enemyWeights 字段");
                return;
            }

            poolProp.arraySize = pool.Count;
            weightProp.arraySize = weightList.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                poolProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
                weightProp.GetArrayElementAtIndex(i).intValue = weightList[i];
            }
            so.ApplyModifiedProperties();

            log.AppendLine($"  EnemySpawner.enemyPool ← {pool.Count} 种敌人（BOSS 除外，由波次触发）");
        }

        /// <summary>确保根节点下有一个 Visual 子节点，挂着 SpriteRenderer + CharacterView。</summary>
        private static CharacterView EnsureVisualNode(GameObject root, System.Text.StringBuilder log)
        {
            var visualGo = root.transform.Find("Visual")?.gameObject;
            if (visualGo == null)
            {
                visualGo = new GameObject("Visual");
                visualGo.transform.SetParent(root.transform, false);
                log.AppendLine("  新建 Player/Visual");
            }

            var sr = visualGo.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = visualGo.AddComponent<SpriteRenderer>();
                log.AppendLine("  挂上 SpriteRenderer");
            }

            // 每次都强制设一遍排序值：只在新建时设的话，若节点已存在就永远不生效
            // （本轮就踩过 —— 先建了节点、后来用 SceneWirer 补接线，结果 sortingOrder 停在 0）
            if (sr.sortingOrder != SortingOrders.Player)
            {
                sr.sortingOrder = SortingOrders.Player;
                EditorUtility.SetDirty(sr);
            }
            log.AppendLine($"  SpriteRenderer.sortingOrder = {SortingOrders.Player}");

            var view = visualGo.GetComponent<CharacterView>();
            if (view == null)
            {
                view = visualGo.AddComponent<CharacterView>();
                log.AppendLine("  挂上 CharacterView");
            }

            var so = new SerializedObject(view);
            var prop = so.FindProperty("spriteRenderer");
            if (prop != null && prop.objectReferenceValue != sr)
            {
                prop.objectReferenceValue = sr;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(view);
            }

            return view;
        }

        // ================= 赋值工具 =================

        private static void SetAssetRef(Object target, string fieldName, Object asset, System.Text.StringBuilder log)
        {
            if (target == null) return;
            if (asset == null)
            {
                Debug.LogWarning($"[菌核狂潮] 资产不存在，跳过 {target.GetType().Name}.{fieldName}");
                return;
            }

            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[菌核狂潮] 找不到字段 {target.GetType().Name}.{fieldName}");
                return;
            }
            prop.objectReferenceValue = asset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            log.AppendLine($"  {target.GetType().Name}.{fieldName} ← {asset.name}");
        }

        private static void WireRef(Component target, string fieldName, Object value, System.Text.StringBuilder log)
        {
            if (target == null)
            {
                Debug.LogWarning($"[菌核狂潮] 找不到组件，跳过接线 {fieldName}");
                return;
            }
            if (value == null)
            {
                Debug.LogWarning($"[菌核狂潮] 目标为空，跳过 {target.GetType().Name}.{fieldName}");
                return;
            }

            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[菌核狂潮] 找不到字段 {target.GetType().Name}.{fieldName}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            log.AppendLine($"  {target.GetType().Name}.{fieldName} ← {value.name}");
        }
    }
}
