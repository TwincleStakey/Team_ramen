# -*- coding: utf-8 -*-
"""레시피북(Assets/Art/UI/레시피북.png)의 면·계란·김 칸을 다시 그려 얹는다.

원본 손그림(Tools/art_source/레시피북_원본.png, 900x1470)에서 다섯 칸을 지우고 새 그림을 얹는다.
  * 얇은 면(시오·쇼유) / 굵은 면(돈코츠): 젓가락 두 짝에 걸쳐 늘어진 면. 끝은 칸 아래에서
    일자로 끊는다 — 길어서 가려진 느낌. 원래 그림(가로줄 다발)은 면으로 안 읽혔다.
  * 계란·김: 하나씩. 원래 둘씩 그려져 있어 「2개 넣어야 하나」로 헷갈렸다.

항상 원본에서 다시 만드므로 몇 번 돌려도 같다. 재료 자리는 그대로라
RecipeBookUI 의 마우스 오버 표(Cells)는 손댈 것이 없다.

한 칸은 140x120(원본 3배 기준). 2배 슈퍼샘플로 그려 절반으로 줄여 원본과 같은 부드러운 외곽선을 얻는다.
"""
import math
import random
from PIL import Image, ImageDraw

SRC = 'Tools/art_source/레시피북_원본.png'
OUT = 'Assets/Art/UI/레시피북.png'

SS = 2
CW, CH = 140, 120
INK = (60, 42, 30, 255)           # 원본 먹선과 같은 색
NOODLE = (236, 192, 84, 255)
WOOD = (176, 118, 66, 255)
WOOD_D = (120, 76, 40, 255)
EGG_W = (252, 246, 232, 255)
EGG_WD = (222, 208, 180, 255)
EGG_Y = (246, 172, 54, 255)
EGG_YD = (222, 118, 36, 255)
EGG_YL = (255, 214, 120, 255)
NORI_G = (58, 84, 66, 255)
NORI_GL = (92, 130, 96, 255)
NORI_GLL = (150, 190, 140, 255)
PAPER = (250, 244, 226, 255)


def P(x, y):
    return (x * SS, y * SS)


