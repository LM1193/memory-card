using System.Collections;
using UnityEngine;

namespace MemoryCard
{
    /// <summary>
    /// 游戏主控：计时、翻牌流程、配对判定、计分与胜负。
    /// 挂载位置：场景中的一个空物体（命名为 GameManager），
    ///           并在 Inspector 中把 board 与 ui 拖进来。
    ///
    /// 【路线 B：世界坐标方案】卡牌是场景中的 SpriteRenderer 物体，
    /// 点击由 Card.OnMouseDown 触发，本脚本不直接处理输入。
    ///
    /// 对应课件：
    ///   第 13 页 —— 配对判定：_firstRevealed.id == secondRevealed.id
    ///   第 14 页 —— 协同程序：IEnumerator + yield return new WaitForSeconds(1.5f) + StartCoroutine
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;

        /// <summary>
        /// 全局唯一入口。
        /// 【为什么不是简单的 { get; private set; }】
        /// 播放模式中途若发生脚本重编译，Unity 会重载域并清空所有静态字段，
        /// 但场景中的对象仍然存活 —— 此时静态引用变 null，UI 里
        /// "if (GameManager.Instance != null)" 判断会失败，表现为「点 Start 没反应」。
        /// 改成惰性自愈：为空时按需在场景里重新找回，单次域重载不再让游戏卡死。
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = FindFirstObjectByType<GameManager>();
#else
                    _instance = FindObjectOfType<GameManager>();
#endif
                }
                return _instance;
            }
            private set { _instance = value; }
        }

        [Header("引用")]
        [SerializeField] private BoardManager board;
        [SerializeField] private UIManager ui;
        [Tooltip("背景音乐播放器（独立 AudioSource，循环播放 bgm_loop.wav）")]
        [SerializeField] private MusicPlayer music;

        [Header("参数")]
        [Tooltip("配对失败后停留多久再翻回（秒），课件第 14 页示例为 1.5")]
        [SerializeField] private float flipBackDelay = 1.5f;

        [Header("计分规则")]
        [Tooltip("配对成功一组的得分")]
        [SerializeField] private int scorePerMatch = 10;
        [Tooltip("配对失败一次扣的分，分数不会被扣成负数")]
        [SerializeField] private int penaltyPerMismatch = 2;

        private const string BestTimeKey = "MC_BestTime";

        private Card _firstRevealed;      // 课件：_firstRevealed
        private Card _secondRevealed;     // 课件：secondReveled

        private int _score;
        private int _matchedPairs;
        private int _moves;
        private float _timer;

        private bool _isPlaying;          // 是否正在计时/可操作
        private bool _isChecking;         // 配对判定协程进行中（防连点锁）
        private bool _isWin;

        public float ElapsedTime => _timer;
        public int Score => _score;
        public int Moves => _moves;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 音效在这个物体上就地初始化（自动挂 AudioSource，无需改场景）
            Sfx.Init(gameObject);

            // 兜底自动查找（正常情况请在 Inspector 手动拖入引用）
            // 注意：FindObjectOfType 在 Unity 2023.1+ 已弃用，故按版本条件编译
            if (board == null)
            {
#if UNITY_2023_1_OR_NEWER
                board = FindFirstObjectByType<BoardManager>();
#else
                board = FindObjectOfType<BoardManager>();
#endif
            }
            if (ui == null)
            {
#if UNITY_2023_1_OR_NEWER
                ui = FindFirstObjectByType<UIManager>();
#else
                ui = FindObjectOfType<UIManager>();
#endif
            }
            if (music == null)
            {
#if UNITY_2023_1_OR_NEWER
                music = FindFirstObjectByType<MusicPlayer>();
#else
                music = FindObjectOfType<MusicPlayer>();
#endif
            }
        }

        private void Start()
        {
            // 先展示开始面板，等待玩家点击 Start
            if (ui != null) ui.ShowStart();
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _timer += Time.deltaTime;
            if (ui != null) ui.UpdateTime(_timer);
        }

        /// <summary>开始（或重开）一局：重置状态 → 洗牌 → 铺牌 → 进入计时。</summary>
        public void StartGame()
        {
            // 清理上一局可能残留的协程与状态，避免"重开瞬间还在翻回"
            StopAllCoroutines();
            _isChecking = false;
            _firstRevealed = null;
            _secondRevealed = null;

            _score = 0;
            _matchedPairs = 0;
            _moves = 0;
            _timer = 0f;
            _isWin = false;
            _isPlaying = true;

            board.BuildNumbers();
            board.Shuffle();
            board.BuildBoard(OnCardClicked);

            // 重开时恢复背景音乐音量，并确保循环仍在播放
            if (music != null)
            {
                music.SetDucked(false);
                music.PlayMusic();
            }

            if (ui != null)
            {
                ui.HideAll();
                ui.UpdateScore(_score);
                ui.UpdateMoves(_moves);
                ui.UpdateTime(_timer);
            }
        }

        /// <summary>
        /// 玩家点击一张牌。由 Card.OnClick → 回调到这里。
        /// </summary>
        public void OnCardClicked(Card card)
        {
            if (!_isPlaying || _isWin) return;
            if (_isChecking) return;                    // 协程判定期间不接受新点击（防连点）
            if (card == null || !card.IsClickable) return;

            card.Reveal();
            Sfx.PlayFlip();

            if (_firstRevealed == null)
            {
                _firstRevealed = card;
                return;
            }

            _secondRevealed = card;

            // 步数：每翻开第二张牌记 1 步（一次配对尝试 = 1 步）
            _moves++;
            if (ui != null) ui.UpdateMoves(_moves);

            StartCoroutine(CheckMatch());               // 课件第 14 页：StartCoroutine
        }

        /// <summary>
        /// 配对判定协程（课件第 13、14 页）。
        /// 相同 → 锁定并加分；不同 → 等 1.5 秒后双双翻回背面。
        /// </summary>
        private IEnumerator CheckMatch()
        {
            _isChecking = true;

            if (_firstRevealed.id == _secondRevealed.id)          // 课件：id 相等 = 配对成功
            {
                _firstRevealed.SetMatched();
                _secondRevealed.SetMatched();
                Sfx.PlayMatch();

                _score += scorePerMatch;
                _matchedPairs++;
                if (ui != null) ui.UpdateScore(_score);

                _firstRevealed = null;
                _secondRevealed = null;
                _isChecking = false;

                if (_matchedPairs >= board.PairCount) GameOver();
            }
            else
            {
                Sfx.PlayMismatch();

                // 失败扣分，但不允许扣成负数（避免新手连错后分数为负）
                _score = Mathf.Max(0, _score - penaltyPerMismatch);
                if (ui != null) ui.UpdateScore(_score);

                // 课件第 14 页：让该函数暂停 1.5 秒后继续
                yield return new WaitForSeconds(flipBackDelay);

                _firstRevealed.Hide();
                _secondRevealed.Hide();

                _firstRevealed = null;
                _secondRevealed = null;
                _isChecking = false;
            }
        }

        /// <summary>胜利结算：停表 → 记录最佳成绩 → 弹出胜利面板。</summary>
        private void GameOver()
        {
            _isWin = true;
            _isPlaying = false;
            Sfx.PlayWin();
            if (music != null) music.SetDucked(true);   // 压低 BGM，让胜利音效突出

            float best = PlayerPrefs.GetFloat(BestTimeKey, 0f);
            bool isNewRecord = best <= 0f || _timer < best;
            if (isNewRecord)
            {
                best = _timer;
                PlayerPrefs.SetFloat(BestTimeKey, best);
                PlayerPrefs.Save();
            }

            if (ui != null) ui.ShowWin(_timer, _score, _moves, best, isNewRecord);
        }
    }
}
