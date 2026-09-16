using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// HUD（表现层 MonoBehaviour，IController）。
    /// 面板本体是预制体 `Prefabs/HudPanel.prefab`，本脚本负责按阶段显隐、刷新文本与经验条。
    /// 血量是玩家本地运行时状态（不放 Model），用每帧轮询；其余用事件刷新。
    /// </summary>
    public class HudController : MonoBehaviour, IController
    {
        [SerializeField] private PlayerController playerInput;
        [SerializeField] private Text hpText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text killsText;
        [SerializeField] private Text expText;
        [SerializeField] private Image expBarFill;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Start()
        {
            // 玩家是场景对象，预制体无法跨对象引用，故预制体里留空、运行时查找
            if (playerInput == null)
            {
                playerInput = FindObjectOfType<PlayerController>();
            }

            var model = this.GetModel<IGameStateModel>();
            model.SurvivedTime.Register(_ => RefreshTop());
            model.Wave.Register(_ => RefreshTop());
            model.Kills.Register(_ => RefreshTop());
            model.Experience.Register(_ => RefreshExp());
            model.Level.Register(_ => RefreshExp());
            model.Phase.Register(OnPhaseChanged);
            RefreshTop();
            RefreshExp();

            // 开始界面期间整块 HUD 隐藏
            OnPhaseChanged(model.Phase.Value);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            gameObject.SetActive(phase != GamePhase.Ready);

            // Reset() 走 SetValueWithoutEvent 静默归零（防误弹升级面板），HUD 的监听收不到通知；
            // 故进入局内时主动刷一次，否则死亡重开后等级/经验会停在上一局的数值。
            if (phase == GamePhase.Playing)
            {
                RefreshTop();
                RefreshExp();
            }
        }

        private void Update()
        {
            RefreshHP();
        }

        private void RefreshTop()
        {
            var model = this.GetModel<IGameStateModel>();
            if (timeText != null) timeText.text = "时间 " + Mathf.FloorToInt(model.SurvivedTime.Value) + "s";
            if (waveText != null) waveText.text = "波次 " + model.Wave.Value;
            if (killsText != null) killsText.text = "击杀 " + model.Kills.Value;
        }

        /// <summary>刷新经验条与等级文本。阈值按当前等级现算，不缓存。</summary>
        private void RefreshExp()
        {
            var model = this.GetModel<IGameStateModel>();
            int level = model.Level.Value;
            int exp = model.Experience.Value;
            int threshold = this.GetSystem<IUpgradeSystem>().GetThreshold(level);

            if (expBarFill != null)
            {
                // 用锚点宽度表示进度，而不是 Image.fillAmount：
                // Image 没有 Source Image 时 type / fillAmount 不生效（Unity 会直接走 Simple 的全矩形绘制），
                // 而本工程的美术全是无 sprite 的占位色块。
                float ratio = threshold > 0 ? Mathf.Clamp01((float)exp / threshold) : 0f;
                var rect = expBarFill.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(ratio, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            if (expText != null)
            {
                expText.text = "Lv." + level + "   " + exp + " / " + threshold;
            }
        }

        private void RefreshHP()
        {
            if (playerInput == null || hpText == null) return;
            hpText.text = "HP " + playerInput.CurrentHealth + "/" + playerInput.MaxHealth;
        }
    }
}
