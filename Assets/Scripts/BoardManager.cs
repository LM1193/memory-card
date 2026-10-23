using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemoryCard
{
    /// <summary>
    /// 牌桌管理：负责卡牌池（numbers 数组）、洗牌，以及按【课件第 7 页的世界坐标公式】生成 12 张卡牌。
    ///
    /// 【路线 B：世界坐标方案 —— 对齐课件】
    /// 本脚本不使用 UGUI 的 GridLayoutGroup，卡牌位置完全由公式计算：
    ///     (x, y) = (offsetX * j + originalX, offsetY * i + originalY)
    /// 其中 i 为行号（0 起，自下而上）、j 为列号（0 起，自左向右）。
    ///
    /// 挂载位置：场景中的空物体（命名为 Board），不需要任何 UI 组件。
    ///
    /// 对应课件：
    ///   第 7 页  —— 布局：3 行 4 列；originalX=-3, originalY=0, offsetX=2, offsetY=2.5
    ///   第 9 页  —— images 数组（6 种牌面）+ numbers 数组初始 {0,0,1,1,2,2,3,3,4,4,5,5}
    ///   第 10 页 —— 洗牌
    ///   第 11 页 —— 第 i 行第 j 列取 Images[numbers[i * cols + j]]
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        [Header("布局（课件第 7 页）")]
        [Tooltip("行数（课件示例为 3）")]
        [SerializeField] private int rows = 3;
        [Tooltip("列数（课件示例为 4）")]
        [SerializeField] private int cols = 4;
        [Tooltip("左下角起始点 X（课件示例为 -3）")]
        [SerializeField] private float originalX = -3f;
        [Tooltip("左下角起始点 Y（课件示例为 0）")]
        [SerializeField] private float originalY = 0f;
        [Tooltip("横向间隔（课件示例为 2）")]
        [SerializeField] private float offsetX = 2f;
        [Tooltip("纵向间隔（课件示例为 2.5）")]
        [SerializeField] private float offsetY = 2.5f;

        [Header("牌面素材（课件第 9 页 images 数组，顺序 = 牌面 ID）")]
        [Tooltip("依次放入：circle / crescent / diamond / heart / sanjiao / square")]
        [SerializeField] private Sprite[] images = new Sprite[6];

        [Header("卡背素材")]
        [SerializeField] private Sprite backSprite;

        [Header("预制体与层级")]
        [SerializeField] private Card cardPrefab;
        [Tooltip("卡牌的渲染层级，必须大于背景的 sortingOrder")]
        [SerializeField] private int cardSortingOrder = 0;
        [Tooltip("卡牌父节点；留空则挂在自身下")]
        [SerializeField] private Transform boardRoot;

        [Header("规格参考")]
        [Tooltip("卡牌世界尺寸：150×200 像素 ÷ PPU 100 = 1.5 × 2 世界单位")]
        [SerializeField] private Vector2 cardWorldSize = new Vector2(1.5f, 2f);

        private int[] _numbers;                          // 课件第 9 页的 numbers 数组
        private readonly List<Card> _cards = new List<Card>();

        public IReadOnlyList<Card> Cards => _cards;
        public int Rows => rows;
        public int Cols => cols;
        /// <summary>总对数 = 牌数 / 2</summary>
        public int PairCount => rows * cols / 2;
        public int TotalCards => rows * cols;

        private void Awake()
        {
            if (boardRoot == null) boardRoot = transform;
        }

        /// <summary>
        /// 课件第 9 页：生成长度为 12 的 numbers 数组，值为 {0,0,1,1,2,2,3,3,4,4,5,5}，
        /// 保证每种牌面恰好出现 2 张。
        /// </summary>
        public void BuildNumbers()
        {
            _numbers = new int[TotalCards];
            for (int k = 0; k < _numbers.Length; k++)
            {
                _numbers[k] = k / 2;     // 0,0,1,1,2,2,...
            }
        }

        /// <summary>课件第 10 页：对 numbers 数组洗牌。</summary>
        public void Shuffle()
        {
            if (_numbers == null || _numbers.Length != TotalCards) BuildNumbers();
            ShuffleUtil.Shuffle(_numbers);
        }

        /// <summary>
        /// 课件第 7、11 页：按世界坐标公式算出每张卡的位置并生成卡牌。
        /// 卡牌种类由 idx = numbers[i * cols + j] 决定。
        /// </summary>
        /// <param name="onCardClicked">点击回调，通常传 GameManager.OnCardClicked</param>
        public void BuildBoard(Action<Card> onCardClicked)
        {
            ClearBoard();
            if (_numbers == null || _numbers.Length != TotalCards) BuildNumbers();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // ---------- 课件第 7 页布局公式 ----------
                    float x = offsetX * j + originalX;
                    float y = offsetY * i + originalY;
                    // --------------------------------------

                    int index = _numbers[i * cols + j];     // 课件第 11 页

                    Card card = Instantiate(cardPrefab, boardRoot);
                    card.name = $"Card_{i}_{j}";
                    card.transform.position = new Vector3(x, y, 0f);
                    card.Init(index, images[index], backSprite, cardSortingOrder, onCardClicked);

                    _cards.Add(card);
                }
            }
        }

        /// <summary>把所有牌翻回背面。</summary>
        public void HideAll()
        {
            foreach (Card c in _cards) c.Hide();
        }

        /// <summary>销毁当前所有卡牌对象。</summary>
        public void ClearBoard()
        {
            foreach (Card c in _cards)
            {
                if (c != null) Destroy(c.gameObject);
            }
            _cards.Clear();
        }

        /// <summary>按课件公式计算第 i 行第 j 列的位置（供外部复用或调试）。</summary>
        public Vector2 GetPosition(int i, int j)
        {
            return new Vector2(offsetX * j + originalX, offsetY * i + originalY);
        }

        /// <summary>牌阵总占位尺寸（含卡牌自身宽高），用于计算相机正交尺寸。</summary>
        public Vector2 GetBoardWorldSize()
        {
            Vector2 leftBottom = GetPosition(0, 0);
            Vector2 rightTop = GetPosition(rows - 1, cols - 1);
            return new Vector2(
                (rightTop.x - leftBottom.x) + cardWorldSize.x,
                (rightTop.y - leftBottom.y) + cardWorldSize.y);
        }

        /// <summary>
        /// 在 Scene 视图画出 12 个卡牌落点框，便于对齐相机取景（选中 Board 物体时可见）。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 1f);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Gizmos.DrawWireCube(GetPosition(i, j), cardWorldSize);
                }
            }
        }
    }
}
