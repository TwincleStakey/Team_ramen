# -*- coding: utf-8 -*-
"""면 튀김기 앞면에 다는 팻말 판을 굽는다.

    python Tools/make_noodle_signs.py

    Assets/Art/UI/Generated/면팻말.png   (68x22)

바구니 둘이 생김새가 거의 같아서 어느 쪽이 얇은면인지 눌러 보기 전에는 알 수 없었다.
튀김기 앞면(은색 띠)에 작은 판을 달고 그 위에 이름을 쓴다. 글자는 빌더가 TMP 로 얹으므로
여기서는 **판만** 굽는다 — 이름이 둘이라 판을 두 벌 굽는 것보다 한 벌을 나눠 쓰는 편이 낫다.

모양은 이 게임의 명패들(설정명패·정확도명패·완벽팻말)과 같은 언어다. 네 귀퉁이를 비스듬히
깎고 금테를 한 픽셀 두른다. 색도 설정명패에서 그대로 따 왔다 — 어두운 판에 금테라
은색 띠 위에서 또렷하게 뜬다.

판을 늘려 쓰지 않는다. 68x22 로 구워 그 크기 그대로 쓴다. 9-슬라이스로 늘리면 깎은 귀퉁이가
같이 늘어나 비뚤어진다.
"""
from PIL import Image
import os

OUT = 'Assets/Art/UI/Generated'
NAME = '면팻말.png'

W, H = 68, 22
CHAMFER = 4          # 귀퉁이를 몇 칸 깎을지

INK = (38, 26, 20, 255)        # 판 바탕 — 설정명패와 같은 먹갈색
GOLD = (214, 172, 84, 255)     # 금테 — 설정명패와 같은 색
GOLD_DIM = (150, 118, 56, 255) # 아랫변·오른변 그늘. 판이 도톰해 보인다
STUD = (214, 172, 84, 255)     # 양 끝 금못


def blank():
    return Image.new('RGBA', (W, H), (0, 0, 0, 0))


def inside(x, y):
    """깎은 귀퉁이 안쪽인가. 네 모서리를 대각선으로 자른다."""
    if x + y < CHAMFER:                     return False
    if (W - 1 - x) + y < CHAMFER:           return False
    if x + (H - 1 - y) < CHAMFER:           return False
    if (W - 1 - x) + (H - 1 - y) < CHAMFER: return False
    return True


def draw():
    img = blank()
    p = img.load()

    # 바탕
    for y in range(H):
        for x in range(W):
            if inside(x, y):
                p[x, y] = INK

    # 금테 한 픽셀. 안쪽이면서 바깥에 빈 자리가 닿아 있는 칸이 가장자리다.
    edge = []
    for y in range(H):
        for x in range(W):
            if not inside(x, y):
                continue
            for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                nx, ny = x + dx, y + dy
                if nx < 0 or ny < 0 or nx >= W or ny >= H or not inside(nx, ny):
                    edge.append((x, y))
                    break

    for x, y in edge:
        # 아래쪽과 오른쪽은 그늘진 금으로. 위·왼쪽만 밝으면 빛이 한 방향에서 온 것으로 읽힌다.
        p[x, y] = GOLD_DIM if (y > H // 2 or x > W - 4) else GOLD

    # 양 끝 금못 두 개. 「어딘가에 박혀 있다」는 표시다.
    for cx in (5, W - 6):
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            p[cx + dx, H // 2 - 1 + dy] = STUD

    return img


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, OUT)
    os.makedirs(out_dir, exist_ok=True)

    path = os.path.join(out_dir, NAME)
    draw().save(path)
    print('구웠습니다:', path)


if __name__ == '__main__':
    main()
