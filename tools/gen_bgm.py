# -*- coding: utf-8 -*-
"""
生成《记忆翻牌》背景音乐 bgm_loop.wav（20 秒，无缝循环）。

设计要点（实验报告中会用到）：
1. 长度取 BPM 96 下的 8 小节（4/4 拍），即 20.00 s，采样数 = 22050 * 20 整除，循环点落在小节线上；
2. 每小节 8 个八分音符走分解和弦（C - G - Am - F 四和弦各两小节），叠加低音根音；
3. 音符尾音（1.2 s）超过缓冲区末尾时，把溢出部分「绕回」到缓冲区开头叠加，
   这才是真正的循环点无缝 —— 相当于上一轮循环的余音仍然在响，不需要淡入淡出掩码；
4. 16 bit / 单声道 / 22050 Hz，20 s 约 0.86 MB，对 exe 包体无实质影响。
"""
import math
import wave
import struct

SR = 22050
BPM = 96.0
BEAT = 60.0 / BPM          # 0.625 s
BARS = 8
BEATS = BARS * 4           # 32 拍
DUR = BEATS * BEAT         # 20.0 s
N = int(round(SR * DUR))
TAIL = int(SR * 1.5)       # 尾音溢出池，必须长于单个音符
BUF = [0.0] * (N + TAIL)

NOTE_DUR = 1.2             # 单音符渲染时长（远长于八分音符间隔，让余音自然重叠）


def midi_hz(m):
    return 440.0 * (2.0 ** ((m - 69) / 12.0))


def add_note(start_sec, midi, amp, decay, harmonics):
    """把一个带泛音与指数衰减的音频叠加进 BUF；溢出缓冲区末尾的部分绕回开头。"""
    start = int(round(start_sec * SR))
    n = int(NOTE_DUR * SR)
    f = midi_hz(midi)
    attack = int(0.006 * SR)          # 6 ms 起音，避免咔哒声
    w = 2.0 * math.pi * f / SR
    for i in range(n):
        idx = start + i
        if idx >= N + TAIL:
            break
        t = i / SR
        env = math.exp(-t / decay)
        if i < attack:
            env *= i / attack          # 线性起音
        s = 0.0
        for k, g in enumerate(harmonics, start=1):
            s += g * math.sin(w * k * i)
        v = s * env * amp
        if idx < N:
            BUF[idx] += v
        else:
            BUF[idx - N] += v          # 绕回开头 —— 循环点无缝的关键


# 四个和弦（MIDI 音高）：C 大调 I - V - vi - IV
CHORDS = [
    dict(tones=[60, 64, 67, 72], bass=36),   # C  C4 E4 G4 C5   bass C2
    dict(tones=[67, 71, 74, 79], bass=43),   # G  G4 B4 D5 G5   bass G2
    dict(tones=[69, 72, 76, 81], bass=45),   # Am A4 C5 E5 A5   bass A2
    dict(tones=[65, 69, 72, 77], bass=41),   # F  F4 A4 C5 F5   bass F2
]

# 每小节 8 个八分音符的分解和弦走向：根 - 三 - 五 - 八 - 五 - 三 - 五 - 根
PATTERN = [0, 1, 2, 3, 2, 1, 2, 0]

MELODY_H = [1.0, 0.34, 0.14]     # 八音盒音色：基频 + 两个泛音
BASS_H = [1.0, 0.25]

for bar in range(BARS):
    chord = CHORDS[(bar // 2) % 4]
    bar_start = bar * 4 * BEAT
    # 主旋律：八分音符
    for step in range(8):
        t = bar_start + step * (BEAT / 2.0)
        midi = chord["tones"][PATTERN[step]]
        # 强弱：每拍第一个音稍重，制造 4/4 拍的律动
        amp = 0.20 if step % 2 == 0 else 0.13
        add_note(t, midi, amp, 0.30, MELODY_H)
    # 低音：每小节第 1、3 拍
    for beat in (0, 2):
        add_note(bar_start + beat * BEAT, chord["bass"], 0.26, 0.55, BASS_H)

# 归一化到 -6 dBFS，留出余量避免与其他音效叠加时削波
peak = max(abs(v) for v in BUF[:N]) or 1.0
gain = 0.5 / peak
out = [v * gain for v in BUF[:N]]

# 采样级检查循环点连续性（相邻样本差值应远小于满量程）
jump = abs(out[0] - out[-1])
print("N =", N, " 时长 = %.3f s" % (N / SR))
print("峰值 = %.3f  →  归一化后 %.3f" % (peak, max(abs(v) for v in out)))
print("循环点 |s[0]-s[N-1]| = %.5f （满量程 1.0，越小越无缝）" % jump)
print("首尾样本: s[0]=%.5f  s[N-1]=%.5f" % (out[0], out[-1]))

path = r"E:/unitwork/Memory Card/Assets/Audio/bgm_loop.wav"
import os
os.makedirs(os.path.dirname(path), exist_ok=True)
with wave.open(path, "wb") as wf:
    wf.setnchannels(1)
    wf.setsampwidth(2)
    wf.setframerate(SR)
    wf.writeframes(b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in out))
print("written:", path, "%.2f MB" % (os.path.getsize(path) / 1024 / 1024))
