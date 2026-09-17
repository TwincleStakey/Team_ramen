# -*- coding: utf-8 -*-
"""창 아이콘(실행 파일 아이콘)을 게임 안 그림으로 만든다.

    python Tools/make_app_icon.py

    Assets/Art/UI/Generated/AppIcon.png  (256x256)

타이틀 배경(포장마차 야경)을 네모로 잘라 써 봤더니 48칸에서 붉은 얼룩이 됐다. 아이콘은
작게 줄었을 때도 무엇인지 읽혀야 해서, 그림 하나가 화면을 다 채우는 편이 낫다.
그래서 조리 화면의 그릇 그림을 그대로 쓴다 — 국물과 면이 다 든 마지막 칸에 토핑 셋을 얹었다.

원본이 128칸이라 2배(256)로만 키운다. 정수배가 아니면 픽셀이 뭉개진다.
"""
from PIL import Image
import os

BOWL = 'Assets/Art/그릇/Sio_Topping.png'      # 16칸 시트. 마지막 칸이 국물+면이 다 든 그릇
TOPPING = 'Assets/Art/그릇/토핑배치.png'       # 6x5 칸, 한 칸 50
OUT = 'Assets/Art/UI/Generated/AppIcon.png'

CELL = 50
SIZE = 128
SCALE = 2

# (칸 x, 칸 y, 그릇 안에 놓을 왼쪽 위 자리) — 그릇 한가운데 국물 위에 셋을 흩는다.
# 자리는 Bowl.Layouts 를 베끼지 않았다. 아이콘은 한 장 뿐이라 눈으로 맞추는 편이 빠르다.
PIECES = [
    (4, 0, (30, 40)),   # 계란 반쪽 — 왼쪽
    (1, 2, (60, 30)),   # 멘마 — 가운데 위
    (1, 3, (58, 56)),   # 차슈 — 가운데 아래
]


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

    sheet = Image.open(os.path.join(root, BOWL)).convert('RGBA')
    bowl = sheet.crop((sheet.width - SIZE, 0, sheet.width, SIZE)).copy()

    toppings = Image.open(os.path.join(root, TOPPING)).convert('RGBA')
    for cx, cy, at in PIECES:
        cell = toppings.crop((cx * CELL, cy * CELL, (cx + 1) * CELL, (cy + 1) * CELL))
        bowl.alpha_composite(cell, at)

    icon = bowl.resize((SIZE * SCALE, SIZE * SCALE), Image.NEAREST)

    path = os.path.join(root, OUT)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    icon.save(path)
    print('구웠습니다:', path)


if __name__ == '__main__':
    main()
