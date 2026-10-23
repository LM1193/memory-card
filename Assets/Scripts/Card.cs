using System;
using System.Collections;
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
    ///
    /// 【视觉表现】翻面用 X 轴缩放做「卡片绕竖轴翻转」的近似动画：
    ///   缩放 x 从 1 → 0 时换图，再从 0 → 1，视觉上等价于翻牌。
    ///   动画与状态判定完全解耦 —— 状态在方法调用时立即变更，
    ///   动画只影响观感，不影响 GameManager 的配对逻辑。
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

        [Header("动画参数")]
        [Tooltip("单次翻面动画时长（秒）；设为 0 则瞬间切换")]
        [SerializeField] private float flipDuration = 0.16f;
        [Tooltip("配对成功时的脉冲放大倍率")]
        [SerializeField] private float matchedPulseScale = 1.08f;
        [Tooltip("配对成功脉冲时长（秒）")]
        [SerializeField] private float matchedPulseDuration = 0.28f;

        /// <summary>牌面类型 ID，等于 images 数组中的索引（课件第 11 页）。</summary>
        [HideInInspector] public int id = -1;

        private SpriteRenderer _renderer;
        private Sprite _faceSprite;
        private Sprite _backSprite;
        private Action<Card> _onClicked;

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _flipRoutine;
        private Coroutine _pulseRoutine;
        /// <summary>正在翻向的目标贴图；非空表示有一次翻面在动画中途，尚未提交</summary>
        private Sprite _pendingSprite;

        public State CurrentState => state;
        public bool IsClickable => state == State.Hidden;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
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

            _baseScale = transform.localScale;
            StopAnimations();

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
            AnimateTo(_faceSprite);
        }

        /// <summary>翻回：正面 → 背面（配对失败时由协程调用）。已配对的牌不会被翻回。</summary>
        public void Hide()
        {
            if (state == State.Matched) return;
            state = State.Hidden;
            AnimateTo(_backSprite);
        }

        /// <summary>标记为配对成功：锁定，之后不可再点击，并给一次放大脉冲作为正反馈。</summary>
        public void SetMatched()
        {
            state = State.Matched;

            // 配对判定可能发生在第二张牌的翻面动画途中，先统一收尾再脉冲，避免两个协程抢 localScale
            StopAnimations();
            if (isActiveAndEnabled) _pulseRoutine = StartCoroutine(PulseRoutine());
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
        ///
        /// 【触屏补充】WebGL 在手机上打开时走触摸输入，必须用 fingerId 重载判断，
        /// 无参重载只对应鼠标指针（id = -1），会把 UI 上的触摸误判为点在卡牌上。
        /// </summary>
        private void OnMouseDown()
        {
            if (EventSystem.current != null)
            {
                bool overUI = Input.touchCount > 0
                    ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                    : EventSystem.current.IsPointerOverGameObject();

                if (overUI) return;
            }

            OnClick();
        }

        // ---------------------------------------------------------------- 动画

        private void AnimateTo(Sprite target)
        {
            // 新目标会覆盖旧目标，因此这里直接掐断旧协程，不做提交
            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
                _flipRoutine = null;
            }
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (!isActiveAndEnabled || flipDuration <= 0.001f)
            {
                transform.localScale = _baseScale;
                _pendingSprite = null;
                if (_renderer != null) _renderer.sprite = target;
                return;
            }

            _pendingSprite = target;
            _flipRoutine = StartCoroutine(FlipRoutine(target));
        }

        private IEnumerator FlipRoutine(Sprite target)
        {
            float half = flipDuration * 0.5f;

            // 1) 收缩到 x = 0
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / half);
                transform.localScale = new Vector3(_baseScale.x * (1f - k), _baseScale.y, _baseScale.z);
                yield return null;
            }
            transform.localScale = new Vector3(0f, _baseScale.y, _baseScale.z);

            // 2) 在最薄的一帧换图（此时观众看不出切换）
            if (_renderer != null) _renderer.sprite = target;
            _pendingSprite = null;

            // 3) 展开回原尺寸
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / half);
                transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y, _baseScale.z);
                yield return null;
            }

            transform.localScale = _baseScale;
            _flipRoutine = null;
        }

        private IEnumerator PulseRoutine()
        {
            transform.localScale = _baseScale;

            for (float t = 0f; t < matchedPulseDuration; t += Time.deltaTime)
            {
                float k = t / matchedPulseDuration;
                float wave = Mathf.Sin(k * Mathf.PI);                       // 0 → 1 → 0
                float scale = Mathf.Lerp(1f, matchedPulseScale, wave);
                transform.localScale = _baseScale * scale;
                yield return null;
            }

            transform.localScale = _baseScale;
            _pulseRoutine = null;
        }

        /// <summary>
        /// 立刻停止所有动画并把尺寸复位。
        /// 关键：若此刻正处在一次翻面的中途（贴图还没换），必须**先提交目标贴图**再停，
        /// 否则卡牌会被永久锁定在旧贴图上（配对瞬间打断动画就会踩到这个坑）。
        /// </summary>
        private void StopAnimations()
        {
            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
                _flipRoutine = null;

                if (_pendingSprite != null)
                {
                    if (_renderer != null) _renderer.sprite = _pendingSprite;
                    _pendingSprite = null;
                }
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            transform.localScale = _baseScale;
        }

        public override string ToString()
        {
            return $"[Card id={id} state={state} pos={transform.position}]";
        }
    }
}
