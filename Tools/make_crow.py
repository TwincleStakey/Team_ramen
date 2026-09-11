# -*- coding: utf-8 -*-
"""손님 리액션 "까악까악" 용 까마귀 스프라이트 시트를 굽는다.

옆모습, 부리는 오른쪽. 48x48 칸 4프레임을 가로로 이어 붙인다(192x48).
  0  입 다묾, 날개 내림
  1  고개 살짝 들고 부리 반쯤, 날개 중간
  2  고개 더 들고 부리 활짝, 날개 등 위로 활짝 — 여기가 "까악"
  3  다시 부리 반쯤, 날개 중간
0→1→2→3→0 으로 돌리면 파닥이며 까악까악 한다.

둥근 몸통에 큰 눈, 주황 부리·발(옆모습이라 하나만). 몸은 먹색보다 한 단 밝은 남색 계열이라 먹 윤곽이 보인다.
좌표 1칸 = 원본 1픽셀. 키울 때는 늘리지 않고 이 크기로 다시 그린다.

같이 뽑는 GIF 는 확인용이다(4배, 프레임당 0.12초).
"""
from PIL import Image, ImageDraw
import os, sys

OUT = 'Assets/Art/나머지/까마귀.png'
GIF = sys.argv[1] if len(sys.argv) > 1 else 'crow_preview.gif'
CELL = 48

CLEAR = (0, 0, 0, 0)
INK = (14, 12, 18, 255)
BODY = (46, 44, 60, 255)
BODY_LIGHT = (78, 76, 98, 255)
BODY_DARK = (30, 28, 40, 255)
EYE_WHITE = (250, 250, 255, 255)
BEAK = (240, 160, 40, 255)
BEAK_DARK = (196, 116, 24, 255)
FOOT = (224, 140, 32, 255)
CHEEK = (214, 100, 110, 255)


