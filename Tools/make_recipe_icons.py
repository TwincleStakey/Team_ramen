# -*- coding: utf-8 -*-
"""레시피북에 쓰는 재료 아이콘을 굽는다.

조리 화면 재료 그림(Assets/Art/재료, 64칸)을 그대로 줄여 쓰면 안 된다. 그쪽은 손으로 집어
옮기는 물건이라 결·그림자가 촘촘한데, 레시피북에서는 한 칸이 32칸으로 줄어 그 결이 전부
뭉개진 얼룩이 된다. 그래서 **여기서 따로, 훨씬 간결하게** 굽는다.

간결화 규칙 — 작게 줄어도 뭐가 뭔지 구별되게 하는 것이 전부다.

  * 색은 재료마다 **한 덩어리**만. 명암은 밝은 면 하나, 어두운 면 하나까지.
  * 테두리는 어두운 갈색 1칸. 검정은 김하고 겹쳐서 안 쓴다.
  * 형태는 실루엣으로 읽히게. 곁가지(김 모락, 젓가락, 접시)는 다 뺀다.
  * 세 타래(시오·쇼유·돈코츠)만 같은 통 모양에 색을 달리한다 — 셋이 한 묶음으로 읽혀야 한다.

한 칸은 32칸이다. 레시피북 그림이 3배로 그려져 있어 붙일 때 3배(96칸)로 쓴다.
"""
import math
import os
import sys
from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated'
S = 32

INK = (60, 42, 30, 255)          # 테두리. 레시피북 글씨와 같은 먹색
STEEL = (176, 182, 190, 255)     # 스테인리스
STEEL_D = (126, 133, 143, 255)
BROTH = (226, 148, 58, 255)      # 육수
NOODLE = (232, 186, 74, 255)     # 면
NOODLE_D = (198, 146, 40, 255)
SHIO = (242, 232, 198, 255)      # 소금 타래
SHOYU = (120, 52, 38, 255)       # 간장 타래
TONKOTSU = (238, 226, 210, 255)  # 돈코츠 베이스
PORK = (236, 176, 170, 255)      # 차슈 살
PORK_D = (196, 116, 112, 255)
MENMA = (206, 152, 78, 255)      # 멘마
ONION = (126, 194, 84, 255)      # 파
ONION_D = (86, 150, 56, 255)
EGG_W = (250, 244, 230, 255)
EGG_Y = (246, 170, 52, 255)
NORI = (44, 58, 52, 255)
NORI_L = (70, 90, 80, 255)
SPROUT = (244, 240, 226, 255)
WOOD = (96, 62, 58, 255)
WOOD_L = (134, 92, 84, 255)
OIL = (244, 196, 74, 255)


def cell():
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def tare(fill):
    """타래 통. 셋이 같은 모양에 색만 다르다 — 한 묶음으로 읽히게."""
    img, d = cell()
    d.rectangle([4, 9, 27, 25], fill=STEEL, outline=INK)     # 사각 통
    d.rectangle([7, 12, 24, 22], fill=fill, outline=INK)     # 안에 담긴 것
    d.line([(5, 10), (5, 24)], fill=STEEL_D)                 # 왼쪽 그늘
    return img


def broth():
    """육수 냄비. 위가 벌어진 몸통에 손잡이 둘, 위에 국물 면이 보인다."""
    img, d = cell()
    d.polygon([(7, 12), (25, 12), (23, 26), (9, 26)], fill=STEEL, outline=INK)   # 몸통
    d.rectangle([3, 13, 7, 16], fill=STEEL, outline=INK)                          # 왼 손잡이
    d.rectangle([25, 13, 29, 16], fill=STEEL, outline=INK)                        # 오른 손잡이
    d.ellipse([5, 8, 27, 16], fill=BROTH, outline=INK)                            # 국물 면
    d.line([(11, 20), (11, 24)], fill=STEEL_D)
    return img


def noodles(thick):
    """
    면 뭉치. 굵기로 얇은면·굵은면을 가른다.

    타원 안에 줄을 긋는 방식은 버렸다 — 줄이 면이 아니라 무늬로 보이고, 굵기를 바꿔도
    둘이 구별되지 않았다. 가락을 **따로따로 눕혀** 그리면 작아도 면으로 읽힌다.
    """
    img, d = cell()
    if thick:
        rows, w = ((10, 0.0), (19, 1.6), (27, 3.2)), 5
    else:
        rows, w = ((8, 0.0), (13, 1.2), (18, 2.4), (23, 3.6), (28, 4.8)), 2

    # 네모 물결(지그재그)로 그었더니 32칸에서는 그물처럼 뭉쳤다. 한 굽이만 완만하게 준다 —
    # 곧은 막대가 아니라는 것만 보이면 면으로 읽힌다.
    for y, phase in rows:
        path = [(x, y + 1.6 * math.sin((x + phase * 3) * math.pi / 11.0))
                for x in range(3, 29)]
        d.line(path, fill=INK, width=w + 2, joint='curve')
        d.line(path, fill=NOODLE, width=w, joint='curve')
        if w >= 5:
            d.line(path, fill=NOODLE_D, width=1, joint='curve')
    return img


