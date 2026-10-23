#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成《记忆翻牌》新增牌面素材：星形（star）与十字（cross）。

设计约束全部从既有 6 张素材实测反推，保证风格 1:1 一致：
  · 画布      150 × 200 RGBA
  · 牌面底色  (255, 242, 212)  #FFF2D4
  · 卡牌轮廓  直接复用 square-symbol.png 的 alpha 通道（圆角与抗锯齿完全一致）
  · 符号墨量  目标 5000–6000 px，落在既有 circle(4797) / heart(5813) / diamond(6463) 区间内
  · 配色      star  #C97A00 深琥珀（对奶油底对比度 3.02，且与月牙橙靠明度拉开）
              cross #00A0BE 青（色相 189.5°，落在已用色相第二大空隙 156.8°→225.7° 的中点）

用法： python tools/gen_symbols.py
输出： Assets/Art/star-symbol.png、Assets/Art/cross-symbol.png
"""

import math
import os

from PIL import Image, ImageDraw

ART = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Art")
REF = os.path.join(ART, "square-symbol.png")   # 借它的 alpha 作为卡牌轮廓
W, H = 150, 200
CREAM = (255, 242, 212, 255)
SS = 4                                          # 超采样倍数，用于边缘抗锯齿
CX, CY = W / 2, H / 2

STAR_COLOR = (201, 122, 0)                      # #C97A00
CROSS_COLOR = (0, 160, 190)                     # #00A0BE


def card_mask() -> Image.Image:
    """复用既有素材的卡牌轮廓 alpha，保证圆角/毛边与其余 6 张完全一致。"""
    return Image.open(REF).convert("RGBA").split()[3]


def star_points(cx, cy, r_out, ratio=0.48, points=5):
    """正五角星顶点：交替外/内半径，首个外点朝正上方（-90°）。"""
    r_in = r_out * ratio
    pts = []
    for k in range(points * 2):
        ang = -math.pi / 2 + k * math.pi / points
        r = r_out if k % 2 == 0 else r_in
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return pts


def polygon_area(pts) -> float:
    n = len(pts)
    return abs(sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1]
                   for i in range(n))) / 2.0


def draw_shape(polys) -> Image.Image:
    """把若干多边形画成 150×200 的抗锯齿单通道遮罩。"""
    big = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(big)
    for poly in polys:
        d.polygon([(x * SS, y * SS) for x, y in poly], fill=255)
    return big.resize((W, H), Image.LANCZOS)


def compose(symbol_mask: Image.Image, color) -> Image.Image:
    card = Image.new("RGBA", (W, H), CREAM)
    card.paste(Image.new("RGBA", (W, H), color + (255,)), (0, 0), symbol_mask)
    card.putalpha(card_mask())                  # 套用既有卡牌轮廓
    # 透明区域统一填底色，避免 Unity 双线性过滤出现暗边
    px = card.load()
    for y in range(H):
        for x in range(W):
            if px[x, y][3] == 0:
                px[x, y] = (CREAM[0], CREAM[1], CREAM[2], 0)
    return card


def main():
    # ---- 星形：高度 = R*(1+cos36)，宽度 = 2R*sin72，整体居中于卡面 ----
    R = 56.0
    top_offset = R * (1 - math.cos(math.radians(36))) / 2.0   # 使 bbox 垂直居中于 CY
    star = star_points(CX, CY + top_offset, R)
    star_mask = draw_shape([star])
    print("star  R=%.1f  area=%.0f px  " % (R, polygon_area(star)))

    # ---- 十字：两臂等长，交叉部不重复计面积 ----
    L, T = 104.0, 34.0
    cross = [
        (CX - T / 2, CY - L / 2), (CX + T / 2, CY - L / 2),
        (CX + T / 2, CY - T / 2), (CX + L / 2, CY - T / 2),
        (CX + L / 2, CY + T / 2), (CX + T / 2, CY + T / 2),
        (CX + T / 2, CY + L / 2), (CX - T / 2, CY + L / 2),
        (CX - T / 2, CY + T / 2), (CX - L / 2, CY + T / 2),
        (CX - L / 2, CY - T / 2), (CX - T / 2, CY - T / 2),
    ]
    cross_mask = draw_shape([cross])
    print("cross L=%.0f T=%.0f  area=%.0f px" % (L, T, L * T * 2 - T * T))

    out = [("star-symbol.png", star_mask, STAR_COLOR),
           ("cross-symbol.png", cross_mask, CROSS_COLOR)]
    for name, mask, color in out:
        img = compose(mask, color)
        path = os.path.join(ART, name)
        img.save(path, "PNG")
        # 统计实际墨量，便于与既有素材比对
        ink = sum(1 for p in img.getdata()
                  if p[3] > 128 and abs(p[0] - CREAM[0]) + abs(p[1] - CREAM[1]) + abs(p[2] - CREAM[2]) > 24)
        print("wrote %-18s ink=%d px" % (name, ink))


if __name__ == "__main__":
    main()