def canvas():
    img = Image.new('RGBA', (CW * SS, CH * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def fin(img):
    return img.resize((CW, CH), Image.LANCZOS)


def stroke(d, pts, fill, w, o=1.5):
    """외곽선 있는 선. 먹선을 먼저 굵게, 그 위에 색."""
    pts = [P(*p) for p in pts]
    d.line(pts, fill=INK, width=int((w + 2 * o) * SS), joint='curve')
    d.line(pts, fill=fill, width=int(w * SS), joint='curve')


def ellipse(d, box, fill, o=2):
    x0, y0, x1, y1 = box
    d.ellipse([P(x0 - o, y0 - o), P(x1 + o, y1 + o)], fill=INK)
    d.ellipse([P(x0, y0), P(x1, y1)], fill=fill)


# ---------------------------------------------------------------- 면
def noodles(thick):
    """젓가락 두 짝 위로 고리를 이루며 넘어와 늘어지는 면. 아래는 한 줄에서 끊는다."""
    img, d = canvas()
    y_left, y_right, gap = 34, 24, 9         # 젓가락: 왼쪽 굵은 끝 → 오른쪽 가는 끝, 살짝 올라감
    cut = 108
    w = 8 if thick else 4
    n = 3 if thick else 5
    r = 9 if thick else 7                    # 젓가락을 넘는 고리 반지름
    xs = [70 + (i - (n - 1) / 2) * (22 if thick else 12) for i in range(n)]

    def stick_y(x):
        return y_left + (y_right - y_left) * (x - 10) / 122

    # 1) 젓가락 뒤에서 올라와 고리 꼭대기까지 (180° → 90°)
    for x in xs:
        ys = stick_y(x)
        arc = []
        for t in range(16):
            a = math.pi - math.pi * t / 15 * 0.5
            arc.append((x + r * math.cos(a), ys + 3 - r * 1.3 * math.sin(a)))
        stroke(d, [(x - r, ys + 12), (x - r, ys + 3)] + arc, NOODLE, w)
    # 2) 젓가락
    for k in range(2):
        yl, yr = y_left + k * gap, y_right + k * gap
        d.polygon([P(10, yl - 3.5), P(132, yr - 2), P(132, yr + 2), P(10, yl + 3.5)], fill=INK)
        d.polygon([P(12, yl - 2), P(130, yr - 1), P(130, yr + 1), P(12, yl + 2)], fill=WOOD)
        d.line([P(16, yl - 1), P(126, yr - 0.5)], fill=WOOD_D, width=SS)
    # 3) 고리 꼭대기에서 앞으로 넘어와 칸 밖까지 늘어지는 가닥 (90° → 0°)
    for x in xs:
        ys = stick_y(x)
        arc = []
        for t in range(16):
            a = math.pi / 2 - math.pi / 2 * t / 15
            arc.append((x + r * math.cos(a), ys + 3 - r * 1.3 * math.sin(a)))
        stroke(d, arc + [(x + r, 140)], NOODLE, w)
    # 아래를 일자로 끊는다
    clear = Image.new('RGBA', (img.width, img.height - cut * SS), (0, 0, 0, 0))
    img.paste(clear, (0, cut * SS))
    return fin(img)


# ---------------------------------------------------------------- 계란
def egg():
    """반숙 반쪽 하나. 조리 화면 계란과 같은 구도."""
    img, d = canvas()
    cx, cy = 70, 60
    ellipse(d, (cx - 34, cy - 44, cx + 34, cy + 44), EGG_W, o=4)
    d.ellipse([P(cx - 30, cy - 40), P(cx + 30, cy + 40)], outline=EGG_WD, width=int(2.5 * SS))
    ellipse(d, (cx - 21, cy - 24, cx + 21, cy + 24), EGG_Y, o=3)
    d.ellipse([P(cx - 14, cy - 6), P(cx + 16, cy + 20)], fill=EGG_YD)     # 흐르는 반숙
    d.ellipse([P(cx - 14, cy - 17), P(cx - 4, cy - 9)], fill=EGG_YL)      # 하이라이트
    return fin(img)


# ---------------------------------------------------------------- 김
def nori():
    """김 한 장 정면. 가장자리가 살짝 울퉁불퉁하고 오른쪽 아래 모서리가 말려 「얇은 한 장」이 드러난다."""
    img, d = canvas()
    x0, y0, x1, y1 = 32, 16, 108, 106
    rnd = random.Random(3)
    pts = []
    for t in range(20):
        pts.append((x0 + (x1 - x0) * t / 20, y0 + rnd.uniform(-1.5, 1.5)))
    for t in range(20):
        pts.append((x1 + rnd.uniform(-1.5, 1.5), y0 + (y1 - y0) * t / 20))
    for t in range(20):
        pts.append((x1 - (x1 - x0) * t / 20, y1 + rnd.uniform(-1.5, 1.5)))
    for t in range(20):
        pts.append((x0 + rnd.uniform(-1.5, 1.5), y1 - (y1 - y0) * t / 20))
    d.polygon([P(*p) for p in pts], fill=INK)
    d.polygon([P(x - (3 if x > 70 else -3), y - (3 if y > 60 else -3)) for x, y in pts], fill=NORI_G)
    # 결 — 얼룩 + 비스듬한 윤기 획
    rnd = random.Random(1)
    for _ in range(34):
        px = rnd.uniform(x0 + 4, x1 - 10)
        py = rnd.uniform(y0 + 4, y1 - 6)
        w = rnd.uniform(4, 9)
        col = (78, 108, 84, 255) if rnd.random() < 0.7 else (48, 70, 56, 255)
        d.ellipse([P(px, py), P(px + w, py + w * 0.4)], fill=col)
    for (sx, sy, ln) in ((x0 + 14, y0 + 30, 22), (x0 + 36, y0 + 12, 30), (x0 + 30, y1 - 32, 18)):
        d.line([P(sx, sy + ln), P(sx + ln * 0.55, sy)], fill=NORI_GLL, width=2 * SS)
    # 오른쪽 아래 모서리 말림 — 뒷면이 종이색
    d.polygon([P(x1 - 24, y1 + 2), P(x1 + 2, y1 + 2), P(x1 + 2, y1 - 24)], fill=PAPER)
    d.polygon([P(x1 - 26, y1), P(x1 - 4, y1 - 10), P(x1, y1 - 26)], fill=INK)
    d.polygon([P(x1 - 26, y1), P(x1 - 4, y1 - 10), P(x1, y1 - 26), P(x1 - 12, y1 - 14)], fill=NORI_GL)
    return fin(img)


# ---------------------------------------------------------------- 얹기
def center(x, y):
    """RecipeBookUI.Cells 의 좌표(판 300x490 기준, 가운데 원점) → 원본 900x1470 픽셀."""
    return (150 + x) * 3, (245 - y) * 3


# 지우는 높이. 원래 그림이 위아래로 ±63 까지 뻗어 있어 120 으로는 맨 윗줄이 한 줄 남았다.
# 옆 줄 그림은 가운데에서 67 부터 시작하므로 128(±64)이 상한이다.
EH = 128


def paper_at(book, cy):
    """같은 높이의 빈 종이(오른쪽 여백 108칸)를 떼어 오고 양옆 16칸은 거울로 채운다.
    줄 무늬가 가로라 거울로 이어도 티가 안 난다. x 764~872 는 어느 줄에서도 비어 있다."""
    strip = book.crop((764, cy - EH // 2, 872, cy + EH // 2))
    out = Image.new('RGBA', (CW, EH))
    out.paste(strip, (16, 0))
    flip = strip.transpose(Image.FLIP_LEFT_RIGHT)
    out.paste(flip.crop((92, 0, 108, EH)), (0, 0))
    out.paste(flip.crop((0, 0, 16, EH)), (124, 0))
    return out


def main():
    book = Image.open(SRC).convert('RGBA')
    cells = [
        (center(23, 119), noodles(False)),     # 시오 얇은 면
        (center(23, 0), noodles(False)),       # 쇼유 얇은 면
        (center(23, -119), noodles(True)),     # 돈코츠 굵은 면
        (center(-88, -159), egg()),            # 돈코츠 계란
        (center(-35, -159), nori()),           # 돈코츠 김
    ]
    for (cx, cy), art in cells:
        tile = paper_at(book, cy)
        tile.alpha_composite(art, (0, (EH - CH) // 2))
        book.paste(tile, (cx - CW // 2, cy - EH // 2))
    book.save(OUT)
    print('wrote', OUT)


if __name__ == '__main__':
    main()
