using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 生成兵种选择界面的预制体（编辑器工具，可重跑）。
    ///
    /// 产物是**预制体资产** + 场景里的一个实例 —— 满足工程约定
    /// 「UI 一律用预制体，脚本只通过 [SerializeField] 引用，不存在代码创建的 UI」。
    /// 本工具本身只在编辑器期搭层级并存成资产，不参与运行时。
    ///
    /// ⚠ 文字用 **legacy UnityEngine.UI.Text**，不是 TMP。
    ///   工程约定禁用 TMP（其自带字体无中文字形会渲染成豆腐块），
    ///   而 unity-skills 的 ui_create_text 在有 TMP 时会默认建 TMP，所以这里手工建 Text。
    ///
    /// 用法：Unity 菜单 Tools ▸ Game ▸ Build Class Select Panel
    /// </summary>
    public static class ClassSelectPanelBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/ClassSelectPanel.prefab";
        private const int ClassCount = 10;
        private const int WeaponCount = 5;

        // 颜色对齐既有面板的暗色工业风
        private static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.08f, 0.94f);
        private static readonly Color ButtonBg = new Color(0.11f, 0.13f, 0.16f, 1f);
        private static readonly Color AccentText = new Color(0.78f, 0.88f, 0.24f, 1f);
        private static readonly Color NormalText = new Color(0.85f, 0.88f, 0.92f, 1f);
        private static readonly Color MutedText = new Color(0.65f, 0.70f, 0.78f, 1f);

        [MenuItem("Tools/Game/Build Class Select Panel")]
        public static void Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "重建兵种选择预制体",
                    PrefabPath + " 已存在，重建会覆盖它（场景里已有的实例会失去引用）。继续？",
                    "覆盖", "取消");
                if (!overwrite) return;
            }

            var root = BuildHierarchy();
            EnsureFolder("Assets/Prefabs");

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // 场景里放一个实例（工程约定：面板实例直接放在场景里）
            var sceneGo = GameObject.Find("ClassSelectCanvas");
            if (sceneGo != null) Object.DestroyImmediate(sceneGo);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "ClassSelectCanvas";

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[菌核狂潮] 兵种选择预制体已生成：{PrefabPath}，并在场景里放了实例 ClassSelectCanvas");
        }

        // ================= 层级搭建 =================

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("ClassSelectPanel", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;   // 见计划：StartMenu 100 > ClassSelect 95 > GameOver 90 > Hud 0

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;   // 与既有面板一致
            scaler.referenceResolution = new Vector2(800, 600);
            root.AddComponent<GraphicRaycaster>();

            var panel = NewUI("ClassSelectPanel", root.transform);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = PanelBg;

            // ---- 标题 ----
            var title = NewText("Title", panel.transform, "SELECT CLASS", 40, AccentText);
            Anchor(title, new Vector2(0.5f, 0.92f), new Vector2(720, 60));

            // ---- 兵种列表：两列 × 五行（左列男性、右列女性，与索引顺序一致） ----
            var classButtons = new List<Button>();
            var classLabels = new List<Text>();

            for (int i = 0; i < ClassCount; i++)
            {
                int col = i / WeaponCount;      // 0 = 男性, 1 = 女性
                int row = i % WeaponCount;

                float x = col == 0 ? 0.05f : 0.27f;
                float y = 0.80f - row * 0.128f;  // 5 行：0.80 / 0.672 / ...

                var item = NewUI($"Class{i}", panel.transform);
                Anchor(item, new Vector2(x, y), new Vector2(170, 46), new Vector2(0f, 0.5f));
                var img = item.gameObject.AddComponent<Image>();
                img.color = ButtonBg;

                var btn = item.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;

                var label = NewText($"ClassLabel{i}", item, "—", 16, NormalText);
                Stretch(label);

                classButtons.Add(btn);
                classLabels.Add(label);
            }

            // ---- 详情区 ----
            var portrait = NewUI("Portrait", panel.transform);
            Anchor(portrait, new Vector2(0.72f, 0.62f), new Vector2(150, 150));
            var portraitImg = portrait.gameObject.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.color = Color.white;

            var nameText = NewText("ClassName", panel.transform, "—", 28, AccentText);
            Anchor(nameText, new Vector2(0.72f, 0.475f), new Vector2(380, 40));

            var roleText = NewText("ClassRole", panel.transform, "—", 18, MutedText);
            Anchor(roleText, new Vector2(0.72f, 0.425f), new Vector2(380, 30));

            var statTexts = new List<Text>();
            for (int i = 0; i < 6; i++)
            {
                var t = NewText($"Stat{i}", panel.transform, "—", 17, NormalText);
                Anchor(t, new Vector2(0.72f, 0.365f - i * 0.048f), new Vector2(380, 26));
                statTexts.Add(t);
            }

            // ---- 武器图标行 ----
            var weaponIcons = new List<Image>();
            for (int i = 0; i < WeaponCount; i++)
            {
                var icon = NewUI($"Weapon{i}", panel.transform);
                Anchor(icon, new Vector2(0.60f + i * 0.06f, 0.075f), new Vector2(44, 44));
                var img = icon.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.color = new Color(1f, 1f, 1f, 0.35f);
                weaponIcons.Add(img);
            }

            // ---- 开始按钮 ----
            var startItem = NewUI("StartButton", panel.transform);
            Anchor(startItem, new Vector2(0.72f, 0.075f), new Vector2(200, 48));
            var startImg = startItem.gameObject.AddComponent<Image>();
            startImg.color = new Color(0.30f, 0.42f, 0.12f, 1f);
            var startBtn = startItem.gameObject.AddComponent<Button>();
            startBtn.targetGraphic = startImg;
            var startLabel = NewText("StartLabel", startItem, "START", 20, AccentText);
            Stretch(startLabel);

            // ---- 绑定控制器 ----
            var ctrl = root.AddComponent<ClassSelectPanelController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            AssignArray(so, "classButtons", classButtons.ToArray());
            AssignArray(so, "classButtonLabels", classLabels.ToArray());
            so.FindProperty("portrait").objectReferenceValue = portraitImg;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("roleText").objectReferenceValue = roleText;
            AssignArray(so, "statTexts", statTexts.ToArray());
            AssignArray(so, "weaponIcons", weaponIcons.ToArray());
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ================= 小工具 =================

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// 新建 legacy Text（**不是 TMP**）。
        /// 字体用内置的 LegacyRuntime.ttf —— 它只有拉丁字形，所以界面文案暂用 ASCII 占位。
        /// </summary>
        private static Text NewText(string name, Transform parent, string content, int fontSize, Color color)
        {
            var rt = NewUI(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = BuiltinFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font BuiltinFont()
        {
            // 2022 起内置字体改名为 LegacyRuntime.ttf；旧名 Arial.ttf 作为兜底
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        // 收 Component 而不是 RectTransform：调用方既有 RectTransform（UI 节点）
        // 也有 Text / Image（组件），后者不能隐式转成 RectTransform，会编译失败。
        private static void Stretch(Component c)
        {
            var rt = c != null ? c.transform as RectTransform : null;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(Component c, Vector2 anchor, Vector2 size)
        {
            Anchor(c, anchor, size, new Vector2(0.5f, 0.5f));
        }

        private static void Anchor(Component c, Vector2 anchor, Vector2 size, Vector2 pivot)
        {
            var rt = c != null ? c.transform as RectTransform : null;
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void AssignArray(SerializedObject so, string field, Object[] values)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[菌核狂潮] 找不到字段 {field}");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
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
