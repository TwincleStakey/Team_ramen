# -*- coding: utf-8 -*-
"""조리 그릇 위로 피어오르는 김.

## 왜 따로 굽나

손님 앞 그릇은 김이 **그림에 구워져** 있다(`손님그릇.png` 네 칸). 옆에서 본 각도라
그릇 뒤로 김이 올라가면 되기 때문이다.

조리 화면 그릇은 **위에서 내려다본 각도**다. 같은 방식으로 구우면 국물 그림(타래 4종 x
붓기 8칸 + 찰랑임)을 전부 다시 구워야 한다 — 수십 장이다. 김만 따로 떼어 얹는다.

## 줄기가 아니라 아지랑이다

처음에는 김 줄기 셋을 타원으로 쌓아 올렸다. **너무 또렷했다** — 모양이 잡힌 연기 세 가닥이
올라가는 것으로 보이지, 뜨거운 국물에서 김이 오르는 것으로 안 보였다.

지금은 **그릇 폭 전체에 깔린 옅은 노이즈**를 위로 흘려보낸다. 형태가 없고 농담만 있다.
가까이 보면 아무 모양도 아니고, 멀리서 보면 뜨겁다.

## 이어지는 루프

노이즈를 정수 주파수 사인의 합으로 만든다. 그러면 세로로 H 만큼 밀었을 때 원래와 같아진다.
한 칸에 H/FRAMES 씩 밀면 마지막 장 다음이 첫 장과 정확히 맞물린다 — 루프에서 안 튄다.

## 은은해야 한다

조리 화면은 계속 보는 화면이라 김이 세면 금방 거슬리고, 무엇보다 **국물 위 재료를 가린다.**
알파를 낮게 깔고 위로 갈수록 사라지게 한다.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/조리'

W, H = 128, 96          # 그릇 폭과 같고, 그릇 위로 96칸 올라간다
FRAMES = 8
PEAK_ALPHA = 250         # 제일 진한 자리의 알파(255 중). 이 이상은 재료를 가린다
COLOR = (255, 250, 240)
BLUR = 3.0

# 옅은 쪽을 얼마나 더 누를지. 1 이면 노이즈 그대로고, 높을수록 진한 덩어리만 남는다.
# 2 로 했더니 화면에서 아예 안 보였다.
GAMMA = 1.4

# 겹치는 물결 — (가로 주기, 세로 주기, 세기, 위상)
#
# **가로 주기를 세로보다 크게 둔다.** 그래야 세로로 길쭉한 결이 생겨서 「올라간다」로 읽힌다.
# 처음에 둘을 비슷하게(1~3) 뒀더니 세로 결이 없어 가로로 뜬 빛 띠처럼 보였다.
# 그렇다고 너무 잘게 쪼개면 지글지글 끓는 무늬가 된다 — 7 이 위쪽 한계다.
WAVES = (
    (2, 1, 1.00, 0.00),
    (3, 1, 0.80, 1.90),
    (5, 2, 0.50, 3.40),
    (4, 3, 0.35, 5.10),
    (7, 2, 0.25, 0.70),
)


def field(shift):
    """세로로 shift 만큼 흐른 노이즈. 값은 대략 -1~1."""
    x = np.arange(W, dtype=np.float32)[None, :] / W
    y = (np.arange(H, dtype=np.float32)[:, None] + shift) / H

    out = np.zeros((H, W), dtype=np.float32)
    for fx, fy, amp, phase in WAVES:
        out += amp * np.sin(2 * math.pi * (fx * x + fy * y) + phase)

    return out / sum(w[2] for w in WAVES)


def envelope():
    """어디가 진한가. 아래(국물 쪽)가 진하고 위로 갈수록 사라진다."""
    t = np.linspace(0.0, 1.0, H, dtype=np.float32)[:, None]     # 0 위 ~ 1 아래
    up = t ** 1.25                                              # 위로 갈수록 옅어진다

    # 맨 아랫줄은 조금 죽인다. 김이 어디선가 뚝 시작하는 것처럼 보이지 않게.
    up = up * np.clip((1.0 - t) * 6.0 + 0.55, 0.0, 1.0)

    # 좌우 끝도 죽인다. 안 죽이면 판 모서리에 네모난 자국이 남는다.
    x = np.linspace(0.0, 1.0, W, dtype=np.float32)[None, :]
    side = np.clip(np.sin(x * math.pi) * 1.8, 0.0, 1.0)

    return up * side


ENV = envelope()


def frame(n):
    # 노이즈를 0~1 로 옮기고 감마를 먹여 옅은 쪽을 더 눌러 둔다.
    # 이걸 안 하면 화면 전체에 뿌연 막이 한 겹 씌워진 것처럼 보인다.
    v = np.clip(field(n * H / float(FRAMES)) * 0.5 + 0.5, 0.0, 1.0) ** GAMMA

    a = (v * ENV * PEAK_ALPHA).astype(np.uint8)

    img = np.zeros((H, W, 4), dtype=np.uint8)
    img[:, :, 0], img[:, :, 1], img[:, :, 2] = COLOR
    img[:, :, 3] = a

    return Image.fromarray(img, 'RGBA').filter(ImageFilter.GaussianBlur(BLUR))


def main():
    os.makedirs(OUT, exist_ok=True)

    sheet = Image.new('RGBA', (W * FRAMES, H), (0, 0, 0, 0))
    for n in range(FRAMES):
        sheet.paste(frame(n), (W * n, 0))

    path = os.path.join(OUT, '그릇김.png')
    sheet.save(path)

    a = np.asarray(sheet)[:, :, 3]
    print('%s  %dx%d (%d칸)  알파 최대 %d / 평균 %.1f'
          % (path, sheet.width, sheet.height, FRAMES, a.max(), a[a > 0].mean()))


if __name__ == '__main__':
    main()
