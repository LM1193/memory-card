using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MemoryCard
{
    /// <summary>
    /// 单张卡牌：负责自身的三种状态与翻面表现。
    ///
    /// 【路线 B：世界坐标方案 —— 对齐课件】
    /// 卡牌是场景中的真实物体（SpriteRenderer），不是 UGUI 元素。
    /// 挂载位置：Card.prefab 根物体，该物体需带 SpriteRenderer + BoxCollider2D。
    /// 点击响应：OnMouseDown()（由 2D 碰撞体 + 相机射线自动触发，无需 UGUI Button）
    ///
    /// 对应课件：
    ///   第 11 页 —— id 即 numbers 数组中的元素值（= images 数组的索引）
    ///   第 12 页 —— 三种状态：未被翻开 / 翻开 / 配对成功
    ///   第 13 页 —— 配对判定依据 id 是否相等
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Card : MonoBehaviour
    {
        /// <summary>课件第 12 页：卡牌的三种状态。</summary>
        public enum State
        {
            Hidden,     // 未被翻开
            Revealed,   // 翻开状态
            Matched     // 配对成功（锁定）
        }

        [Header("运行时状态（只读，仅供调试观察）")]
        [SerializeField] private State state = State.Hidden;

        /// <summary>牌面类型 ID，等于 images 数组中的索引（课件第 11 页）。</summary>
        [HideInInspector] public int id = -1;

        private SpriteRenderer _renderer;
        private Sprite _faceSprite;
        private Sprite _backSprite;
        private Action<Card> _onClicked;

        public State CurrentState => state;
        public bool IsClickable => state == State.Hidden;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 初始化卡牌。由 BoardManager 在生成卡牌后调用。
        /// </summary>
        /// <param name="cardId">牌面类型 ID（images 的索引）</param>
        /// <param name="face">正面牌面图</param>
        /// <param name="back">背面卡背图</param>
        /// <param name="sortingOrder">渲染层级（必须大于背景的 sortingOrder）</param>
        /// <param name="onClicked">点击回调</param>
        public void Init(int cardId, Sprite face, Sprite back, int sortingOrder, Action<Card> onClicked)
        {
            id = cardId;
            _faceSprite = face;
            _backSprite = back;
            _onClicked = onClicked;

            state = State.Hidden;

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = _backSprite;
            _renderer.sortingOrder = sortingOrder;

            // 卡牌不参与物理运动，碰撞体只用于接收鼠标点击
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            col.isTrigger = false;
            Rigidbody2D rb = col.attachedRigidbody;
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }

        /// <summary>翻开：背面 → 正面。仅"未被翻开"状态可执行。</summary>
        public void Reveal()
        {
            if (state != State.Hidden) return;
            state = State.Revealed;
            _renderer.sprite = _faceSprite;
        }

        /// <summary>翻回：正面 → 背面（配对失败时由协程调用）。已配对的牌不会被翻回。</summary>
        public void Hide()
        {
            if (state == State.Matched) return;
            state = State.Hidden;
            _renderer.sprite = _backSprite;
        }

        /// <summary>标记为配对成功：锁定，之后不可再点击。</summary>
        public void SetMatched()
        {
            state = State.Matched;
        }

        /// <summary>点击入口。已翻开或已配对的牌直接忽略（防连点的第一道闸）。</summary>
        public void OnClick()
        {
            if (!IsClickable) return;
            _onClicked?.Invoke(this);
        }

        /// <summary>
        /// Unity 消息：鼠标/触屏点击到本物体碰撞体时自动触发。
        /// 前置条件：相机为正交（Orthographic）、卡牌带 Collider2D、且被点击处没有 UI 遮挡。
        ///
        /// ⚠ 重要：UGUI 并**不会**自动拦截 OnMouseDown —— 点击 UI 面板时，
        /// 事件会「穿透」到面板下方的卡牌碰撞体。因此必须手动判断：
        /// 指针是否停在 UI 上（面板/按钮），是则不处理本次点击。
        /// 这也是为什么 StartPanel / WinPanel 的 Image 要**保留** Raycast Target 勾选。
        /// </summary>
        private void OnMouseDown()
        {
            // 指针位于任意 UI 元素（Canvas 上的面板、按钮、文本）之上时，忽略世界物体的点击
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            OnClick();
        }

        public override string ToString()
        {
            return $"[Card id={id} state={state} pos={transform.position}]";
        }
    }
}
