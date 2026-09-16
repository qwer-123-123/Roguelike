using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 开始界面（表现层 MonoBehaviour，IController）。
    /// 面板本体是预制体 `Prefabs/StartMenuPanel.prefab`，本脚本只负责按阶段显示/隐藏与按钮绑定。
    /// 仅在 <see cref="GamePhase.Ready"/> 阶段可见；显示期间世界静止（各玩法逻辑按 Phase 门禁停摆），不依赖 Time.timeScale。
    /// </summary>
    public class StartMenuPanelController : MonoBehaviour, IController
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button startButton;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
            }

            // 用当前阶段同步初始可见性（架构可能在 Start 之前就已初始化）
            var model = this.GetModel<IGameStateModel>();
            OnPhaseChanged(model.Phase.Value);
            model.Phase.Register(OnPhaseChanged);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (panel != null)
            {
                panel.SetActive(phase == GamePhase.Ready);
            }
        }

        /// <summary>「开始游戏」：重置一局并进入局内。</summary>
        private void StartGame()
        {
            this.SendCommand(new StartNewGameCommand());
        }
    }
}
