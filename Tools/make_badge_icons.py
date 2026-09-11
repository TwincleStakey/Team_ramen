# -*- coding: utf-8 -*-
"""그릇 옆에 뜨는 조미료 배지 아이콘 두 개를 굽는다.

시치미와 향미유는 그릇에 넣어도 그림이 안 바뀐다(Bowl.AddIcon 이 조미료를 거른다).
넣었는지 확인할 길이 없어서 그릇 오른쪽 위에 "아이콘 x N" 으로 따로 보여 준다.

색은 기존 그림에서 따 왔다.
  향미유  Assets/Art/조리/향미유통.png 의 기름   (253,185,2) / (191,103,1) / (150,66,0)
  시치미  Assets/Art/나머지/시치미.png 의 가루   (253,81,2) / (197,7,0) / (122,1,0)

윤곽은 재료 그림들과 같은 먹색이다. 24x24 칸에 그리고, 화면에서도 24칸으로 쓴다
(좌표 1칸 = 원본 1픽셀). 16으로 그렸더니 옆 글자보다 작아 눈에 안 들어왔다.
키울 때는 그린 것을 늘리지 않고 이 크기로 다시 그린다. 정수배가 아니면 획이 뭉개진다.
"""
from PIL import Image
import os

OUT = 'Assets/Art/UI'
SIZE = 24

INK = (26, 20, 18, 255)

OIL_SHINE = (255, 250, 214, 255)   # 윤기의 심. 거의 흰색이라야 젖어 보인다
OIL_LIGHT = (255, 226, 130, 255)
OIL = (253, 185, 2, 255)
OIL_DARK = (191, 103, 1, 255)

CHILI_LIGHT = (253, 81, 2, 255)
CHILI = (197, 7, 0, 255)
CHILI_DARK = (122, 1, 0, 255)
LEAF = (92, 158, 58, 255)
LEAF_DARK = (54, 106, 38, 255)


def blank():
    return Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))


def fill_spans(img, spans, color):
    """spans: {y: (x0, x1)} — 양 끝 포함."""
    p = img.load()
    for y, (x0, x1) in spans.items():
        for x in range(x0, x1 + 1):
            p[x, y] = color


def outline(img, color=INK):
    """칠해진 자리 바깥에 한 겹 두른다. 픽셀아트 윤곽은 대각선까지 두르면 뭉툭해진다."""
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


def droplet():
    """향미유 — 물방울. 끝이 뾰족하고 아래가 둥글다."""
    img = blank()

    # 줄마다 폭을 짝수로 못 박는다. 반폭을 실수로 두고 반올림하면 줄마다 중심이
    # 11 과 11.5 사이를 오가며 옆구리에 꺾임이 생기고, 그 꺾임 때문에 불꽃으로 보인다.
    width = {1: 2, 2: 2, 3: 4, 4: 4, 5: 4, 6: 6, 7: 6, 8: 8, 9: 8, 10: 10,
             11: 10, 12: 12, 13: 12, 14: 12, 15: 12, 16: 12, 17: 10, 18: 8, 19: 6}
    spans = {}
    for y, w in width.items():
        spans[y] = (12 - w // 2, 11 + w // 2)
    fill_spans(img, spans, OIL)

    p = img.load()
    # 그늘은 오른쪽 아래를 따라 초승달로 넣는다. 가로 띠로 깔면 그릇처럼 잘려 보인다.
    for y, (x0, x1) in spans.items():
        if y < 12:
            continue
        # 아래로 갈수록 넓어져 바닥을 감싼다. 줄을 통째로 칠하면 받침을 댄 것처럼 잘려 보인다.
        depth = max(3, y - 11)
        for x in range(max(x0, x1 - depth + 1), x1 + 1):
            p[x, y] = OIL_DARK

    # 윤기. 밝은 덩어리 하나로는 젖은 느낌이 안 난다. 왼쪽 배를 따라 심을 길게 세우고
    # 위아래를 한 겹 무르게 감싼다. 줄마다 왼쪽 끝에서 재므로 폭을 고쳐도 따라온다.
    for y in (11, 12, 13, 14):
        x0 = spans[y][0]
        p[x0 + 1, y] = OIL_SHINE
        p[x0 + 2, y] = OIL_SHINE
    for y in (10, 15):
        x0 = spans[y][0]
        p[x0 + 1, y] = OIL_LIGHT
        p[x0 + 2, y] = OIL_LIGHT

    # 꼭지 쪽 작은 반짝임. 기름방울은 좁은 데서 한 번 더 번뜩인다.
    for y in (6, 7):
        p[spans[y][0] + 1, y] = OIL_LIGHT

    outline(img)
    return img


def chili():
    """시치미 — 빨간 고추. 꼭지는 초록, 몸통은 왼쪽 아래로 휜다."""
    img = blank()

    # (중심 x, 반폭) — 아래로 갈수록 왼쪽으로 휘고 가늘어지다가, 끝에서 다시
    # 오른쪽으로 살짝 감긴다. 곧게 내리면 고추가 아니라 고드름으로 보인다.
    # 가는 대각선이라 같은 24칸인데도 물방울보다 작아 보였다. 반폭을 한 칸씩 키웠다.
    body = {5: (15.5, 2.6), 6: (15.2, 3.5), 7: (14.8, 3.8), 8: (14.2, 3.8),
            9: (13.5, 3.7), 10: (12.7, 3.5), 11: (11.8, 3.3), 12: (10.9, 3.1),
            13: (10.0, 2.8), 14: (9.1, 2.5), 15: (8.3, 2.2), 16: (7.6, 1.9),
            17: (7.1, 1.5), 18: (6.9, 1.2), 19: (7.3, 1.0), 20: (8.0, 0.8)}
    spans = {}
    for y, (cx, hw) in body.items():
        x0 = int(round(cx - hw))
        x1 = int(round(cx + hw)) - 1
        if x1 < x0:
            x1 = x0
        spans[y] = (x0, x1)
    fill_spans(img, spans, CHILI)

    p = img.load()
    # 오른쪽 아래에 그늘. 고추는 매끈해서 면이 둘로 갈린다.
    for y, (x0, x1) in spans.items():
        if x1 > x0:
            p[x1, y] = CHILI_DARK

    # 밝은 면은 바깥으로 휜 왼쪽 등줄기에만 준다. 줄마다 x0 을 칠하면
    # 몸통이 휘는 구간에서 점점이 흩어져 얼룩으로 보인다.
    ys = sorted(spans)
    for i, y in enumerate(ys):
        x0, x1 = spans[y]
        if x1 - x0 < 2:
            continue
        prev = spans[ys[i - 1]][0] if i else x0 + 1
        if x0 <= prev:
            p[x0 + 1, y] = CHILI_LIGHT

    # 꼭지. 몸통에 걸치는 초록 모자와 위로 뻗은 꼭지.
    for x, y in ((13, 4), (14, 4), (15, 4), (16, 4), (17, 4), (18, 4),
                 (14, 3), (15, 3), (16, 3), (17, 3)):
        p[x, y] = LEAF
    for x, y in ((17, 3), (17, 4)):
        p[x, y] = LEAF_DARK
    for x, y in ((16, 2), (17, 1)):
        p[x, y] = LEAF
    p[17, 1] = LEAF_DARK

    outline(img)
    return img


def main():
    for name, img in (('향미유 배지', droplet()), ('시치미 배지', chili())):
        path = os.path.join(OUT, name + '.png')
        img.save(path)
        print(path, img.size)


if __name__ == '__main__':
    main()
