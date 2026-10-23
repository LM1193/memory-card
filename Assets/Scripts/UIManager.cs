using UnityEngine;
using UnityEngine.UI;

namespace MemoryCard
{
    /// <summary>
    /// 界面管理：计时/分数显示、开始面板、胜利面板与按钮事件。
    /// 挂载位置：Canvas。
    ///
    /// 字体说明：Unity 内置 Arial 【不含中文字形】，WebGL 下中文会显示为方框，
    /// 因此本脚本的固定文案一律使用英文与数字。
    /// 若确实需要中文，请把 Text 换成 TextMeshProUGUI，并生成中文字体图集（Font Asset）。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("文本")]
        [SerializeField] private Text timeText;      // 只显示时间数字，如 "12.3"
        [SerializeField] private Text scoreText;     // 如 "SCORE 3"
        [SerializeField] private Text resultText;    // 胜利面板：用时与得分
        [SerializeField] private Text bestText;      // 胜利面板：历史最佳

        [Header("面板")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject winPanel;

        [Header("按钮")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button restartButton;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (restartButton != null) restartButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
        }

        /// <summary>每帧刷新计时显示。只显示数字，避免中文字体问题。</summary>
        public void UpdateTime(float seconds)
        {
            if (timeText != null) timeText.text = seconds.ToString("F1");
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null) scoreText.text = $"SCORE {score}";
        }

        /// <summary>显示开始面板（进入游戏时）。</summary>
        public void ShowStart()
        {
            if (startPanel != null) startPanel.SetActive(true);
            if (winPanel != null) winPanel.SetActive(false);
        }

        /// <summary>显示胜利面板。</summary>
        public void ShowWin(float time, int score, float best, bool isNewRecord)
        {
            if (winPanel != null) winPanel.SetActive(true);
            if (startPanel != null) startPanel.SetActive(false);

            if (resultText != null)
                resultText.text = $"TIME {time:F1}s    SCORE {score}";

            if (bestText != null)
                bestText.text = isNewRecord ? $"BEST {best:F1}s  NEW!" : $"BEST {best:F1}s";
        }

        /// <summary>开始游戏时隐藏所有弹窗面板。</summary>
        public void HideAll()
        {
            if (startPanel != null) startPanel.SetActive(false);
            if (winPanel != null) winPanel.SetActive(false);
        }
    }
}
