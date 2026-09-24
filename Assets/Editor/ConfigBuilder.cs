using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 配置表生成器（编辑器工具，可重跑）。
    ///
    /// 从 <c>Assets/Art/</c> 的目录结构自动生成：
    ///   BodyDefinition ×2、WeaponDefinition ×5、CharacterSpriteSet ×10、
    ///   ClassCatalog ×1、GameConfig ×1
    /// 并读取 <c>Assets/Art/Manifest.csv</c> 填入 uniformScale 与各 clip 的 offsetY。
    ///
    /// 为什么不手工建：10 个兵种的动画集一共有约 260 个精灵引用，手工拖拽既慢又容易错。
    /// 本工具可重跑 —— 换了美术只要重跑一次，引用会自动重建。
    ///
    /// 用法：Unity 菜单 Tools ▸ 菌核狂潮 ▸ 生成配置表
    /// </summary>
    public static class ConfigBuilder
    {
        private const string ArtRoot = "Assets/Art";
        private const string ConfigRoot = "Assets/Configs";
        private const string ManifestPath = ArtRoot + "/Manifest.csv";

        private const string SpriteFolder = ConfigRoot + "/CharacterSprites";
        private const string BodyFolder = ConfigRoot + "/Bodies";
        private const string WeaponFolder = ConfigRoot + "/Weapons";

        // 武器顺序即兵种索引的次序（与 H5 data.js 的 WEAPON_ORDER 一致）
        private static readonly string[] WeaponOrder = { "knife", "bat", "gun", "riffle", "flame" };

        // 菜单路径用纯 ASCII：中文路径经 REST 传输时会被编码破坏
        // （editor_execute_menu 报 TARGET_NOT_FOUND，且错误信息里的路径本身就是乱码）
        [MenuItem("Tools/Game/Build Configs")]
        public static void BuildAll()
        {
            var manifest = LoadManifest();

            EnsureFolder(ConfigRoot);
            EnsureFolder(BodyFolder);
            EnsureFolder(WeaponFolder);
            EnsureFolder(SpriteFolder);

            var man = CreateBody("man", "男性", 120f, 3.2f, 1.10f, 1.00f, 0.42f, 1.75f);
            var girl = CreateBody("girl", "女性", 100f, 3.6f, 1.00f, 1.08f, 0.36f, 1.63f);

            var weapons = new Dictionary<string, WeaponDefinition>();
            foreach (var id in WeaponOrder)
            {
                weapons[id] = CreateWeapon(id);
            }

            var catalog = LoadOrCreate<ClassCatalog>(ConfigRoot + "/ClassCatalog.asset");

            var bodies = new[] { man, girl };
            catalog.bodies = bodies;
            catalog.weapons = WeaponOrder.Select(id => weapons[id]).ToArray();

            // 展平顺序 = bodyIndex * weapons.Length + weaponIndex（见 ClassCatalog 的索引约定）
            var sets = new CharacterSpriteSet[bodies.Length * WeaponOrder.Length];
            int index = 0;
            foreach (var body in bodies)
            {
                foreach (var weaponId in WeaponOrder)
                {
                    sets[index] = CreateSpriteSet(body, weapons[weaponId], manifest);
                    index++;
                }
            }
            catalog.spriteSets = sets;

            EditorUtility.SetDirty(catalog);

            var config = LoadOrCreate<GameConfig>(ConfigRoot + "/GameConfig.asset");
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[菌核狂潮] 配置表已生成：2 角色 / {weapons.Count} 武器 / {sets.Length} 个兵种动画集");
            if (!catalog.Validate(out var err))
            {
                Debug.LogError($"[菌核狂潮] ClassCatalog 校验失败：{err}");
            }
        }

        // ================= 单项创建 =================

        private static BodyDefinition CreateBody(string id, string name, float hp, float speed,
                                                 float dmgMul, float rateMul, float radius, float worldHeight)
        {
            var so = LoadOrCreate<BodyDefinition>($"{BodyFolder}/Body_{id}.asset");
            so.bodyId = id;
            so.displayName = name;
            so.baseHp = hp;
            so.baseSpeed = speed;
            so.damageMultiplier = dmgMul;
            so.attackRateMultiplier = rateMul;
            so.hitRadius = radius;
            so.worldHeight = worldHeight;
            so.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Portraits/{id} icon_no_bg.png");
            EditorUtility.SetDirty(so);
            return so;
        }

        /// <summary>数值直接抄 H5 原型 <c>data.js</c> 的 WEAPONS。</summary>
        private static WeaponDefinition CreateWeapon(string id)
        {
            var so = LoadOrCreate<WeaponDefinition>($"{WeaponFolder}/Weapon_{id}.asset");
            so.weaponId = id;

            switch (id)
            {
                case "knife":
                    Set(so, "匕首", "近战·高频", AttackModel.Arc, 2.5f, 1.0f, 8f);
                    so.arcDegrees = 100f; so.knockback = 0.6f;
                    so.atkFrames = 8; so.hitAt = 0.30f;
                    break;
                case "bat":
                    Set(so, "棒球棍", "近战·范围", AttackModel.Arc, 0.9f, 1.3f, 34f);
                    so.arcDegrees = 120f; so.knockback = 2.6f;
                    so.atkFrames = 12; so.hitAt = 0.38f;
                    break;
                case "gun":
                    Set(so, "手枪", "远程·均衡", AttackModel.Projectile, 1.6f, 6.0f, 12f);
                    so.projectileSpeed = 13f; so.shots = 1;
                    so.atkFrames = 5; so.hitAt = 0.22f;
                    break;
                case "riffle":
                    Set(so, "步枪", "远程·连射", AttackModel.BurstProjectile, 4.0f, 7.0f, 7f);
                    so.projectileSpeed = 15f; so.shots = 1; so.spread = 0.05f;
                    so.burst = 3; so.burstGap = 0.055f;
                    so.maxAmmo = 200; so.ammoRegen = 9f;
                    so.atkFrames = 9; so.hitAt = 0.18f;
                    break;
                case "flame":
                    Set(so, "火焰喷射器", "远程·扇形持续", AttackModel.Cone, 8f, 3.5f, 3f);
                    so.coneDegrees = 38f; so.burnDuration = 3f; so.burnDps = 4f;
                    so.maxAmmo = 100; so.ammoRegen = 7f;
                    so.atkFrames = 9; so.hitAt = 0f;
                    break;
            }

            so.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Items/items_{IconName(id)}.png");
            EditorUtility.SetDirty(so);
            return so;
        }

        private static void Set(WeaponDefinition so, string name, string role, AttackModel model,
                                float rate, float range, float damage)
        {
            so.displayName = name;
            so.role = role;
            so.model = model;
            so.rate = rate;
            so.range = range;
            so.damage = damage;
        }

        private static string IconName(string weaponId)
        {
            // 与 H5 data.js 的 WEAPONS[*].icon 一致
            switch (weaponId)
            {
                case "knife":  return "0015_knife";
                case "bat":    return "0002_bat";
                case "gun":    return "0000_gun";
                case "riffle": return "0014_gun";
                case "flame":  return "0001_fire";
                default:       return "0000_gun";
            }
        }

        private static CharacterSpriteSet CreateSpriteSet(BodyDefinition body, WeaponDefinition weapon,
                                                          Dictionary<string, ManifestRow> manifest)
        {
            string fileName = $"Sprites_{body.bodyId}_{weapon.weaponId}.asset";
            var so = LoadOrCreate<CharacterSpriteSet>($"{SpriteFolder}/{fileName}");
            so.body = body;
            so.weapon = weapon;

            so.uniformScale = ScaleOf(manifest, body.bodyId);

            so.idle   = BuildClip($"{ArtRoot}/Units/{body.bodyId}/{weapon.weaponId}_idle", manifest);
            so.walk   = BuildClip($"{ArtRoot}/Units/{body.bodyId}/{weapon.weaponId}_walk", manifest);
            so.attack = BuildClip($"{ArtRoot}/Units/{body.bodyId}/{weapon.weaponId}_atk",  manifest);
            so.death  = BuildClip($"{ArtRoot}/Units/{body.bodyId}/death", manifest);

            // 各 clip 的帧时长（照 H5：idle 慢、walk 中、攻击按帧数、死亡慢）
            so.idle.frameDuration = 0.105f;
            so.walk.frameDuration = 0.062f;
            so.attack.frameDuration = AttackFrameDuration(weapon.atkFrames);
            so.death.frameDuration = 0.1f;

            EditorUtility.SetDirty(so);
            return so;
        }

        private static float AttackFrameDuration(int frames)
        {
            // H5 用「帧数 / 18」秒播完整段攻击动作
            return frames > 0 ? 1f / 18f : 0.06f;
        }

        private static CharacterSpriteSet.Clip BuildClip(string folder, Dictionary<string, ManifestRow> manifest)
        {
            var clip = new CharacterSpriteSet.Clip();
            clip.frames = LoadSprites(folder);
            clip.offsetY = RowOf(manifest, folder).offsetY;
            return clip;
        }

        /// <summary>按文件名排序加载一个目录下的全部精灵（000.png, 001.png …）。</summary>
        private static Sprite[] LoadSprites(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[菌核狂潮] 目录不存在：{folder}");
                return new Sprite[0];
            }

            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var list = new List<Sprite>(guids.Length);
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) list.Add(sprite);
            }

            // FindAssets 的顺序不保证，必须按名字排序，否则帧序会乱
            return list.OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();
        }

        // ================= Manifest =================

        private struct ManifestRow
        {
            public float scale;
            public float offsetY;
        }

        /// <summary>
        /// 读 Manifest.csv，按**帧所在目录**索引 scale 与 offsetY。
        ///
        /// 为什么按目录而不是按「角色|状态」：同一个角色的每把武器是不同的目录
        /// （<c>Units/man/gun_idle</c> 与 <c>Units/man/knife_idle</c>），帧高不同，
        /// offsetY 也不同。若只按状态归并会把它们混成一个值。
        ///
        /// 另有一条硬校验：**同一角色的所有 clip 必须共用同一个 scale**——
        /// 这正是 H5 commit 851b49b「攻击时人物缩小」那条修复的核心，表里若不一致必须报错而不是静默接受。
        /// </summary>
        private static Dictionary<string, ManifestRow> LoadManifest()
        {
            var byFolder = new Dictionary<string, ManifestRow>();
            var scaleByChar = new Dictionary<string, float>();

            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[菌核狂潮] 找不到 {ManifestPath}，请先跑 Docs/Design/_tools/split-frames.ps1");
                return byFolder;
            }

            var lines = File.ReadAllLines(ManifestPath);
            for (int i = 1; i < lines.Length; i++)   // 跳过表头
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 11) continue;

                string assetPath = parts[0];          // Assets/Art/Units/man/gun_idle/000.png
                string charId = parts[2];
                float scale = float.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture);
                float offsetY = float.Parse(parts[10], System.Globalization.CultureInfo.InvariantCulture);

                string folder = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                if (!byFolder.ContainsKey(folder))
                {
                    byFolder[folder] = new ManifestRow { scale = scale, offsetY = offsetY };
                }

                if (scaleByChar.TryGetValue(charId, out var prev))
                {
                    if (Mathf.Abs(prev - scale) > 0.0001f)
                    {
                        Debug.LogError($"[菌核狂潮] {charId} 的 scale 不一致（{prev} vs {scale}，来自 {folder}）—— " +
                                       "同一角色的所有动画必须共用一个缩放比，否则会出现「攻击时人物缩小」");
                    }
                }
                else
                {
                    scaleByChar[charId] = scale;
                }
            }

            return byFolder;
        }

        /// <summary>取某角色任意一个 clip 的 scale（同角色各 clip 相同，表里已校验）。</summary>
        private static float ScaleOf(Dictionary<string, ManifestRow> manifest, string charId)
        {
            foreach (var kv in manifest)
            {
                // 目录形如 Assets/Art/Units/man/gun_idle —— 用 /man/ 做角色判定
                if (kv.Key.Contains("/" + charId + "/")) return kv.Value.scale;
            }
            return 1f;
        }

        private static ManifestRow RowOf(Dictionary<string, ManifestRow> manifest, string folder)
        {
            return manifest.TryGetValue(folder, out var row) ? row : new ManifestRow { scale = 1f, offsetY = 0f };
        }

        // ================= 工具 =================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
