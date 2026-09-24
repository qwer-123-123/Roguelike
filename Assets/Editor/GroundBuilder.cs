using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 生成地面（编辑器工具，可重跑）。
    ///
    /// 做法：<see cref="SpriteRenderer"/> 的 **Tiled 平铺模式** —— 一张瓦片 sprite 铺满整个
    /// 渲染器尺寸，**不占额外显存**（对比：把 40×40 的场地烘焙成一张贴图，按 108 px/单位
    /// 算要 4300² ≈ 70MB）。代价是单一瓦片、没有自动拼接的地貌过渡。
    ///
    /// 完整的瓦片自动拼接（`TILE_MASK`，H5 那套）与地貌过渡留给迭代 4 —— 那需要把
    /// 整块地面烘焙成一张贴图，或引入 Tilemap。本轮先把「黑地图」解决掉。
    ///
    /// 用法：Unity 菜单 Tools ▸ Game ▸ Build Ground
    /// </summary>
    public static class GroundBuilder
    {
        private const string GrassTilePath = "Assets/Art/Tiles/grass_tiles_0000_Layer-55.png";

        /// <summary>地面尺寸（世界单位）。比相机视野（10 单位高）大得多，避免走出地面。</summary>
        private const float GroundSize = 48f;

        [MenuItem("Tools/Game/Build Ground")]
        public static void Build()
        {
            var importer = AssetImporter.GetAtPath(GrassTilePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[菌核狂潮] 找不到草地瓦片 {GrassTilePath}");
                return;
            }

            // Tiled 模式要求 sprite 的 mesh 是 FullRect（默认 Tight 会退化并打警告）。
            // 注意：meshType 不在 TextureImporter 上，要经 TextureImporterSettings 读写。
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                Debug.Log("[菌核狂潮] 草地瓦片 meshType → FullRect（Tiled 模式的要求）");
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrassTilePath);
            if (sprite == null)
            {
                Debug.LogError($"[菌核狂潮] 载入草地瓦片失败 {GrassTilePath}");
                return;
            }

            var go = GameObject.Find("Ground");
            if (go == null)
            {
                go = new GameObject("Ground");
                Debug.Log("[菌核狂潮] 新建 Ground 对象");
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(GroundSize, GroundSize);
            sr.sortingOrder = SortingOrders.Ground;
            sr.color = Color.white;

            // 地面上方再铺一层裂纹贴片做变化 —— 单靠平铺的草地看着太规整
            ScatterDecals(go.transform);

            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"[菌核狂潮] 地面已生成：{GroundSize}×{GroundSize} 单位，Tiled 模式的草地瓦片");
        }

        /// <summary>
        /// 在地面上撒一批裂纹 / 泥土贴片。用固定种子，保证每次重跑布局一致。
        /// </summary>
        private static void ScatterDecals(Transform parent)
        {
            var decals = new System.Collections.Generic.List<Sprite>();

            // 裂纹贴片在 UI/Elements 下（迭代 0 拆帧时归到那里），文件名是
            // element_0154_Layer-156 这种——Layer 号不连续，所以扫目录按前缀筛，不要拼名字
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/UI/Elements" });
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                // 裂纹贴片的编号区间是 0154~0181
                if (sp != null && p.Contains("element_01")) decals.Add(sp);
            }

            if (decals.Count == 0)
            {
                Debug.LogWarning("[菌核狂潮] 没找到裂纹贴片，跳过地面装饰");
                return;
            }

            // 清掉上一次生成的装饰，保证可重跑
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }

            var rng = new System.Random(20260924);
            const int count = 40;
            float half = GroundSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Decal_{i:D2}");
                go.transform.SetParent(parent, false);

                float x = (float)(rng.NextDouble() * 2 - 1) * (half - 2f);
                float y = (float)(rng.NextDouble() * 2 - 1) * (half - 2f);
                float s = 0.6f + (float)rng.NextDouble() * 1.6f;
                float rot = (float)rng.NextDouble() * 360f;

                go.transform.localPosition = new Vector3(x, y, 0f);
                go.transform.localScale = Vector3.one * s;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);

                var dsr = go.AddComponent<SpriteRenderer>();
                dsr.sprite = decals[rng.Next(decals.Count)];
                dsr.sortingOrder = SortingOrders.GroundDetail;
                dsr.color = new Color(1f, 1f, 1f, 0.45f);
            }

            Debug.Log($"[菌核狂潮] 地面装饰 {count} 个（固定种子，可重跑）");
        }
    }
}
