using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 配置表挂载点：把 <see cref="ClassCatalog"/> 与 <see cref="GameConfig"/> 注入
    /// <see cref="IPlayerStatSystem"/>。放在场景里，引用在 Inspector 上指定。
    ///
    /// 为什么需要它：QFramework 的 Architecture 是静态单例，场景里没有实体可以挂
    /// <c>[SerializeField]</c> 的资源引用，而 System 是纯 C#、读不到 Unity 资源。
    /// 所以由一个极简的 MonoBehaviour 在 Awake 时把资源推给 System。
    /// </summary>
    public class GameConfigHolder : MonoBehaviour, IController
    {
        [SerializeField] private ClassCatalog catalog;
        [SerializeField] private GameConfig config;

        private static GameConfig fallbackConfig;

        public ClassCatalog Catalog => catalog;

        /// <summary>未在 Inspector 指定时返回一份内存兜底配置，避免空引用打满控制台。</summary>
        public GameConfig Config
        {
            get
            {
                if (config != null) return config;
                if (fallbackConfig == null)
                {
                    fallbackConfig = ScriptableObject.CreateInstance<GameConfig>();
                    Debug.LogWarning("[菌核狂潮] GameConfigHolder 未指定 GameConfig，已使用内存兜底配置（仅本次运行有效）");
                }
                return fallbackConfig;
            }
        }

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Awake()
        {
            this.GetSystem<IPlayerStatSystem>().Bind(catalog, Config);

            SelfCheck();
        }

        /// <summary>启动自查：配置表内部一致性。只在编辑器与开发版生效。</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void SelfCheck()
        {
            if (catalog == null)
            {
                Debug.LogError("[菌核狂潮] GameConfigHolder 未指定 ClassCatalog，兵种系统不可用");
                return;
            }

            if (!catalog.Validate(out var catalogError))
            {
                Debug.LogError($"[菌核狂潮] ClassCatalog 校验失败：{catalogError}");
            }
            else
            {
                Debug.Log($"[菌核狂潮] 兵种目录校验通过，共 {catalog.Count} 个兵种");
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                var set = catalog.SpritesAt(i);
                if (set != null && !set.Validate(out var setError))
                {
                    Debug.LogError($"[菌核狂潮] {catalog.DisplayNameAt(i)} 的动画集校验失败：{setError}");
                }
            }
        }
    }
}
