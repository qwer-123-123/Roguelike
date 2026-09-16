using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 升级三选一面板（表现层 MonoBehaviour，IController）。
    /// 面板本体是预制体 `Prefabs/UpgradePanel.prefab`，本脚本负责订阅升级事件、填充选项文本与按钮绑定。
    /// </summary>
    public class UpgradePanelController : MonoBehaviour, IController
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceTexts;

        private UpgradeChoice[] currentChoices;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Start()
        {
            if (choiceButtons != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    if (choiceButtons[i] == null) continue;
                    int index = i;
                    choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(index));
                }
            }

            HidePanel();

            // 监听升级事件，而不是 Level 属性：
            // Reset() 把 Level 从高置回 1 时不会再误弹面板（迭代 5 修复 3 的根因就此消除）。
            this.RegisterEvent<OnLevelUpEvent>(OnLevelUp);
        }

        private void OnLevelUp(OnLevelUpEvent e)
        {
            ShowChoices(e.Choices);
        }

        private void ShowChoices(UpgradeChoice[] choices)
        {
            currentChoices = choices;

            int buttonCount = choiceButtons != null ? choiceButtons.Length : 0;
            for (int i = 0; i < buttonCount; i++)
            {
                bool has = choices != null && i < choices.Length;

                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].gameObject.SetActive(has);
                }

                if (has && choiceTexts != null && i < choiceTexts.Length && choiceTexts[i] != null)
                {
                    choiceTexts[i].text = choices[i].Name + "\n" + choices[i].Description;
                }
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            // 需求：弹出技能面板时暂停游戏，直到选择完成
            Time.timeScale = 0f;
        }

        /// <summary>重开一局时隐藏面板并恢复时间缩放（公开供软重启调用）。</summary>
        public void HideForNewGame()
        {
            HidePanel();
            Time.timeScale = 1f;
        }

        private void HidePanel()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnChoiceClicked(int index)
        {
            if (currentChoices == null || index >= currentChoices.Length) return;

            this.SendCommand(new ChooseUpgradeCommand(currentChoices[index].Type));
            HidePanel();
            Time.timeScale = 1f;
        }
    }
}