def ring(img, color):
    """칠해진 자리 바깥에 한 겹. 대각선은 안 두른다."""
    p = img.load()
    w, h = img.size
    edge = []
    for y in range(h):
        for x in range(w):
            if p[x, y][3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and p[nx, ny][3] != 0:
                    edge.append((x, y))
                    break
    for x, y in edge:
        p[x, y] = color


def frame(head_dy, beak, wing):
    """beak: 0 다묾, 1 반쯤, 2 활짝. wing: 0 내림, 1 중간, 2 활짝 올림."""
    img = Image.new('RGBA', (CELL, CELL), CLEAR)
    d = ImageDraw.Draw(img)
    p = img.load()

    # 발 — 옆모습이라 하나만 보인다. 몸통보다 먼저 그려 몸에 가려지게. 세 발가락.
    fx = 21
    d.line([fx, 38, fx, 42], fill=FOOT)
    d.line([fx - 2, 43, fx + 2, 43], fill=FOOT)
    p[fx, 43] = FOOT
    p[fx - 2, 42] = FOOT
    p[fx + 2, 42] = FOOT

    # 꼬리 — 몸 뒤(왼쪽)로 비스듬히 올라간 깃 세 갈래
    d.polygon([(11, 30), (2, 22), (3, 27), (1, 30), (5, 31), (10, 35)], fill=BODY_DARK)

    # 몸통 — 밝은 타원을 깔고 어두운 타원을 오른쪽 아래로 밀어 왼쪽 위에 윤기를 남긴다
    d.ellipse([8, 20, 34, 41], fill=BODY_LIGHT)
    d.ellipse([10, 22, 34, 41], fill=BODY)

    # 날개 — 어깨(28,24)에 붙어 돈다. 내리면 몸통을 크게 덮고, 올리면 등 위로 활짝 선다.
    # 머리보다 먼저 그려 올린 날개가 머리 뒤로 들어가게 한다.
    if wing == 0:
        d.polygon([(10, 25), (28, 23), (32, 29), (26, 38), (8, 37)], fill=BODY_DARK)
        d.line([13, 33, 24, 35], fill=BODY)                  # 깃 결
        d.line([12, 30, 25, 31], fill=BODY)
    elif wing == 1:
        d.polygon([(4, 18), (28, 23), (32, 29), (20, 33), (6, 28)], fill=BODY_DARK)
        d.line([9, 24, 24, 29], fill=BODY)
        d.line([8, 21, 22, 26], fill=BODY)
    else:
        d.polygon([(8, 4), (16, 5), (30, 22), (32, 29), (22, 28), (12, 16)], fill=BODY_DARK)
        d.line([12, 8, 26, 24], fill=BODY)
        d.line([16, 9, 28, 26], fill=BODY)
        # 깃 끝 세 갈래
        d.polygon([(8, 4), (5, 2), (10, 7)], fill=BODY_DARK)
        d.polygon([(4, 8), (9, 9), (7, 12)], fill=BODY_DARK)

    # 머리 — 몸통 오른쪽 위에 겹친 원. 까악할 때 위로 든다.
    hy = 17 + head_dy
    d.ellipse([22, hy - 9, 40, hy + 9], fill=BODY_LIGHT)
    d.ellipse([23, hy - 8, 40, hy + 9], fill=BODY)

    # 눈 — 크고 하얗다. 눈동자는 오른쪽 아래(부리 쪽)를 본다. 반짝임 한 칸.
    ex, ey = 34, hy - 1
    d.ellipse([ex - 3, ey - 3, ex + 3, ey + 3], fill=EYE_WHITE)
    d.rectangle([ex, ey - 1, ex + 1, ey + 1], fill=INK)
    p[ex - 1, ey - 2] = EYE_WHITE
    p[ex + 1, ey - 1] = EYE_WHITE

    # 볼 — 귀엽게 붉은 점 두 칸
    p[ex - 5, ey + 3] = CHEEK
    p[ex - 4, ey + 3] = CHEEK

    # 부리 — 머리 오른쪽에서 오른쪽으로 뾰족. 위·아래 부리를 따로 그려 벌린다.
    bx, by = 40, hy + 1
    if beak == 0:
        d.polygon([(bx - 1, by - 2), (bx + 7, by), (bx - 1, by + 2)], fill=BEAK)
        d.line([bx - 1, by, bx + 6, by], fill=BEAK_DARK)
    elif beak == 1:
        d.polygon([(bx - 1, by - 3), (bx + 7, by - 2), (bx - 1, by)], fill=BEAK)
        d.polygon([(bx - 1, by + 1), (bx + 6, by + 3), (bx - 1, by + 3)], fill=BEAK_DARK)
        d.rectangle([bx - 1, by, bx + 1, by + 1], fill=INK)   # 벌어진 입 속
    else:
        d.polygon([(bx - 1, by - 4), (bx + 7, by - 5), (bx - 1, by - 1)], fill=BEAK)
        d.polygon([(bx - 1, by + 1), (bx + 6, by + 5), (bx - 1, by + 4)], fill=BEAK_DARK)
        d.rectangle([bx - 1, by - 1, bx + 2, by + 1], fill=INK)
        p[bx, by] = CHEEK                                    # 입 속 혀

    ring(img, INK)
    return img


def main():
    frames = [frame(0, 0, 0), frame(-1, 1, 1), frame(-2, 2, 2), frame(-1, 1, 1)]

    sheet = Image.new('RGBA', (CELL * len(frames), CELL), CLEAR)
    for i, f in enumerate(frames):
        sheet.paste(f, (i * CELL, 0))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT)
    print(OUT, sheet.size)

    # 확인용 GIF — 4배, 어두운 배경. 마지막에 다문 프레임을 한 번 더 쥐어 "까악까악" 사이를 띄운다.
    scale = 4
    seq = frames + [frames[0], frames[0]]
    big = []
    for f in seq:
        bg = Image.new('RGBA', (CELL * scale, CELL * scale), (56, 48, 72, 255))
        up = f.resize((CELL * scale, CELL * scale), Image.NEAREST)
        bg.paste(up, (0, 0), up)
        big.append(bg.convert('P', palette=Image.ADAPTIVE))
    big[0].save(GIF, save_all=True, append_images=big[1:], duration=120, loop=0, disposal=2)
    print(GIF)


if __name__ == '__main__':
    main()
