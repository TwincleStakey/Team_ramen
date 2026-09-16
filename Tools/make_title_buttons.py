# -*- coding: utf-8 -*-
"""시작 화면 버튼 — 짙은 나무 팻말에 금테와 쇠못.

## 왜 바꿨나

예전에는 `Icon.png` 에서 잘라 쓰는 **46x16 짜리 조각**을 200x44 로 늘려 썼다.
가로 4.3배 · 세로 2.75배라 **나뭇결이 뭉개져 색면**이 됐고, 색까지 밝아서 밤 배경 위에
혼자 떠 있었다. 바로 위 로고는 짙은 나무 간판에 금색 붓글씨인데 버튼만 딴 게임 UI 였다.

나무라는 재질은 그대로 두고 금테를 더했다. 로고와 한 세트로 읽힌다.

## 9-슬라이스로 굽는다

64x44 타일에 테두리 (14, 12, 14, 12) 다. 가운데만 늘어나고 좌우 14칸은 원본 그대로 찍힌다.

  **세로 44 는 쓰는 크기와 같다.** 배율이 1이라 나뭇결이 세로로 안 늘어난다.
  **쇠못은 좌우 테두리 영역(14칸) 안에 둔다.** 그래야 가로로 늘려도 양 끝에 그대로 남는다.
     가운데에 두면 늘어나면서 못이 길쭉한 타원으로 뭉개진다.

## 눌린 그림

유니티 Button 의 SpriteSwap 에 물린다(빌더의 `MakeWoodButton`). 색만 어둡게 하는
ColorTint 와 달리, 눌리면 **빛과 그늘이 위아래로 뒤집혀** 실제로 눌려 들어간 모양이 된다.
"""
import os
import random
import sys

from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated'

W, H = 64, 44
RADIUS = 4

GOLD = (214, 172, 84, 255)
GOLD_DIM = (150, 110, 46, 255)


def grain(d, x0, y0, x1, y1, base, seed, strength=10):
    """가로로 흐르는 나뭇결. 결이 없으면 나무가 아니라 색종이로 보인다."""
    rnd = random.Random(seed)
    y = y0
    while y < y1:
        k = rnd.randint(1, 3)
        s = rnd.randint(-strength, strength)
        c = (max(0, min(255, base[0] + s)),
             max(0, min(255, base[1] + int(s * 0.8))),
             max(0, min(255, base[2] + int(s * 0.6))), 255)
        d.rectangle([x0, y, x1, min(y1, y + k) - 1], fill=c)
        y += k


def plate(base, lit, dark, ring, pressed):
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=RADIUS, fill=base)
    grain(d, 2, 2, W - 2, H - 2, base, 7)

    if pressed:
        d.rectangle([2, 2, W - 3, 7], fill=dark)        # 눌리면 위가 그늘진다
        d.rectangle([2, H - 6, W - 3, H - 3], fill=lit)
    else:
        d.rectangle([2, 2, W - 3, 6], fill=lit)         # 빛은 위에서 온다
        d.rectangle([2, H - 7, W - 3, H - 3], fill=dark)

    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=RADIUS, outline=ring, width=2)
    return img


def nails(img, color, shine):
    """네 귀퉁이 쇠못. 좌우 테두리 영역(14칸) 안이라 늘려도 안 뭉개진다."""
    d = ImageDraw.Draw(img)
    for x in (7, W - 8):
        for y in (8, H - 9):
            d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=color)
            d.point((x - 1, y - 1), fill=shine)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)

    normal = nails(plate((88, 52, 28, 255), (120, 74, 40, 255), (54, 30, 15, 255), GOLD, False),
                   (120, 96, 52, 255), (214, 180, 110, 255))
    normal.save(os.path.join(OUT, '버튼_타이틀.png'))
    print('버튼_타이틀      %dx%d  (테두리 14/12)' % normal.size)

    down = nails(plate((66, 39, 21, 255), (92, 56, 30, 255), (42, 23, 12, 255), GOLD_DIM, True),
                 (96, 76, 42, 255), (170, 142, 88, 255))
    down.save(os.path.join(OUT, '버튼_타이틀_눌림.png'))
    print('버튼_타이틀_눌림 %dx%d' % down.size)


if __name__ == '__main__':
    main()
