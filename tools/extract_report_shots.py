# -*- coding: utf-8 -*-
"""从运行录屏中抽取报告用的 5 张截图。

用法：
    python tools/extract_report_shots.py "E:/tupian/Desktop/录屏.mp4"

做两件事：
1. 按时间点精确抽帧（-ss 必须放在 -i 之后，前置 -ss 会吸附到关键帧）；
2. 抹掉录屏软件自身的叠加层（右上角「录制已开始」气泡、右下角角标），
   用周边背景的二次曲面拟合填充，避免截图里出现与游戏无关的元素。

依赖 imageio-ffmpeg 自带的 ffmpeg 二进制、numpy、Pillow。
"""
import os
import subprocess
import sys

import numpy as np
from PIL import Image

try:
    import imageio_ffmpeg
except ImportError:  # pragma: no cover
    sys.exit("需要 imageio-ffmpeg：pip install imageio-ffmpeg")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "docs", "screenshots")

# (时间点秒, 输出文件名, 是否需要清除右上角录制气泡)
SHOTS = [
    (4.5, "20_运行截图_开始界面.png", True),
    (5.3, "21_运行截图_牌阵就绪.png", True),
    (13.0, "22_运行截图_配对成功.png", False),
    (17.0, "23_运行截图_配对失败.png", False),
    (55.5, "24_运行截图_胜利界面.png", False),
]

W, H = 2560, 1600          # 录屏原始分辨率
OUT_W, OUT_H = 1200, 750   # 报告用尺寸（与 440 × 275 pt 的插图比例一致）


def surf_fill(a, x0, x1, y0, y1, px=150, py=44, feather=6):
    """用环状邻域的二次曲面拟合填充矩形区域。

    背景是静态的柔和渐变，二次曲面在邻域残差 < 1/255，填完几乎看不出接缝。
    """
    h, w, _ = a.shape
    x0, x1 = max(0, x0), min(w - 1, x1)
    y0, y1 = max(0, y0), min(h - 1, y1)
    yy, xx = np.mgrid[0:h, 0:w]
    ring = np.zeros((h, w), bool)
    ring[max(0, y0 - py):min(h, y1 + py + 1), max(0, x0 - px):min(w, x1 + 1)] = True
    ring[y0:y1 + 1, x0:x1 + 1] = False
    X, Y = (xx - w / 2) / w, (yy - h / 2) / h
    basis = np.stack([np.ones_like(X), X, Y, X * X, X * Y, Y * Y], axis=-1)
    fill = np.zeros((y1 - y0 + 1, x1 - x0 + 1, 3))
    for c in range(3):
        coef, *_ = np.linalg.lstsq(basis[ring], a[ring][:, c], rcond=None)
        fill[:, :, c] = basis[y0:y1 + 1, x0:x1 + 1] @ coef
    hh, ww = y1 - y0 + 1, x1 - x0 + 1
    mask = np.ones((hh, ww))
    k = min(feather, hh, ww)
    ramp = np.linspace(0, 1, k)
    mask[:, :k] = ramp
    mask[:, -k:] = ramp[::-1]
    mask[:k, :] *= ramp[:, None]
    mask[-k:, :] *= ramp[::-1][:, None]
    m3 = mask[:, :, None]
    a[y0:y1 + 1, x0:x1 + 1] = a[y0:y1 + 1, x0:x1 + 1] * (1 - m3) + fill * m3
    return a


def grab(ffmpeg, src, t, tmp):
    """精确抽帧：-ss 放在 -i 之后逐帧解码，落在请求时刻而不是关键帧。"""
    cmd = [ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
           "-i", src, "-ss", str(t), "-frames:v", "1",
           "-vf", f"scale={W}:{H}", tmp]
    subprocess.run(cmd, check=True)
    return np.asarray(Image.open(tmp).convert("RGB")).astype(float)


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    src = sys.argv[1]
    if not os.path.exists(src):
        sys.exit("找不到录屏文件：%s" % src)
    os.makedirs(OUT_DIR, exist_ok=True)
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    tmp = os.path.join(OUT_DIR, "_frame.png")

    for t, name, toast in SHOTS:
        a = grab(ffmpeg, src, t, tmp)
        if toast:  # 右上角「录制已开始」气泡
            a = surf_fill(a, 2138, W - 1, 114, 292)
        a = surf_fill(a, 2486, W - 1, 1528, H - 1)  # 右下角软件角标
        img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
        img = img.resize((OUT_W, OUT_H), Image.LANCZOS)
        img.save(os.path.join(OUT_DIR, name), optimize=True)
        print("t=%5.1fs -> %s" % (t, name))

    os.remove(tmp)


if __name__ == "__main__":
    main()
