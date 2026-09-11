# -*- coding: utf-8 -*-
"""시식 연출에 쓰는 소품을 굽는다.

지금은 그릇 접지 그림자 한 장뿐이다.

그릇이 카운터에 놓여 있을 때 바닥에 지는 그늘. 없으면 그릇이 나무판 위에 떠 보인다.
카운터 선이 어긋나서가 아니라 접지 그림자가 없어서 뜨는 것이라, 그릇을 아래로 내려도
해결되지 않는다.

**그릇 그림에 붙여 굽지 않는다.** 후룩 하려고 그릇을 들어올릴 때 그림자는 카운터에 남아서
좁아지고 옅어져야 한다. 붙여 두면 그림자가 그릇을 따라 같이 떠올라 버린다.

그릇 시트와 같은 220칸에 그려 두므로 같은 자리에 그대로 겹쳐 놓으면 맞는다.
좌표 1칸 = 원본 1픽셀이라 화면에 그린 크기 그대로 나간다.

한때 여기서 '그릇 잡은 손' 과 '김 진한 그릇 시트' 도 구웠다. 손은 도형을 조합해 만든
티가 나서 뺐고, 김은 원래 옅은 것이 낫다고 판단해 되돌렸다.
"""
from PIL import Image, ImageDraw, ImageFilter
import math
import os

OUT = 'Assets/Art/나머지'
CELL = 220

# 그릇 본체가 칸 안에서 차지하는 자리. 시트를 재서 얻은 값이다.
BOWL_BOTTOM = 188

# 면 색은 '얇은면 그릇용.png' 에서 뽑았다. 그릇 안 면과 같은 색이어야
# 집어 올린 면이 그 그릇에서 나온 것으로 보인다.
NOODLE = (232, 178, 40, 255)
NOODLE_LIGHT = (250, 214, 110, 255)
NOODLE_SHADE = (202, 133, 0, 255)
INK = (26, 20, 18, 255)

# 매달린 면발 시트. 한 칸이 길이 한 단계다.
STRAND_W, STRAND_H = 56, 132
STRAND_STEPS = 7


