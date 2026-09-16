# -*- coding: utf-8 -*-
"""정산표에 박히는 도장 둘과 임대 딱지 하나를 굽는다.

## 무엇이 언제 찍히나

| | 조건 | |
|---|---|---|
| `도장_대박` | 목표 달성 + 평균 정확도 90 이상 | 그릇당 오차 0.5개 |
| `도장_명인` | 목표 달성 + 평균 정확도 99.95 이상 | **전원 완벽.** 대박 다음에 찍힌다 |
| `딱지_임대` | 목표 미달 | 도장은 하나도 안 찍고 이것만 |

평균 정확도 99.95 는 「모든 주문이 100%」와 같은 말이다 — 한 그릇이라도 안 완벽하면
평균이 거기 못 미친다. 그래서 손님 수를 따로 안 넘겨도 판정된다
(`RamenCalculator.PERFECT_ACCURACY` 와 같은 값이다).

## 왜 90 인가

`정확도 = 100 − (토핑 오차 합 / 토핑 정답 합) × 100` 이고 `판매액 = 10000 × 정확도/100` 이라
**수익률과 평균 정확도가 같은 수**다. 목표 금액도 세 구간 다 최대의 70% 다
(35000/50000 · 42000/60000 · 56000/80000). 곧 목표 달성 = 평균 70%.

토핑 정답 수는 시오·쇼유가 5, 돈코츠가 7이라 한 개 틀리면 20% / 14.3% 가 깎인다.
평균 90 은 **두 그릇에 한 번만 틀리는** 수준이다. 85 로 낮추면 세 그릇에 하나씩 틀려도
통과라 무르고, 95 는 분모가 5인 시오·쇼유에서 사실상 무결점을 요구한다.

## 그리는 규칙

* **기울기를 그림에 굽는다.** 유니티에서 `localRotation` 으로 돌리면 획이 반칸에 걸려 흐려진다.
  「주문마감」을 넉 장으로 가른 것과 같은 이유다.
* 글자 크기는 **11의 정수배만** — 11 · 22 · 33. 사이 값을 쓰면 획에 회색이 낀다.
* 잉크는 군데군데 빠뜨린다. 고무 도장은 고르게 안 묻는다. 씨앗을 고정해 다시 구워도 같다.
* 도장은 **잉크**, 딱지는 **종이**다. 재질을 갈라 놔야 잘한 날과 못한 날이 한눈에 구별된다.
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFont

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated'
FONT_PATH = 'Assets/Fonts/Galmuri11.ttf'

INK = (196, 52, 44, 255)           # 「비법」 도장과 같은 붉은색
GOLD = (212, 158, 44, 255)
GOLD_D = (150, 104, 22, 255)
SUMI = (52, 44, 40, 255)

PAPER = (238, 222, 160, 255)       # 바랜 딱지 종이
PAPER_D = (214, 194, 128, 255)
NOTICE_RED = (178, 40, 32, 255)
TAPE = (232, 232, 220, 108)


def font(px):
    return ImageFont.truetype(FONT_PATH, px)


def center(d, box, text, f, fill):
    """상자 한가운데에 글자를 놓는다. Galmuri 는 글자마다 여백이 달라 bbox 로 잡는다."""
    w = d.textlength(text, font=f)
    a = f.getbbox(text)
    d.text((box[0] + (box[2] - box[0] - w) / 2,
            box[1] + (box[3] - box[1] - (a[3] - a[1])) / 2 - a[1]), text, font=f, fill=fill)


def worn(img, seed, rate=0.13):
    """잉크가 고르게 안 묻은 느낌. 알파만 깎아 가장자리 색은 그대로 둔다."""
    rnd = random.Random(seed)
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            if px[x, y][3] > 0 and rnd.random() < rate:
                r, g, b, a = px[x, y]
                px[x, y] = (r, g, b, int(a * rnd.uniform(0.0, 0.45)))
    return img


def tilt(img, deg):
    """기울기를 그림에 굽는다. 런타임 회전 금지(위 주석 참고)."""
    return img.rotate(deg, resample=Image.BICUBIC, expand=True)


def stamp_daebak():
    """
    대박 — 둥근 붉은 인장. 가게가 꽉 찬 날 찍는 일본 가게의 「大入」에서 가져왔다.

    **가운데에 큼지막하게** 박는다. 한때 버튼 위 빈 띠(40칸)에 맞춰 78까지 줄였는데,
    그러면 숫자는 다 읽히지만 도장이 구석에 붙은 표시처럼 보여 한 방이 없었다.
    지금은 숫자를 가로지른다 — 가운데가 뚫린 고리라 밑의 글자가 비쳐 읽힌다.
    """
    S = 118
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.ellipse([3, 3, S - 4, S - 4], outline=INK, width=7)
    d.ellipse([13, 13, S - 14, S - 14], outline=INK, width=3)
    center(d, (0, 8, S, S // 2 + 3), '대박', font(33), INK)
    center(d, (0, S // 2 + 1, S, S - 10), '大入', font(33), INK)
    d.line([(24, S // 2 - 1), (S - 24, S // 2 - 1)], fill=INK, width=3)

    return tilt(worn(img, 101), 10)


def stamp_myungin():
    """
    명인의 솜씨 — 가로로 긴 붉은 검인.

    한때 세로 낙관(落款)으로 만들어 봤는데 글자를 세로로 쌓으니 「명인솜씨」로 읽혔다.
    가로 한 줄이 말이 제대로 읽힌다. 대박이 **둥근** 인장이라 이쪽은 **네모**로 두면
    둘이 나란히 찍혀도 같은 도장 두 개로 안 보인다 — 색이 아니라 모양으로 가른다.
    """
    W, H = 176, 66
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rectangle([2, 2, W - 3, H - 3], outline=INK, width=6)
    d.rectangle([11, 11, W - 12, H - 12], outline=INK, width=2)
    center(d, (0, 0, W, H), '명인의 솜씨', font(22), INK)

    return tilt(worn(img, 102), -7)


def notice_lease():
    """
    임대 딱지 — 목표를 못 채운 날. **도장이 아니라 종이다.**

    잉크(도장)와 종이(딱지)는 재질이 달라서, 잘한 날과 못한 날이 한눈에 갈린다.
    테이프 두 줄로 비뚜로 붙인 꼴로 둔다 — 반듯하면 인쇄물처럼 보여 남의 가게 같지 않다.

    **크기는 정산표에 맞춘 값이다.** 「목표 금액」(y 56)과 「당일 총 수익」(y 20) 두 줄만
    아슬아슬하게 남기고 그 아래를 통째로 덮어야 해서, 들어갈 자리가 y +4 ~ −126 의 130칸이다.
    기울기를 −13 에서 −7 로 줄인 것도 그래서다 — 많이 기울일수록 세로로 커진다.
    """
    W, H = 230, 100
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rectangle([0, 0, W - 1, H - 1], fill=PAPER, outline=SUMI, width=2)
    d.rectangle([5, 5, W - 6, H - 6], outline=NOTICE_RED, width=2)
    center(d, (0, 10, W, 62), '임 대', font(44), NOTICE_RED)
    d.line([(28, 66), (W - 28, 66)], fill=NOTICE_RED, width=2)
    center(d, (0, 68, W, 92), '문의 010-0000-0000', font(11), SUMI)

    # 종이 결·얼룩과 뜯긴 가장자리
    rnd = random.Random(103)
    px = img.load()
    for _ in range(130):
        x, y = rnd.randint(3, W - 4), rnd.randint(3, H - 4)
        if px[x, y] == PAPER:
            px[x, y] = PAPER_D
    for x in range(W):
        for y in (0, 1, H - 2, H - 1):
            if rnd.random() < 0.26:
                px[x, y] = (0, 0, 0, 0)

    out = tilt(img, -7)
    td = ImageDraw.Draw(out)
    for cx, cy in ((34, 22), (out.width - 38, out.height - 26)):
        td.rectangle([cx - 24, cy - 8, cx + 24, cy + 8], fill=TAPE)
    return out


PIECES = {
    '도장_대박': stamp_daebak,
    '도장_명인': stamp_myungin,
    '딱지_임대': notice_lease,
}


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    for name, make in PIECES.items():
        img = make()
        path = os.path.join(OUT, name + '.png')
        img.save(path)
        print('%-14s %3d x %3d  %s' % (name, img.width, img.height, path))


if __name__ == '__main__':
    main()
