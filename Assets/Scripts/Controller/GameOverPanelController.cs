using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 死亡面板（表现层 MonoBehaviour，IController）。
    /// 面板本体是预制体 `Prefabs/GameOverPanel.prefab`，本脚本负责按阶段显隐、刷新结算文本与按钮绑定。
    /// 提供两条路径：「重开」直接进下一局；「返回开始界面」清空场地后回到 <see cref="GamePhase.Ready"/>。
    /// </summary>
    public class GameOverPanelController : MonoBehaviour, IController
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text timeText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Start()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(BackToMainMenu);
            }

            HidePanel();
            this.GetModel<IGameStateModel>().Phase.Register(OnPhaseChanged);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.GameOver)
            {
                ShowPanel();
            }
        }

        private void ShowPanel()
        {
            var model = this.GetModel<IGameStateModel>();
            if (timeText != null)
            {
                timeText.text = "存活 " + Mathf.FloorToInt(model.SurvivedTime.Value) + " 秒 | 击杀 " + model.Kills.Value;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        private void HidePanel()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        /// <summary>「重开」：重置本局并直接进入局内。</summary>
        private void Restart()
        {
            HidePanel();
            this.SendCommand(new StartNewGameCommand());
        }

        /// <summary>「返回开始界面」：重置本局（含清空场地）并回到 Ready 阶段。</summary>
        private void BackToMainMenu()
        {
            HidePanel();
            this.SendCommand(new StartNewGameCommand(GamePhase.Ready));
        }
    }
}
