using System.Collections;
using UnityEngine;

namespace MemoryCard
{
    /// <summary>
    /// 背景音乐播放器：播放一段**循环**的 BGM，并提供「胜利时压低音量」的接口。
    ///
    /// 【为什么要单独挂一个物体、独立一个 AudioSource】
    /// 翻牌、配对成功、配对失败、胜利这四个短音由 Sfx 用 PlayOneShot 播放。
    /// 如果 BGM 和短音共用同一个 AudioSource，PlayOneShot 之间会互相打断，
    /// 甚至把循环中的 BGM 一起停掉。因此本组件刻意挂在独立的 Music 物体上，
    /// 与 Sfx 的 AudioSource 完全隔离 —— 两条音频通道互不干扰。
    ///
    /// 【为什么用导入的 wav 而不是运行时合成】
    /// 背景音乐要循环播放几十秒，运行时逐采样合成会占用主线程且不好循环对齐；
    /// 导入成 AudioClip 后只需把 AudioSource.loop 勾上，由音频线程无缝循环，
    /// 这也是课程里「掌握 AudioSource 组件使用」最标准的做法。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("播放背景音乐的 AudioSource，必须与本组件挂在同一个物体上")]
        [SerializeField] private AudioSource source;
        [Tooltip("循环背景音乐（Assets/Audio/bgm_loop.wav，20 秒无缝循环）")]
        [SerializeField] private AudioClip bgm;

        [Header("参数")]
        [Range(0f, 1f)]
        [Tooltip("背景音乐音量，需明显低于音效，避免盖住翻牌与配对提示音")]
        [SerializeField] private float volume = 0.18f;
        [Tooltip("开场淡入秒数，避免一进入场景就突然出声")]
        [SerializeField] private float fadeInSeconds = 1.2f;
        [Range(0f, 1f)]
        [Tooltip("胜利结算时把 BGM 压低到该倍率，让胜利音效更突出")]
        [SerializeField] private float duckMultiplier = 0.35f;

        private float _targetVolume;
        private Coroutine _fadeRoutine;

        /// <summary>供校验脚本与报告截图核对：BGM 当前是否真的在播、是否循环。</summary>
        public bool IsPlaying => source != null && source.isPlaying;
        public bool IsLooping => source != null && source.loop;

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (source == null) return;

            // 组件的三项关键配置：指定片段、开启循环、2D 播放（不随距离衰减）
            source.clip = bgm;
            source.loop = true;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.volume = 0f;                 // 从 0 开始，交给 FadeIn 抬到目标音量

            _targetVolume = volume;
        }

        private void Start()
        {
            PlayMusic();
        }

        /// <summary>开始播放（若已在播放则不重复触发），带淡入。</summary>
        public void PlayMusic()
        {
            if (source == null || source.clip == null) return;
            if (!source.isPlaying) source.Play();
            FadeTo(_targetVolume);
        }

        /// <summary>胜利结算时压低音量，避免盖住 PlayWin 的音效。</summary>
        public void SetDucked(bool ducked)
        {
            FadeTo(ducked ? _targetVolume * duckMultiplier : _targetVolume);
        }

        private void FadeTo(float target)
        {
            if (source == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(target));
        }

        /// <summary>用 unscaledDeltaTime：即使将来把 Time.timeScale 设为 0，淡入也不会卡住。</summary>
        private IEnumerator FadeRoutine(float target)
        {
            float from = source.volume;
            float duration = Mathf.Max(0.01f, fadeInSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.volume = target;
            _fadeRoutine = null;
        }
    }
}
