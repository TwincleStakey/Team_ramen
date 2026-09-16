# -*- coding: utf-8 -*-
"""조리 화면 메인에 띄우는 빌지(주문서)·책(레시피북) 아이콘을 굽는다.

32x32 칸. 먹색 1픽셀 윤곽 바깥에 흰색을 두 겹 더 둘러 스티커처럼 뜨게 한다.
배경이 어두운 우주라 흰 테가 있어야 눈에 들어온다.

빌지: 크림색 영수증. 아래 끝이 톱니, 글줄 몇 개, 합계 줄, 빨간 도장.
책:   갈색 표지에 왼쪽 등, 오른쪽 아래로 삐져나온 속지, 빨간 갈피끈.

키울 때는 늘리지 않고 이 크기로 다시 그린다(좌표 1칸 = 원본 1픽셀).
"""
from PIL import Image
import math
import os

OUT = 'Assets/Art/UI/Generated'
SIZE = 32

INK = (26, 20, 18, 255)
WHITE = (255, 255, 255, 255)

PAPER = (250, 244, 227, 255)
PAPER_SHADE = (222, 212, 188, 255)
TEXT = (98, 88, 78, 255)
STAMP = (214, 48, 40, 255)

COVER = (150, 74, 40, 255)
COVER_LIGHT = (184, 104, 58, 255)
COVER_DARK = (104, 48, 26, 255)
PAGES = (250, 244, 227, 255)
PAGES_SHADE = (214, 204, 178, 255)
RIBBON = (214, 48, 40, 255)


def blank():
    return Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))


