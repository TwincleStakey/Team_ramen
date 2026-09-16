# -*- coding: utf-8 -*-
"""설정창 조각들 — 판·명패·화살표·눈금틀.

## 왜 짙은 나무에 금테인가

같은 창을 **시작 화면(밤거리)** 과 **조리 화면(밝은 나무 카운터)** 두 곳에서 쓴다.

  종이(결과창·정산표 가족)   밝은 카운터 위에서 묻힌다
  어두운 반투명(튜토리얼 안내) 밤거리 위에서 묻힌다
  짙은 나무 + 금테            둘 다에서 뜬다 — 금테가 어느 배경에서도 윤곽을 만든다

게다가 마무리 버튼·시작화면 버튼·로고 간판이 이미 이 가족이라 새 언어를 안 만들어도 된다.

## 9-슬라이스

판과 눈금틀은 크기가 바뀔 수 있어 작은 타일로 굽는다. 명패와 화살표는 크기가 고정이라
통짜로 굽는다 — 9-슬라이스로 만들면 오히려 가운데가 늘어나 결이 흐려진다.
"""
import os
import random
import sys

from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated'

GOLD = (214, 172, 84, 255)
GOLD_DIM = (150, 110, 46, 255)
WOOD = (88, 52, 28, 255)
WOOD_LIT = (120, 74, 40, 255)
WOOD_DARK = (54, 30, 15, 255)
INK = (244, 220, 170, 255)
WELL = (30, 20, 15, 235)        # 눈금이 들어앉는 우묵한 자리


def grain(d, x0, y0, x1, y1, base, seed, strength=9):
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


def panel():
    """설정판. 48x48 타일, 테두리 16. 쇠못은 테두리 영역 안이라 늘려도 네 귀퉁이에 남는다."""
    w = h = 48
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, fill=WOOD)
    grain(d, 3, 3, w - 3, h - 3, WOOD, 11)
    d.rectangle([4, 4, w - 5, 9], fill=WOOD_LIT)
    d.rectangle([4, h - 10, w - 5, h - 5], fill=WOOD_DARK)

    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, outline=GOLD, width=3)
    d.rounded_rectangle([6, 6, w - 7, h - 7], radius=4, outline=GOLD_DIM, width=1)

    for x in (13, w - 14):
        for y in (13, h - 14):
            d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=(120, 96, 52, 255))
            d.point((x - 1, y - 1), fill=(214, 180, 110, 255))
    return img


def plaque(w=180, h=44):
    """제목 명패. 양끝을 깎아 정확도 명패와 같은 결로 맞췄다. 글자는 TMP 가 쓴다."""
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cut = 8
    pts = [(cut, 0), (w - cut, 0), (w - 1, h // 2), (w - cut, h - 1), (cut, h - 1), (0, h // 2)]
    d.polygon(pts, fill=(38, 26, 20, 255))
    d.polygon(pts, outline=GOLD, width=2)
    return img


def arrow(flip, pressed=False):
    """한 칸 옮기는 화살표. 28x28 고정이라 통짜로 굽는다."""
    s = 28
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    face = (28, 19, 14, 255) if pressed else (40, 27, 20, 255)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=3, fill=face,
                        outline=(GOLD_DIM if pressed else GOLD), width=2)
    c = s // 2
    tip = INK if not pressed else (190, 166, 126, 255)
    pts = [(c + 3, c - 5), (c + 3, c + 5), (c - 4, c)] if flip \
        else [(c - 3, c - 5), (c - 3, c + 5), (c + 4, c)]
    d.polygon(pts, fill=tip)
    return img


def well():
    """눈금이 들어앉는 우묵한 틀. 16x22 타일, 테두리 5."""
    w, h = 16, 22
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=3, fill=WELL, outline=GOLD_DIM, width=1)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)

    p = panel()
    p.save(os.path.join(OUT, '설정판.png'))
    print('설정판      %dx%d  (테두리 16)' % p.size)

    q = plaque()
    q.save(os.path.join(OUT, '설정명패.png'))
    print('설정명패    %dx%d' % q.size)

    for name, flip, pressed in (('버튼_왼쪽', True, False), ('버튼_왼쪽_눌림', True, True),
                                ('버튼_오른쪽', False, False), ('버튼_오른쪽_눌림', False, True)):
        a = arrow(flip, pressed)
        a.save(os.path.join(OUT, name + '.png'))
        print('%-16s %dx%d' % (name, a.size[0], a.size[1]))

    wl = well()
    wl.save(os.path.join(OUT, '눈금틀.png'))
    print('눈금틀      %dx%d  (테두리 5)' % wl.size)


if __name__ == '__main__':
    main()
