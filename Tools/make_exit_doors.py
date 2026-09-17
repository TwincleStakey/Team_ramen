# -*- coding: utf-8 -*-
"""설정창 우측 하단에 다는 「나가기」 문 아이콘을 굽는다.

    python Tools/make_exit_doors.py

    Assets/Art/UI/Generated/문_열림.png     쓰는 것 — 이쪽으로 젖혀진 열린 문

48x64 에 **선으로만** 그린다. 면을 칠하지 않는다 — 설정판이 이미 갈색이라 칠하면 판에
묻히고, 선만 있으면 판 테두리(금색)와 같은 언어가 된다.

획은 3칸이다. 1~2칸으로 그었더니 48칸 아이콘에서 실오라기처럼 보였다.
색은 설정판 테두리에서 그대로 따 왔다.

후보로 화살표·닫힌 문·노렌도 그려 보고 열린 문으로 골랐다. 고른 것만 굽는다 —
안 쓰는 그림을 Assets 에 두면 다음 사람이 어느 것이 진짜인지 모른다. 다시 보고 싶으면
아래 함수들이 그대로 남아 있으니 main 에 이름을 도로 넣으면 된다.
"""
from PIL import Image
import os

OUT = 'Assets/Art/UI/Generated'
W, H = 48, 64

LINE = (214, 172, 84, 255)   # 설정판 테두리와 같은 금색
THICK = 3                     # 획 두께


def blank():
    return Image.new('RGBA', (W, H), (0, 0, 0, 0))


def brush(p, x, y, t, color):
    """(x,y) 를 한가운데로 t 칸짜리 네모 붓을 찍는다."""
    half = t // 2
    for dy in range(-half, t - half):
        for dx in range(-half, t - half):
            nx, ny = x + dx, y + dy
            if 0 <= nx < W and 0 <= ny < H:
                p[nx, ny] = color


def line(img, x0, y0, x1, y1, t=THICK, color=LINE):
    """브레젠험으로 이은 뒤 붓으로 굵힌다."""
    p = img.load()
    dx, dy = abs(x1 - x0), abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx - dy
    x, y = x0, y0
    while True:
        brush(p, x, y, t, color)
        if x == x1 and y == y1:
            break
        e2 = 2 * err
        if e2 > -dy:
            err -= dy
            x += sx
        if e2 < dx:
            err += dx
            y += sy


def poly(img, points, t=THICK, color=LINE, close=True):
    for i in range(len(points) - 1):
        line(img, points[i][0], points[i][1], points[i + 1][0], points[i + 1][1], t, color)
    if close:
        line(img, points[-1][0], points[-1][1], points[0][0], points[0][1], t, color)


def knob(img, x, y, color=LINE):
    """손잡이 점. 획보다 한 칸 굵게 찍어야 점으로 읽힌다."""
    brush(img.load(), x, y, THICK + 1, color)


# ── 1. 열린 문 ─────────────────────────────────────────────
def open_door():
    """기준으로 받은 그림과 같은 꼴. 문틀은 네모, 문짝은 이쪽으로 젖혀진 사다리꼴이다.

    문틀 아래변을 54 로 올리고 문짝 아래변을 63 까지 내렸다. 둘 다 57 근처였을 때는
    획이 3칸이라 두 선이 겹쳐 아래쪽이 뭉툭한 띠로 보였다. 9칸은 떨어져야 두 선으로 읽힌다.

    **문틀 아래변은 문짝 오른쪽 모서리(31)부터만 긋는다.** 네모를 통째로 그으면 문짝에
    가려 안 보여야 할 선이 열린 문짝 밑으로 삐져나와, 문이 아니라 겹친 도형으로 읽힌다.
    """
    img = blank()

    # 문틀. 왼쪽 기둥은 문짝 경첩과 같은 선이고, 아래변은 문짝에 가리지 않는 오른쪽만 보인다.
    line(img, 4, 3, 43, 3)      # 위
    line(img, 43, 3, 43, 54)    # 오른쪽 기둥
    line(img, 31, 54, 43, 54)   # 아래 — 문짝 오른쪽 모서리부터

    # 문짝. 경첩(왼쪽)이 멀고 열린 쪽(오른쪽)이 가까워서 오른쪽 모서리가 더 길다.
    poly(img, [(4, 3), (31, 11), (31, 63), (4, 54)])

    knob(img, 26, 37)
    return img


# ── 2. 열린 문 + 화살표 ────────────────────────────────────
def arrow_door():
    """열린 문에 밖으로 나가는 화살표를 얹는다. 「여기로 나간다」가 한눈에 읽힌다.

    화살표 자리를 내주려고 문을 왼쪽으로 좁혔다. 문틀을 43 까지 두면 화살촉이 기둥에 물려
    화살표인지 가시인지 알 수 없다.
    """
    img = blank()

    poly(img, [(3, 5), (29, 5), (29, 52), (3, 52)])
    poly(img, [(3, 5), (21, 12), (21, 60), (3, 52)])
    knob(img, 17, 36)

    # 문 오른쪽으로 빠져나가는 화살표. 몸통과 화살촉 둘.
    line(img, 34, 31, 45, 31)
    line(img, 39, 25, 45, 31)
    line(img, 39, 37, 45, 31)
    return img


# ── 3. 닫힌 문 ─────────────────────────────────────────────
def closed_door():
    """문틀 안에 문짝 하나. 가장 조용한 꼴이다."""
    img = blank()

    poly(img, [(4, 3), (43, 3), (43, 61), (4, 61)])
    poly(img, [(10, 9), (37, 9), (37, 55), (10, 55)])
    knob(img, 32, 32)
    return img


# ── 4. 노렌 ────────────────────────────────────────────────
def noren():
    """문간에 천이 늘어져 있다. 라멘집 입구 그대로다."""
    img = blank()

    # 문간
    poly(img, [(4, 3), (43, 3), (43, 61), (4, 61)])

    # 천을 거는 봉과 늘어진 천 석 장
    line(img, 7, 12, 40, 12)
    for x in (13, 23, 33):
        line(img, x, 14, x, 31)
    line(img, 7, 31, 40, 31, 2)   # 천 아랫단은 한 칸 얇게 — 접힌 자락이다

    # 문턱. 없으면 아래 절반이 비어 창문처럼 보인다.
    line(img, 9, 55, 38, 55, 2)

    return img


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, OUT)
    os.makedirs(out_dir, exist_ok=True)

    # 고른 것만 굽는다. 후보를 다시 보려면 여기에 이름을 도로 넣는다 —
    # ('문_화살표', arrow_door), ('문_닫힘', closed_door), ('문_노렌', noren)
    for name, make in [('문_열림', open_door)]:
        path = os.path.join(out_dir, name + '.png')
        make().save(path)
        print('구웠습니다:', path)


if __name__ == '__main__':
    main()
