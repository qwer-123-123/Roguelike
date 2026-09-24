using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 兵种选择界面（表现层 MonoBehaviour，IController）。
    /// 面板本体是预制体 <c>Prefabs/ClassSelectPanel.prefab</c>，本脚本只做显示/隐藏与按钮绑定，
    /// **不创建任何 UI**（工程约定：UI 一律用预制体实例，脚本只通过 [SerializeField] 引用）。
    ///
    /// 仅在 <see cref="GamePhase.ClassSelect"/> 阶段可见。该阶段不属于 Playing，
    /// 所以各玩法逻辑的门禁会自动让世界静止，不需要额外处理。
    ///
    /// ⚠ 文案目前是 **ASCII 占位**：工程尚无中文字体（策划案风险 R6），
    ///   用中文会渲染成豆腐块。字体到位后把 <see cref="BuildLabel"/> 换成
    ///   <see cref="ClassCatalog.DisplayNameAt"/> 即可。
    /// </summary>
    public class ClassSelectPanelController : MonoBehaviour, IController
    {
        [Header("面板")]
        [SerializeField] private GameObject panel;

        [Header("兵种列表（10 个，顺序即 index）")]
        [SerializeField] private Button[] classButtons = new Button[0];
        [SerializeField] private Text[] classButtonLabels = new Text[0];

        [Header("详情区")]
        [SerializeField] private Image portrait;
        [SerializeField] private Text nameText;
        [SerializeField] private Text roleText;
        [SerializeField] private Text[] statTexts = new Text[0];
        [SerializeField] private Image[] weaponIcons = new Image[0];

        [Header("开始")]
        [SerializeField] private Button startButton;

        private ClassCatalog catalog;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private IPlayerStatSystem Stats => this.GetSystem<IPlayerStatSystem>();

        private void Start()
        {
            catalog = Object.FindObjectOfType<GameConfigHolder>()?.Catalog;

            if (classButtons != null)
            {
                for (int i = 0; i < classButtons.Length; i++)
                {
                    if (classButtons[i] == null) continue;
                    int index = i;   // 闭包捕获：直接用 i 会让所有按钮都传最后一个值
                    classButtons[i].onClick.AddListener(() => OnClassClicked(index));
                }
            }

            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);

            // 用当前阶段同步初始可见性（架构可能在 Start 之前就已初始化）
            var model = this.GetModel<IGameStateModel>();
            OnPhaseChanged(model.Phase.Value);
            model.Phase.Register(OnPhaseChanged);
            model.SelectedClassIndex.Register(_ => RefreshDetail());
        }

        /// <summary>
        /// 每次启用都刷一遍。刷新是幂等的，且被重新激活时不该显示陈旧数据。
        /// 首次激活时 catalog 可能还没拿到（Start 尚未跑），RefreshAll 内部有判空守卫。
        /// </summary>
        private void OnEnable()
        {
            if (catalog == null) catalog = Object.FindObjectOfType<GameConfigHolder>()?.Catalog;
            RefreshAll();
        }

        private void OnDestroy()
        {
            var model = this.GetModel<IGameStateModel>();
            model.Phase.UnRegister(OnPhaseChanged);
            model.SelectedClassIndex.UnRegister(_ => RefreshDetail());
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            bool visible = phase == GamePhase.ClassSelect;
            if (panel != null) panel.SetActive(visible);
            if (visible) RefreshAll();
        }

        // ================= 交互 =================

        private void OnClassClicked(int index)
        {
            this.SendCommand(new SelectClassCommand(index));
        }

        /// <summary>「出发」：按当前选中的兵种开局。</summary>
        private void OnStartClicked()
        {
            this.SendCommand(new StartNewGameCommand());
        }

        // ================= 刷新 =================

        private void RefreshAll()
        {
            RefreshList();
            RefreshDetail();
        }

        private void RefreshList()
        {
            if (classButtons == null) return;

            for (int i = 0; i < classButtons.Length; i++)
            {
                if (classButtons[i] == null) continue;
                bool has = catalog != null && i < catalog.Count;
                classButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                if (classButtonLabels != null && i < classButtonLabels.Length && classButtonLabels[i] != null)
                {
                    classButtonLabels[i].text = BuildLabel(i);
                }
            }
        }

        private void RefreshDetail()
        {
            if (catalog == null || catalog.Count == 0) return;

            int index = Mathf.Clamp(this.GetModel<IGameStateModel>().SelectedClassIndex.Value, 0, catalog.Count - 1);
            var body = catalog.BodyAt(index);
            var weapon = catalog.WeaponAt(index);
            var stats = Stats;

            if (nameText != null) nameText.text = BuildLabel(index);
            if (roleText != null) roleText.text = weapon != null ? weapon.weaponId.ToUpperInvariant() : "";

            if (portrait != null && body != null) portrait.sprite = body.portrait;

            // 详情数值：直接读系统层，保证展示的就是实际生效的值
            SetStat(0, "HP", stats.MaxHp.ToString("0"));
            SetStat(1, "SPD", stats.BaseSpeed.ToString("0.0"));
            SetStat(2, "DMG", stats.BaseDamage.ToString("0.0"));
            SetStat(3, "RATE", stats.BaseRate.ToString("0.00"));
            SetStat(4, "RANGE", stats.BaseRange.ToString("0.0"));
            SetStat(5, "MODEL", stats.Weapon != null ? stats.Weapon.model.ToString() : "-");

            // 武器图标：高亮当前武器
            if (weaponIcons != null)
            {
                int weaponIndex = catalog.weapons != null ? System.Array.IndexOf(catalog.weapons, weapon) : -1;
                for (int i = 0; i < weaponIcons.Length; i++)
                {
                    if (weaponIcons[i] == null) continue;
                    var c = weaponIcons[i].color;
                    c.a = (i == weaponIndex) ? 1f : 0.35f;
                    weaponIcons[i].color = c;
                }
            }

            // 列表选中态：靠文字颜色区分（不额外依赖按钮的 SpriteState）
            if (classButtonLabels != null)
            {
                for (int i = 0; i < classButtonLabels.Length; i++)
                {
                    if (classButtonLabels[i] == null) continue;
                    classButtonLabels[i].color = (i == index)
                        ? new Color(0.78f, 0.88f, 0.24f)
                        : new Color(0.65f, 0.70f, 0.78f);
                }
            }
        }

        private void SetStat(int i, string label, string value)
        {
            if (statTexts == null || i >= statTexts.Length || statTexts[i] == null) return;
            statTexts[i].text = label + "   " + value;
        }

        /// <summary>
        /// ⚠ ASCII 占位文案。工程有中文字体之前只能用拉丁字形，
        /// 否则 legacy Text 会渲染成豆腐块。字体到位后改成 catalog.DisplayNameAt(index)。
        /// </summary>
        private string BuildLabel(int index)
        {
            var body = catalog != null ? catalog.BodyAt(index) : null;
            var weapon = catalog != null ? catalog.WeaponAt(index) : null;
            if (body == null || weapon == null) return "?";
            return body.bodyId.ToUpperInvariant() + " / " + weapon.weaponId.ToUpperInvariant();
        }
    }
}