def shadow():
    """그릇 아래 타원 그늘. 가장자리를 흐려 나무결에 얹히게 한다."""
    img = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 바닥에 닿는 자리는 그릇 굽(아래쪽 빨간 받침)이라 그것보다 조금 퍼진 폭으로 잡는다.
    half = 52
    cy = BOWL_BOTTOM - 3
    d.ellipse([CELL // 2 - half, cy - 11, CELL // 2 + half, cy + 11], fill=(0, 0, 0, 150))

    img = img.filter(ImageFilter.GaussianBlur(5))

    path = os.path.join(OUT, '그릇그림자.png')
    img.save(path)
    print('그릇 그림자  %s' % path)


def strand_frame(length):
    """젓가락 끝에 매달린 면발 한 칸.

    가닥 넷이 위(젓가락 자리)에서 시작해 아래로 늘어진다. 가닥마다 물결의 위상과 폭이
    달라야 뭉뚱그린 띠로 안 보인다. 길이가 줄면 아래가 잘리는 게 아니라 **위쪽만 남는다** —
    입으로 빨려 들어가는 것이므로 아래 끝이 위로 딸려 올라와야 한다.
    """
    img = Image.new('RGBA', (STRAND_W, STRAND_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    cx = STRAND_W // 2
    # (가로 시작 위치, 물결 폭, 위상, 길이 비율)
    lines = [(-9, 4.0, 0.0, 1.00), (-3, 2.5, 1.7, 0.86), (3, 3.2, 3.0, 0.95), (9, 2.0, 4.4, 0.78)]

    for dx, amp, phase, ratio in lines:
        n = int(length * ratio)
        if n < 3:
            continue

        pts = []
        for y in range(n):
            x = cx + dx + amp * math.sin(y / 9.0 + phase)
            pts.append((x, y))

        # 먹선 한 겹을 먼저 굵게 깔고 그 위에 면을 얹으면 1픽셀 외곽선이 된다.
        d.line(pts, fill=INK, width=5)

    for dx, amp, phase, ratio in lines:
        n = int(length * ratio)
        if n < 3:
            continue

        pts = [(cx + dx + amp * math.sin(y / 9.0 + phase), y) for y in range(n)]
        d.line(pts, fill=NOODLE, width=3)
        d.line([(x - 1, y) for x, y in pts], fill=NOODLE_LIGHT, width=1)
        d.line([(x + 1, y) for x, y in pts], fill=NOODLE_SHADE, width=1)

    return img


def strands():
    """면발 시트. 왼쪽 칸이 가장 길고 오른쪽으로 갈수록 짧아진다 — 후룩 한 번이 한 바퀴다."""
    sheet = Image.new('RGBA', (STRAND_W * STRAND_STEPS, STRAND_H), (0, 0, 0, 0))

    for i in range(STRAND_STEPS):
        # 마지막 칸은 아예 비운다. 면이 다 들어간 상태다.
        length = int(STRAND_H * (1 - i / (STRAND_STEPS - 1.0)))
        sheet.alpha_composite(strand_frame(length), (i * STRAND_W, 0))

    path = os.path.join(OUT, '면발.png')
    sheet.save(path)
    print('매달린 면발  %d칸 (%dx%d)  %s' % (STRAND_STEPS, STRAND_W, STRAND_H, path))


# 땀방울. 32칸 두 장 — 맺힘 / 흘러내림.
#
# 처음엔 16칸으로 그렸는데 손님이 300칸이라 화면에서 거의 안 보였다. 늘려 쓰면 획이
# 반칸에 걸리므로 이 크기로 다시 찍는다.
DROP = (150, 205, 245, 255)
DROP_LIGHT = (228, 246, 255, 255)
DROP_DEEP = (92, 156, 214, 255)

SWEAT_CELL = 32

# 물방울 모양. 위는 뾰족하고 아래는 둥글다.
DROP_TIP = 3          # 뾰족한 끝이 있는 줄
DROP_BULB = 21        # 둥근 아랫배의 한가운데 줄
DROP_R = 8            # 그 반지름


def drop_widths():
    """줄마다 반폭. 끝에서 배까지는 곡선으로 벌어지고, 배 아래는 원을 따른다.

    반폭이 0으로 떨어지는 줄은 아예 비운다. 억지로 1칸씩 채우면 뾰족한 끝이 길게 늘어져,
    맺혀 있는 장인데도 꼬리가 달린 것처럼 보인다.
    """
    out = {}
    for y in range(DROP_TIP, DROP_BULB + DROP_R + 1):
        if y <= DROP_BULB:
            t = (y - DROP_TIP) / float(DROP_BULB - DROP_TIP)
            w = DROP_R * (t ** 1.5)
        else:
            dy = y - DROP_BULB
            w = math.sqrt(max(0.0, DROP_R * DROP_R - dy * dy))

        n = int(round(w))
        if n >= 1:
            out[y] = n
    return out


def drop(shift, tail):
    """물방울 하나. shift 만큼 아래로 내리고, tail 이 참이면 위로 가는 꼬리를 단다."""
    img = Image.new('RGBA', (SWEAT_CELL, SWEAT_CELL), (0, 0, 0, 0))
    p = img.load()

    cx = SWEAT_CELL // 2
    widths = drop_widths()

    def out_top():
        return min(widths)

    for y, w in widths.items():
        y += shift
        if not 0 <= y < SWEAT_CELL:
            continue
        for x in range(cx - w, cx + w):
            p[x, y] = DROP

    # 흘러내리는 장은 위로 가는 가는 꼬리가 있어야 "떨어지는 중"으로 읽힌다.
    # 굵게 두면 물방울과 두께가 비슷해져 관처럼 보인다.
    if tail:
        for y in range(max(0, min(out_top(), SWEAT_CELL) + shift - 9), min(out_top(), SWEAT_CELL) + shift):
            p[cx - 1, y] = DROP
            p[cx, y] = DROP

    # 빛은 왼쪽 위에서 든다. 한 줄로 길게 그으면 줄무늬가 되므로 덩어리로 찍는다.
    for y in range(DROP_BULB - 7 + shift, DROP_BULB - 1 + shift):
        if 0 <= y < SWEAT_CELL:
            for x in range(cx - 5, cx - 2):
                if p[x, y][3]:
                    p[x, y] = DROP_LIGHT

    # 둥근 아래쪽 오른쪽이 제일 어둡다. 가장자리를 따라서만.
    for y in range(DROP_BULB - 2 + shift, DROP_BULB + DROP_R + shift):
        if not 0 <= y < SWEAT_CELL:
            continue
        for x in range(SWEAT_CELL - 1, cx, -1):
            if p[x, y][3]:
                p[x, y] = DROP_DEEP
                if p[x - 1, y][3]:
                    p[x - 1, y] = DROP_DEEP
                break

    # 먹 윤곽 한 겹. 칠해진 자리에 맞닿은 빈 칸만 — 대각선은 빼서 모서리를 살린다.
    edge = []
    for y in range(SWEAT_CELL):
        for x in range(SWEAT_CELL):
            if p[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < SWEAT_CELL and 0 <= ny < SWEAT_CELL and p[nx, ny][3]:
                    edge.append((x, y))
                    break
    for x, y in edge:
        p[x, y] = INK

    return img


def sweat():
    """맺힘 / 흘러내림 두 장을 가로로 잇는다."""
    sheet = Image.new('RGBA', (SWEAT_CELL * 2, SWEAT_CELL), (0, 0, 0, 0))
    sheet.alpha_composite(drop(0, False), (0, 0))
    sheet.alpha_composite(drop(1, True), (SWEAT_CELL, 0))

    path = os.path.join(OUT, '땀방울.png')
    sheet.save(path)
    print('땀방울      2칸 (%dx%d)  %s' % (SWEAT_CELL, SWEAT_CELL, path))


# 까마귀는 여기서 굽지 않는다. Tools/make_crow.py 가 따로 만든다(48칸 4프레임 옆모습).
# 한때 여기서도 실루엣으로 구웠는데 같은 파일에 써서 서로 덮어썼다.


# 어색한 침묵의 점. 손님 머리 위에 셋이 하나씩 찍힌다.
#
# 말풍선 글자로 ". . ." 를 쓰면 대사처럼 읽힌다. 침묵은 대사가 아니므로 그림이어야 한다.
# 배경이 노렌(빨강)·야경(남색)·나무기둥(주황)으로 제각각이라 어디에 놓여도 보이도록
# 먹색 알맹이에 흰 테를 두른다 — Tab·B 아이콘과 같은 방식이다.
DOT_CELL = 28
DOT_FILL = (255, 255, 255, 255)
DOT_LINE = (26, 20, 18, 255)


def dot():
    """점 한 알. 가운데가 살짝 밝아 납작한 원반이 아니라 알갱이로 보인다."""
    img = Image.new('RGBA', (DOT_CELL, DOT_CELL), (0, 0, 0, 0))
    p = img.load()

    c = (DOT_CELL - 1) / 2.0
    r = 9.0

    for y in range(DOT_CELL):
        for x in range(DOT_CELL):
            if math.hypot(x - c, y - c) <= r:
                p[x, y] = DOT_FILL

    # 광은 넣지 않는다. 이 크기에서는 얼룩으로 보인다. 먹 알갱이 하나로 두는 편이 깔끔하다.

    # 먹 테 두 겹. 노렌(빨강)·야경(남색) 어디에 놓여도 흰 알맹이가 또렷하게 떠오른다.
    for _ in range(2):
        edge = []
        for y in range(DOT_CELL):
            for x in range(DOT_CELL):
                if p[x, y][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < DOT_CELL and 0 <= ny < DOT_CELL and p[nx, ny][3]:
                        edge.append((x, y))
                        break
        for x, y in edge:
            p[x, y] = DOT_LINE

    return img


def dots():
    path = os.path.join(OUT, '침묵점.png')
    dot().save(path)
    print('침묵 점    1칸 (%dx%d)  %s' % (DOT_CELL, DOT_CELL, path))


def main():
    shadow()
    strands()
    sweat()
    dots()


if __name__ == '__main__':
    main()
