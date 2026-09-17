# -*- coding: utf-8 -*-
"""아직 안 들어온 재료통에 얹는 자물쇠를 굽는다.

일차별 해금(IngredientUnlock)에 걸린 통은 어둡게 깔리고 그 위에 이 자물쇠가 뜬다.
어둡게만 하면 "지금 못 쓴다"가 아니라 "그림이 왜 이래"로 읽혀서 표식이 하나 필요하다.

48x48 칸에 그리고 화면에서도 48칸으로 쓴다(좌표 1칸 = 원본 1픽셀). 재료통이 64~128칸이라
그 위에 얹었을 때 통을 다 가리지 않으면서도 눈에 들어오는 크기다. 키울 때는 늘리지 말고
이 크기로 다시 그린다 — 정수배가 아니면 획이 뭉개진다.

처음에 24로 구웠더니 김(진한 초록)·목이(진한 갈색)처럼 어두운 통 위에서 자물쇠가 묻혔다.
어두워진 통과 자물쇠가 둘 다 어두우면 "잠겼다"가 아니라 "그림이 왜 이래"로 읽힌다.

색은 상단바 아이콘들과 같은 계열로 맞췄다. 윤곽은 재료 그림들과 같은 먹색이다.
"""
from PIL import Image
import os

OUT = 'Assets/Art/UI/Generated'
NAME = '자물쇠.png'
OPEN_NAME = '자물쇠_열림.png'
SIZE = 48

# 열린 자물쇠에서 고리를 몇 칸 들어 올릴지. 왼쪽 기둥(몸통 윗변까지 8칸)이 완전히 빠져나와야
# 열린 것으로 읽힌다. 7 이면 기둥 끝이 몸통 윗변 위로 한 칸 뜬다.
OPEN_LIFT = 7

INK = (26, 20, 18, 255)            # 윤곽. 재료 그림들과 같은 먹색
SHACKLE = (150, 152, 158, 255)     # 고리 쇠
SHACKLE_DARK = (98, 100, 107, 255)
BODY_LIGHT = (233, 186, 74, 255)   # 몸통 놋쇠
BODY = (198, 143, 44, 255)
BODY_DARK = (140, 94, 24, 255)


def blank():
    return Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))


def fill_spans(img, spans, color):
    """spans: {y: (x0, x1)} — 양 끝 포함."""
    p = img.load()
    for y, (x0, x1) in spans.items():
        for x in range(x0, x1 + 1):
            p[x, y] = color


def dots(img, points, color):
    p = img.load()
    for x, y in points:
        p[x, y] = color


def draw(shackle_lift=0):
    """shackle_lift 만큼 고리를 위로 올려 그린다. 0 이면 잠긴 모습, 양수면 열린 모습이다.

    고리를 기울이지 않고 곧게 들어 올린다. 픽셀아트에서 사선으로 젖히면 대각선 계단이
    통 그림의 격자와 따로 놀아 지저분해진다. 곧게 들어도 왼쪽 기둥이 몸통에서 빠져나오면
    "열렸다"로 읽힌다.
    """
    img = blank()
    lift = shackle_lift

    # ── 고리 ────────────────────────────────────────────────
    # 몸통 위로 솟은 U 를 뒤집은 모양. 바깥 윤곽을 통째로 깔고 안쪽을 파낸다.
    arch = {7 - lift: (20, 27), 8 - lift: (17, 30), 9 - lift: (15, 32)}
    for y in range(10 - lift, 24 - lift):
        arch[y] = (14, 33)
    fill_spans(img, arch, INK)

    # 고리 속을 비운다(뚫린 구멍). 여기가 막히면 자물쇠가 아니라 가방 손잡이로 보인다.
    hole = {10 - lift: (21, 26)}
    for y in range(11 - lift, 23 - lift):
        hole[y] = (20, 27)
    fill_spans(img, hole, (0, 0, 0, 0))

    # 고리 쇳빛. 왼쪽 기둥에 빛, 오른쪽 기둥에 그늘.
    fill_spans(img, {8 - lift: (18, 29)}, SHACKLE)
    fill_spans(img, {9 - lift: (16, 23)}, SHACKLE)
    fill_spans(img, {9 - lift: (24, 31)}, SHACKLE_DARK)
    fill_spans(img, {10 - lift: (15, 20)}, SHACKLE)
    fill_spans(img, {10 - lift: (27, 32)}, SHACKLE_DARK)
    for y in range(11 - lift, 23 - lift):
        fill_spans(img, {y: (15, 18)}, SHACKLE)
        fill_spans(img, {y: (29, 32)}, SHACKLE_DARK)

    # ── 몸통 ────────────────────────────────────────────────
    body = {}
    for y in range(23, 41):
        body[y] = (9, 38)
    fill_spans(img, body, INK)

    inner = {}
    for y in range(24, 40):
        inner[y] = (10, 37)
    fill_spans(img, inner, BODY)

    # 위·왼쪽 빛, 아래·오른쪽 그늘. 두 줄이면 놋쇠처럼 도톰해 보인다.
    fill_spans(img, {24: (10, 37), 25: (10, 37)}, BODY_LIGHT)
    for y in range(26, 38):
        fill_spans(img, {y: (10, 11)}, BODY_LIGHT)
    fill_spans(img, {38: (10, 37), 39: (10, 37)}, BODY_DARK)
    for y in range(26, 40):
        fill_spans(img, {y: (36, 37)}, BODY_DARK)

    # 열쇠 구멍. 동그라미 하나에 아래로 홈 한 줄.
    fill_spans(img, {28: (21, 26), 29: (20, 27), 30: (20, 27), 31: (21, 26)}, INK)
    fill_spans(img, {32: (22, 25), 33: (22, 25), 34: (22, 25), 35: (22, 25)}, INK)

    return img


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, OUT)
    os.makedirs(out_dir, exist_ok=True)

    path = os.path.join(out_dir, NAME)
    draw().save(path)
    print('구웠습니다:', path)

    open_path = os.path.join(out_dir, OPEN_NAME)
    draw(OPEN_LIFT).save(open_path)
    print('구웠습니다:', open_path)


if __name__ == '__main__':
    main()