def rect(p, x0, y0, x1, y1, color):
    """양 끝 포함."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            p[x, y] = color


def ring(img, color):
    """칠해진 자리 바깥에 한 겹 두른다. 대각선은 안 두른다 — 두르면 뭉툭해진다."""
    p = img.load()
    edge = []
    for y in range(SIZE):
        for x in range(SIZE):
            if p[x, y][3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < SIZE and 0 <= ny < SIZE and p[nx, ny][3] != 0:
                    edge.append((x, y))
                    break
    for x, y in edge:
        p[x, y] = color


def sticker(img):
    """먹 윤곽 한 겹, 그 밖에 흰 테 두 겹. 흰 테 두 번째 겹은 대각선까지 채워
    모서리가 둥글게 감싸이도록 한다(한 겹씩 4방향으로 두르면 모서리가 뾰족하게 파인다)."""
    ring(img, INK)
    ring(img, WHITE)
    ring(img, WHITE)
    # 안쪽 모서리 파임 메우기: 흰 픽셀 둘이 대각선으로 맞닿고 그 사이 두 칸이 비면 채운다.
    p = img.load()
    for y in range(SIZE - 1):
        for x in range(SIZE - 1):
            a, b, c, d = p[x, y], p[x + 1, y], p[x, y + 1], p[x + 1, y + 1]
            if a == WHITE and d == WHITE and b[3] == 0 and c[3] == 0:
                p[x + 1, y] = WHITE
            if b == WHITE and c == WHITE and a[3] == 0 and d[3] == 0:
                p[x, y] = WHITE


def bill():
    """빌지 — 세로로 긴 영수증. 아래 끝이 톱니."""
    img = blank()
    p = img.load()
    x0, x1 = 9, 22          # 폭 14
    y0, y1 = 4, 25          # 톱니 전까지
    rect(p, x0, y0, x1, y1, PAPER)
    # 톱니: 두 칸 폭으로 하나 걸러 한 칸 내려온다.
    for x in range(x0, x1 + 1, 4):
        rect(p, x, y1 + 1, min(x + 1, x1), y1 + 1, PAPER)
    # 오른쪽 가장자리 그늘
    rect(p, x1, y0, x1, y1, PAPER_SHADE)
    # 제목 줄(굵게) + 글줄
    rect(p, x0 + 2, y0 + 2, x1 - 3, y0 + 3, TEXT)
    for y in (y0 + 6, y0 + 8, y0 + 10):
        rect(p, x0 + 2, y, x1 - 3, y, TEXT)
        p[x1 - 3, y] = PAPER_SHADE
    # 합계 구분선(점선)과 합계 줄
    for x in range(x0 + 2, x1 - 1, 2):
        p[x, y0 + 13] = TEXT
    rect(p, x0 + 2, y0 + 15, x0 + 5, y0 + 15, TEXT)
    rect(p, x1 - 5, y0 + 15, x1 - 2, y0 + 15, TEXT)
    # 빨간 도장(둥근 네모)
    sx, sy = x0 + 7, y0 + 17
    rect(p, sx, sy, sx + 4, sy + 3, STAMP)
    p[sx, sy] = PAPER
    p[sx + 4, sy] = PAPER
    p[sx, sy + 3] = PAPER
    p[sx + 4, sy + 3] = PAPER
    p[sx + 2, sy + 1] = PAPER
    p[sx + 2, sy + 2] = PAPER
    sticker(img)
    return img


def book():
    """책 — 닫힌 책을 살짝 위에서 본 모양. 등은 왼쪽, 속지는 오른쪽 아래."""
    img = blank()
    p = img.load()
    # 속지 뭉치(표지보다 오른쪽·아래로 두 칸 삐져나온다)
    rect(p, 8, 7, 25, 26, PAGES)
    rect(p, 24, 7, 25, 26, PAGES_SHADE)
    rect(p, 8, 25, 25, 26, PAGES_SHADE)
    # 속지 결(가로 줄 몇 개)
    for y in (9, 12, 15, 18, 21, 24):
        p[24, y] = PAGES
    # 표지
    rect(p, 6, 5, 23, 24, COVER)
    # 등(왼쪽 세로 띠)과 그 경계
    rect(p, 6, 5, 8, 24, COVER_DARK)
    rect(p, 9, 5, 9, 24, COVER_LIGHT)
    # 표지 위쪽 하이라이트
    rect(p, 10, 5, 23, 5, COVER_LIGHT)
    # 표지 가운데 밝은 액자(제목 자리)
    rect(p, 12, 9, 20, 9, COVER_LIGHT)
    rect(p, 12, 9, 12, 19, COVER_LIGHT)
    rect(p, 12, 19, 20, 19, COVER_DARK)
    rect(p, 20, 9, 20, 19, COVER_DARK)
    rect(p, 14, 12, 18, 12, PAGES)
    rect(p, 14, 14, 18, 14, PAGES)
    rect(p, 14, 16, 16, 16, PAGES)
    # 갈피끈 — 위에서 내려와 아래로 삐져나온다
    rect(p, 17, 5, 18, 8, RIBBON)
    rect(p, 17, 25, 18, 28, RIBBON)
    p[17, 28] = (0, 0, 0, 0)
    sticker(img)
    return img


# 톱니바퀴 — 설정(Esc).
#
# **놋쇠다.** 쇠색(회색)으로 먼저 구웠는데 상단바가 흰 판·나무·금테로 따뜻한 데다
# 설정창·마무리 버튼·타이틀 버튼이 전부 나무+금테라, 회색 하나만 차갑게 떴다.
# 놋쇠로 내리면 그 금테들과 한 식구가 되면서도 종이·가죽과는 재질이 갈린다.
GEAR = (186, 146, 70, 255)
GEAR_LIGHT = (226, 192, 120, 255)
GEAR_DARK = (128, 94, 40, 255)
GEAR_HOLE = (74, 52, 22, 255)


def gear():
    """
    톱니바퀴. 각도로 그린다 — 이를 하나씩 놓으면 몸통에서 떨어져 나온다(한 번 그랬다).

    이는 **여덟 개**다. 여섯이면 별처럼 보이고 열둘이면 32칸에서 이 하나가 한 칸이라
    뭉개져 동그라미가 된다. 여덟이면 위아래·좌우 넷이 축에 맞고 대각선 넷이 사이를 메운다.

    바깥 반지름은 12 다. 32칸에서 가운데가 16 이니 가장자리에 4칸이 남고,
    스티커 테(먹 한 겹 + 흰 두 겹)가 그 자리를 쓴다.
    """
    img = blank()
    p = img.load()

    cx = cy = 16.0
    R_TOOTH, R_BODY, R_HOLE = 12.0, 8.6, 3.6
    TEETH = 8

    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x - cx + 0.5, y - cy + 0.5
            r = math.hypot(dx, dy)
            if r > R_TOOTH:
                continue

            if r > R_BODY:
                # 이 사이 골. 한 바퀴를 여덟로 나눠 절반만 이로 남긴다.
                # 이 하나를 **위쪽 한가운데에 맞춘다.** 반 칸 돌려 놓지 않으면 이가
                # 대각선에 박혀서 톱니바퀴가 통째로 기울어 보인다.
                turn = (math.atan2(dy, dx) / (2 * math.pi) + 0.5 / TEETH) % 1.0
                if (turn * TEETH) % 1.0 > 0.5:
                    continue

            # 빛은 위에서. **세로로만** 눕힌다 — 대각선으로 가르면 그늘이 쐐기처럼
            # 도드라져서 톱니바퀴 위에 다른 도형이 얹힌 것으로 보인다.
            lit = dy / R_TOOTH
            p[x, y] = GEAR_LIGHT if lit < -0.45 else (GEAR_DARK if lit > 0.45 else GEAR)

    # 가운데 구멍. 뚫려 있어야 톱니바퀴지, 막혀 있으면 해바라기다.
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x - cx + 0.5, y - cy + 0.5
            if math.hypot(dx, dy) <= R_HOLE:
                p[x, y] = GEAR_HOLE

    sticker(img)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, img in (('Icon_Bill', bill()), ('Icon_Book', book()), ('Icon_Gear', gear())):
        path = os.path.join(OUT, name + '.png')
        img.save(path)
        print(path, img.size)


if __name__ == '__main__':
    main()
