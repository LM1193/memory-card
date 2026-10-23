using UnityEngine;

namespace MemoryCard
{
    /// <summary>
    /// 程序化音效：不依赖任何外部音频文件，运行时用正弦波 + 指数衰减包络合成。
    /// 好处：WebGL 包体增量为 0 字节，也不会出现「素材缺失」这类交付问题。
    ///
    /// 使用方式：GameManager.Awake 中调用一次 Sfx.Init(gameObject)，
    /// 之后任意位置调用 Sfx.PlayFlip() / PlayMatch() / PlayMismatch() / PlayWin()。
    ///
    /// 浏览器策略：WebGL 上音频必须由用户交互触发才能播放；
    /// 本游戏的第一次音效发生在点击 Start 按钮之后，满足该限制。
    /// </summary>
    public static class Sfx
    {
        private const int SampleRate = 44100;

        private const float _volume = 0.22f;

        private static AudioSource _source;
        private static AudioClip _flip;
        private static AudioClip _match;
        private static AudioClip _mismatch;
        private static AudioClip _win;

        public static void Init(GameObject host)
        {
            if (_source != null || host == null) return;

            _source = host.GetComponent<AudioSource>();
            if (_source == null) _source = host.AddComponent<AudioSource>();

            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;          // 纯 2D，不做空间衰减
            _source.volume = _volume;

            _flip = MakeTone("sfx_flip", 620f, 900f, 0.055f, 0.9f);
            _match = MakeTone("sfx_match", 880f, 1320f, 0.16f, 1f);
            _mismatch = MakeTone("sfx_mismatch", 300f, 170f, 0.14f, 0.8f);
            _win = MakeTone("sfx_win", 660f, 1320f, 0.42f, 1f);
        }

        public static void PlayFlip() { Play(_flip); }
        public static void PlayMatch() { Play(_match); }
        public static void PlayMismatch() { Play(_mismatch); }
        public static void PlayWin() { Play(_win); }

        private static void Play(AudioClip clip)
        {
            if (_source == null || clip == null) return;
            _source.PlayOneShot(clip, _volume);
        }

        /// <summary>
        /// 合成一个「音高滑动 + 指数衰减」的短音。
        /// 相位用累加方式推进，避免频率变化时出现相位跳变导致的爆音。
        /// </summary>
        private static AudioClip MakeTone(string name, float freqStart, float freqEnd, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            var data = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)sampleCount;
                float freq = Mathf.Lerp(freqStart, freqEnd, progress);

                phase += 2f * Mathf.PI * freq / SampleRate;

                // (1 - progress)^3 的衰减包络：起音干净、收尾不拖尾
                float envelope = Mathf.Pow(1f - progress, 3f);
                data[i] = Mathf.Sin(phase) * envelope * amplitude;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
