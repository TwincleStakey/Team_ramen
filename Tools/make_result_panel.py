# -*- coding: utf-8 -*-
"""손님 한 명분 결과창(주문 결과 팝업)의 판과 정확도 명패를 굽는다.

## 왜

라멘을 낼 때마다 뜨는 창인데 **게임에서 제일 자주 보는 팝업**이다. 그런데 크림색 사각판
(`Hex("#FFF8E7")` + `TextBox.png`)에 주황 막대를 얹은 것이라, 하루 정산(영수증)·5일 결산(장부)과
결이 달랐다. 같은 종이 가족으로 맞춘다.

## 셋이 서로 달라야 한다

종이 셋이 다 같으면 어느 창인지 헷갈린다. 가장자리 모양으로 가른다.

| | 무엇 | 가장자리 |
|---|---|---|
| 정산표 | 하루 마감 | 아래만 톱니, 오른쪽 위 접힘 |
| 최종결산판 | 5일 마감 | 아래만 톱니, 왼쪽 위 접힘 |
| **주문결과판** | 손님 한 명 | **위아래 둘 다 톱니** — 두루마리에서 뜯어낸 전표 |

## 정확도 명패

숫자가 매번 바뀌므로 **글자는 안 굽는다.** 판만 굽고 TMP 가 그 위에 쓴다.
바탕을 밝게 두고 테를 둘러야 짙은 갈색 글자가 읽힌다.
"""
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated'

PAPER = (254, 251, 231, 255)
PAPER_OLD = (246, 232, 196, 255)
PAPER_STAIN = (236, 214, 166, 255)
BORDER = (58, 26, 12, 255)
WHITE = (255, 255, 255, 255)

PLATE = (244, 214, 150, 255)          # 명패 바탕 — 종이보다 진해야 판 위에서 뜬다
PLATE_LIT = (252, 234, 186, 255)
PLATE_DARK = (214, 172, 104, 255)

PANEL_W, PANEL_H = 300, 330
PAD = 6                               # 흰 테가 들어갈 바깥 여백
ZIG_W, ZIG_H = 13, 8                  # 톱니 한 칸


def torn_edge(x0, x1, y, depth, down):
    """톱니로 찢긴 변. down 이 참이면 아래로 파인다."""
    n = max(2, int((x1 - x0) / ZIG_W))
    pts = []
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        d = depth if i % 2 else 0
        pts.append((x, y + (d if down else -d)))
    return pts


def panel_shape():
    """위아래가 다 뜯긴 전표. 좌우 변은 1칸씩 흔들어 자로 그은 티를 없앤다."""
    rnd = random.Random(5)
    x0, y0 = PAD, PAD
    x1, y1 = PANEL_W - PAD, PANEL_H - PAD

    pts = torn_edge(x0, x1, y0 + ZIG_H, ZIG_H, False)      # 윗변 (왼→오)
    y = y0 + ZIG_H + 14                                     # 오른쪽 변
    while y < y1 - ZIG_H - 10:
        pts.append((x1 + rnd.choice((0, -1)), y))
        y += 16
    pts += torn_edge(x1, x0, y1 - ZIG_H, ZIG_H, True)       # 아랫변 (오→왼)
    y = y1 - ZIG_H - 14                                     # 왼쪽 변 (아래→위)
    while y > y0 + ZIG_H + 10:
        pts.append((x0 + rnd.choice((0, 1)), y))
        y -= 16
    return pts