def chashu():
    """차슈 한 점. 바깥 살, 안쪽 소용돌이."""
    img, d = cell()
    d.ellipse([5, 6, 26, 26], fill=PORK, outline=INK)
    d.ellipse([11, 12, 20, 21], fill=PORK_D, outline=INK)
    d.point((15, 16), fill=PORK)
    return img


def menma():
    """
    멘마(죽순). 세로로 **세워** 그린다.

    눕혀 그렸더니 굵은면과 구별이 안 됐다 — 둘 다 가로 막대 셋이었다.
    죽순은 원래 세로로 길고 끝이 비스듬하니, 세워서 끝을 자르면 면과 안 헷갈린다.
    """
    img, d = cell()
    for i, x in enumerate((7, 14, 21)):
        top = 6 + (i % 2) * 3
        d.polygon([(x, top + 2), (x + 5, top), (x + 5, 26), (x, 26)],
                  fill=MENMA, outline=INK)
    return img


def green_onion():
    """파. 동그란 고리 넷."""
    img, d = cell()
    for cx, cy in ((10, 11), (21, 12), (12, 22), (23, 22)):
        d.ellipse([cx - 5, cy - 5, cx + 4, cy + 4], fill=ONION, outline=INK)
        d.ellipse([cx - 2, cy - 2, cx + 1, cy + 1], fill=ONION_D)
    return img


def egg():
    """반숙 계란 반쪽. 흰자에 노른자 하나."""
    img, d = cell()
    d.ellipse([5, 6, 26, 26], fill=EGG_W, outline=INK)
    d.ellipse([11, 12, 21, 21], fill=EGG_Y, outline=INK)
    return img


def nori():
    """김 한 장. 결 두 줄만 넣는다."""
    img, d = cell()
    d.rectangle([7, 5, 24, 27], fill=NORI, outline=INK)
    d.line([(11, 8), (11, 24)], fill=NORI_L)
    d.line([(19, 8), (19, 24)], fill=NORI_L)
    return img


def sprout():
    """
    숙주 셋. 머리(콩) 한 알에 꼬리 하나씩.

    선을 겹쳐 긋다 테두리가 속살을 덮어 점만 남았었다. 몸통을 **먼저 굵게 깔고**
    그 위에 밝은 속을 한 겹 얹는 순서라야 가늘어도 뭉개지지 않는다.
    """
    img, d = cell()

    # 선을 구부려 그리면 32칸에서는 긁힌 자국으로 보였다. 콩 한 알에 꼬리가 뻗은
    # **쉼표 모양** 통짜로 그린다. 머리가 크고 꼬리가 가늘어야 숙주로 읽힌다.
    for hx, hy, tx, ty in ((11, 10, 27, 5), (10, 21, 26, 17), (19, 27, 5, 24)):
        d.polygon([(hx, hy - 6), (tx, ty - 2), (tx, ty + 2), (hx, hy + 6)],
                  fill=SPROUT, outline=INK)
        d.ellipse([hx - 6, hy - 6, hx + 5, hy + 5], fill=SPROUT, outline=INK)
        d.ellipse([hx - 2, hy - 3, hx, hy - 1], fill=(222, 216, 194, 255))
    return img


def wood_ear():
    """목이버섯. 물결치는 귀 모양."""
    img, d = cell()
    d.polygon([(6, 20), (9, 10), (16, 14), (22, 8), (26, 18), (18, 25), (10, 24)],
              fill=WOOD, outline=INK)
    d.line([(12, 18), (20, 15)], fill=WOOD_L)
    return img


def flavor_oil():
    """향미유. 작은 종지에 기름 한 방울."""
    img, d = cell()
    d.ellipse([5, 11, 26, 24], fill=STEEL, outline=INK)
    d.ellipse([8, 13, 23, 21], fill=OIL, outline=INK)
    d.point((13, 16), fill=(255, 240, 190, 255))
    return img


ICONS = {
    'ShioTare': lambda: tare(SHIO),
    'ShoyuTare': lambda: tare(SHOYU),
    'TonkotsuBase': lambda: tare(TONKOTSU),
    'Broth': broth,
    'ThinNoodles': lambda: noodles(False),
    'ThickNoodles': lambda: noodles(True),
    'Chashu': chashu,
    'Menma': menma,
    'GreenOnion': green_onion,
    'Egg': egg,
    'Nori': nori,
    'BeanSprout': sprout,
    'WoodEar': wood_ear,
    'FlavorOil': flavor_oil,
}


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    for name, make in ICONS.items():
        path = os.path.join(OUT, 'RecipeIcon_%s.png' % name)
        make().save(path)

    print('재료 아이콘 %d장  %d칸  %s' % (len(ICONS), S, OUT))


main()
