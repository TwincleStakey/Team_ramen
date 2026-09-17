# -*- coding: utf-8 -*-
"""연출 배속 토글의 키 안내 아이콘을 굽는다.

    python Tools/make_fast_icon.py

    Assets/Art/UI/Generated/Icon_Fast.png      꺼짐 — 속이 빈 겹화살표
    Assets/Art/UI/Generated/Icon_Fast_On.png   켜짐 — 속이 찬 겹화살표

48x48 이다. ESC·B·Tab 아이콘과 같은 스티커 꼴 — 그림 둘레에 흰 테를 한 칸 두른다.
그 셋(32칸)보다 큰 까닭은 저 셋과 달리 상단바 줄에 끼지 않고 혼자 떨어져 서기 때문이다.
작게 두면 빈 나무판 위에서 먼지처럼 보인다.

**32 로 구운 것을 늘리지 않았다.** 48 은 32 의 1.5 배라 정수배가 아니고, 늘리면 획이
반칸에 걸려 가장자리에 회색이 낀다. 이 크기로 다시 그린다.

**꺼짐과 켜짐을 색이 아니라 모양으로 가른다.** 색만 흐리게 하면 「지금 못 쓰는 것」처럼
보인다. 속이 비었나 찼나로 가르면 상태가 분명하다.
"""
from PIL import Image
import os

OUT = 'Assets/Art/UI/Generated'
SIZE = 48

INK = (26, 20, 18, 255)          # 윤곽. 다른 아이콘들과 같은 먹색
GOLD = (233, 186, 74, 255)       # 켜졌을 때 속
GOLD_DARK = (198, 143, 44, 255)  # 그 그늘
RIM = (255, 255, 255, 255)       # 스티커 흰 테


def blank():
    return Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))


def triangle(img, left, top, bottom, apex_x, color):
    """왼쪽 변이 곧고 오른쪽이 뾰족한 삼각형을 채운다."""
    p = img.load()
    height = bottom - top
    mid = (top + bottom) / 2.0
    for y in range(top, bottom + 1):
        # 가운데에서 멀수록 짧아진다
        t = 1.0 - abs(y - mid) / (height / 2.0)
        end = left + int(round((apex_x - left) * t))
        for x in range(left, end + 1):
            if 0 <= x < SIZE and 0 <= y < SIZE:
                p[x, y] = color


def outline(img, color=INK):
    """그려진 것 둘레 한 칸을 윤곽색으로 두른다(바깥쪽)."""
    p = img.load()
    edge = []
    for y in range(SIZE):
        for x in range(SIZE):
            if p[x, y][3] != 0:
                continue
            near = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < SIZE and 0 <= ny < SIZE and p[nx, ny][3] != 0:
                        near = True
            if near:
                edge.append((x, y))
    for x, y in edge:
        p[x, y] = color


def draw(filled):
    img = blank()

    # 겹화살표 둘. 앞엣것이 조금 작아 원근이 생긴다.
    triangle(img, 8, 10, 38, 24, GOLD if filled else (0, 0, 0, 0))
    triangle(img, 23, 10, 38, 39, GOLD if filled else (0, 0, 0, 0))

    if filled:
        # 아래쪽에 그늘 한 겹. 납작해 보이지 않게 한다.
        p = img.load()
        for y in range(25, 39):
            for x in range(SIZE):
                if p[x, y][3] != 0 and p[x, y] == GOLD:
                    p[x, y] = GOLD_DARK
        outline(img)
    else:
        # 속이 빈 꼴 — 삼각형 윤곽만 남긴다.
        solid = blank()
        triangle(solid, 8, 10, 38, 24, INK)
        triangle(solid, 23, 10, 38, 39, INK)
        sp, ip = solid.load(), img.load()
        for y in range(SIZE):
            for x in range(SIZE):
                if sp[x, y][3] == 0:
                    continue
                # 둘레만 남기고 속을 판다
                hole = True
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if not (0 <= nx < SIZE and 0 <= ny < SIZE) or sp[nx, ny][3] == 0:
                            hole = False
                if not hole:
                    ip[x, y] = INK

    # 스티커 흰 테. ESC·B·Tab 과 같은 마감이다.
    outline(img, RIM)
    return img


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, OUT)
    os.makedirs(out_dir, exist_ok=True)

    for name, filled in [('Icon_Fast', False), ('Icon_Fast_On', True)]:
        path = os.path.join(out_dir, name + '.png')
        draw(filled).save(path)
        print('구웠습니다:', path)


if __name__ == '__main__':
    main()