def make_panel():
    pts = panel_shape()

    mask = Image.new('L', (PANEL_W, PANEL_H), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)

    # 종이 — 가장자리로 갈수록 누렇게. 디더로 두 단만 섞는다.
    rnd = random.Random(9)
    paper = Image.new('RGBA', (PANEL_W, PANEL_H), PAPER)
    px = paper.load()
    inner = mask.filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MinFilter(9))
    ip = inner.load()
    for y in range(PANEL_H):
        for x in range(PANEL_W):
            if ip[x, y] < 128 and rnd.random() < 0.7:
                px[x, y] = PAPER_OLD
    for _ in range(320):
        x, y = rnd.randint(PAD, PANEL_W - PAD - 1), rnd.randint(PAD, PANEL_H - PAD - 1)
        px[x, y] = PAPER_OLD if rnd.random() < 0.7 else PAPER_STAIN

    img = Image.new('RGBA', (PANEL_W, PANEL_H), (0, 0, 0, 0))
    img.paste(paper, (0, 0), mask)

    # 테두리와 흰 테 — 마스크를 깎아 만든 띠라 톱니까지 저절로 따라간다.
    band = mask.point(lambda v: 255 if v > 128 else 0)
    bp = band.load()

    inner2 = band.filter(ImageFilter.MinFilter(7)).load()
    border_mask = Image.new('L', (PANEL_W, PANEL_H), 0)
    mp = border_mask.load()
    for y in range(PANEL_H):
        for x in range(PANEL_W):
            if bp[x, y] > 128 and inner2[x, y] < 128:
                mp[x, y] = 255
    img.paste(Image.new('RGBA', (PANEL_W, PANEL_H), BORDER), (0, 0), border_mask)

    outer = band.filter(ImageFilter.MaxFilter(9)).load()
    white_mask = Image.new('L', (PANEL_W, PANEL_H), 0)
    wp = white_mask.load()
    for y in range(PANEL_H):
        for x in range(PANEL_W):
            if outer[x, y] > 128 and bp[x, y] < 128:
                wp[x, y] = 255

    under = Image.new('RGBA', (PANEL_W, PANEL_H), (0, 0, 0, 0))
    under.paste(Image.new('RGBA', (PANEL_W, PANEL_H), WHITE), (0, 0), white_mask)
    under.alpha_composite(img)
    return under


PLATE_W, PLATE_H = 236, 44


def make_plate():
    """
    정확도 명패. 숫자는 TMP 가 그 위에 쓴다.

    바탕을 종이보다 진하게 두고 먹테를 둘러야 짙은 갈색 글자가 읽힌다.
    양끝을 깎아 「완벽한 한 그릇!」 현판과 같은 결로 맞췄다.
    """
    img = Image.new('RGBA', (PLATE_W, PLATE_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    cut = 10
    pts = [(cut, 0), (PLATE_W - cut, 0), (PLATE_W - 1, PLATE_H // 2),
           (PLATE_W - cut, PLATE_H - 1), (cut, PLATE_H - 1), (0, PLATE_H // 2)]

    d.polygon(pts, fill=PLATE)
    d.polygon([(cut, 0), (PLATE_W - cut, 0), (PLATE_W - cut, 7), (cut, 7)], fill=PLATE_LIT)
    d.polygon([(cut, PLATE_H - 8), (PLATE_W - cut, PLATE_H - 8),
               (PLATE_W - cut, PLATE_H - 1), (cut, PLATE_H - 1)], fill=PLATE_DARK)
    d.polygon(pts, outline=BORDER, width=3)

    # 안쪽 가는 선 한 겹. 명패다운 테가 생긴다.
    d.polygon([(cut + 5, 5), (PLATE_W - cut - 5, 5), (PLATE_W - 6, PLATE_H // 2),
               (PLATE_W - cut - 5, PLATE_H - 6), (cut + 5, PLATE_H - 6), (5, PLATE_H // 2)],
              outline=PLATE_DARK, width=1)
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    panel = make_panel()
    panel.save(os.path.join(OUT_DIR, '주문결과판.png'))
    print('주문결과판  %dx%d' % panel.size)

    plate = make_plate()
    plate.save(os.path.join(OUT_DIR, '정확도명패.png'))
    print('정확도명패  %dx%d' % plate.size)


if __name__ == '__main__':
    main()
