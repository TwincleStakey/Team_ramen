# -*- coding: utf-8 -*-
"""B 키로 올라오는 비법서 배경을 굽는다.

정면에서 본 작은 세로 메모 수첩. 위쪽에 촘촘한 검은 코일.
오래된 느낌: 누런 종이, 옅은 가로 줄, 가장자리 얼룩, 커피 자국, 접힌 귀퉁이, 빨간 "비법" 도장.

크기 256x316. 종이는 (8,12)~(247,311) 240x300. 사방 8칸(위는 12칸, 코일 자리)이 여백이다.
빌더가 이 그림을 Panel 스프라이트로 쓴다(좌표 1칸 = 원본 1픽셀).

Galmuri7 로 도장 글자를 찍는다. 7픽셀 원본 크기 그대로 찍어야 획이 안 뭉개진다.
"""
from PIL import Image, ImageDraw, ImageFont
import os, random, math

OUT = 'Assets/Art/UI/Generated/RecipeBook.png'
W, H = 256, 316
PX, PY, PW, PH = 8, 12, 240, 300          # 종이

INK = (60, 42, 30, 255)
PAPER = (236, 222, 186, 255)
PAPER_OLD = (222, 204, 160, 255)          # 가장자리로 갈수록
PAPER_STAIN = (206, 184, 138, 255)
RULE = (214, 198, 160, 255)               # 옅은 가로 줄
MARGIN_LINE = (196, 120, 110, 255)        # 왼쪽 빨간 여백선
COIL = (34, 30, 30, 255)
COIL_LIGHT = (92, 86, 84, 255)
HOLE = (150, 132, 100, 255)
STAMP = (196, 52, 44, 255)

CLEAR = (0, 0, 0, 0)


def main():
    random.seed(7)
    img = Image.new('RGBA', (W, H), CLEAR)
    p = img.load()
    d = ImageDraw.Draw(img)

    # 종이 — 안쪽은 밝고, 가장자리 12칸은 점점 누렇게. 디더로 두 단만 섞는다.
    for y in range(PY, PY + PH):
        for x in range(PX, PX + PW):
            edge = min(x - PX, PX + PW - 1 - x, y - PY, PY + PH - 1 - y)
            c = PAPER
            if edge < 12:
                t = (12 - edge) / 12.0
                if random.random() < t * 0.9:
                    c = PAPER_OLD
                if edge < 3 and random.random() < 0.5:
                    c = PAPER_STAIN
            p[x, y] = c

    # 가로 줄 — 코일 아래 여백을 두고 16칸마다. 군데군데 끊어 인쇄가 바랜 것처럼.
    for y in range(PY + 44, PY + PH - 20, 16):
        for x in range(PX + 6, PX + PW - 6):
            if random.random() < 0.92:
                p[x, y] = RULE

    # 왼쪽 빨간 여백선
    mx = PX + 30
    for y in range(PY + 30, PY + PH - 8):
        if random.random() < 0.9:
            p[mx, y] = MARGIN_LINE

    # 종이 위 잔 얼룩
    for _ in range(160):
        x = random.randint(PX + 4, PX + PW - 5)
        y = random.randint(PY + 4, PY + PH - 5)
        p[x, y] = PAPER_OLD if random.random() < 0.7 else PAPER_STAIN

    # 커피 자국은 뺐다(2026-09-11). 낙서 옆에 있으니 뭔지 모를 원으로 보였다.

    # 종이 윤곽
    d.rectangle([PX, PY, PX + PW - 1, PY + PH - 1], outline=INK)

    # 접힌 귀퉁이 — 오른쪽 아래. 대각선 바깥 삼각형은 비우고(귀퉁이가 들려 배경이 비친다),
    # 대각선 안쪽 삼각형은 뒷면(어두운 종이)으로 덮는다. 뒷면의 세 변에 먹선.
    # 한 번에 X자로 그리면 구겨진 것처럼 보인다. 바깥은 반드시 비어 있어야 한다.
    F = 16
    right, bottom = PX + PW - 1, PY + PH - 1
    for y in range(bottom - F, bottom + 1):
        for x in range(right - F, right + 1):
            dist = (right - x) + (bottom - y)      # 귀퉁이에서 대각선 방향 거리
            if dist < F:
                p[x, y] = CLEAR                    # 들린 귀퉁이 자리
            elif dist == F:
                p[x, y] = INK                      # 접힌 선
            elif x == right - F or y == bottom - F:
                p[x, y] = INK                      # 뒷면의 두 변
            else:
                p[x, y] = PAPER_OLD                # 뒷면

    # 아래 가장자리 잔 찢김
    for x in range(PX + 14, PX + PW - 30, 19):
        if random.random() < 0.7:
            p[x, PY + PH - 1] = CLEAR
            p[x, PY + PH - 2] = INK

    # 코일 — 12칸마다 고리 하나, 속이 빈 아치. 꽉 채우면 못을 박아 둔 것처럼 보인다.
    for x in range(PX + 6, PX + PW - 10, 12):
        d.rectangle([x + 1, PY + 4, x + 6, PY + 7], fill=HOLE, outline=INK)
        d.rectangle([x, 0, x + 7, PY + 5], fill=COIL)
        d.rectangle([x + 2, 2, x + 5, PY + 3], fill=CLEAR)
        d.rectangle([x + 2, PY + 1, x + 5, PY + 3], fill=p[x + 3, PY + 2])
        p[x, 0] = CLEAR
        p[x + 7, 0] = CLEAR
        p[x + 1, 1] = COIL_LIGHT
        p[x + 1, 2] = COIL_LIGHT
        p[x + 2, 1] = COIL_LIGHT
        for y in range(1, PY + 6):
            if p[x, y] == COIL: p[x, y] = INK
            if p[x + 7, y] == COIL: p[x + 7, y] = INK
        for xx in range(x + 1, x + 7):
            if p[xx, 0] == COIL: p[xx, 0] = INK

    # 색인 탭은 두지 않는다. 오른쪽으로 삐져나온 탭이 부자연스러워 뺐다(2026-09-11).

    # 낙서 — 연필색으로, 글이 안 닿는 자리에만. 글은 그림 y 56~236, x 44~244 를 쓴다.
    # PIL 의 선·타원은 안티에일리어싱이 없어서 픽셀아트에 그대로 쓸 수 있다.
    PENCIL = (128, 104, 82, 255)

    # 1. 라멘 그릇 — 오른쪽 아래. 테두리 타원, 몸통 아래 반원, 굽, 안에 물결 면, 김 세 줄기.
    bx0, bx1, by = PX + 142, PX + 206, PY + 244
    d.ellipse([bx0, by, bx1, by + 12], outline=PENCIL)                 # 테두리
    d.arc([bx0, by - 14, bx1, by + 40], 0, 180, fill=PENCIL)           # 몸통
    d.line([bx0 + 22, by + 40, bx1 - 22, by + 40], fill=PENCIL)        # 굽
    for x in range(bx0 + 8, bx1 - 8):                                  # 면
        wy = by + 5 + ((x // 3) % 2)
        p[x, wy] = PENCIL
        if x % 7 == 0:
            p[x, wy + 1] = PENCIL
    for k, sx_ in enumerate((bx0 + 18, bx0 + 32, bx0 + 46)):           # 김
        for t in range(12):
            yy = by - 6 - t - (k * 2)
            xx = sx_ + (1 if (t // 3) % 2 else 0)
            if t % 4 != 3:
                p[xx, yy] = PENCIL
    # 젓가락 두 짝 — 테두리 오른쪽에서 위로 비스듬히
    d.line([bx1 - 6, by + 2, bx1 + 14, by - 26], fill=PENCIL)
    d.line([bx1 - 12, by + 4, bx1 + 8, by - 26], fill=PENCIL)

    # 2. 반숙 계란 — 왼쪽 아래. 타원 안에 작은 노른자 원.
    ex0, ey0 = PX + 98, PY + 250
    d.ellipse([ex0, ey0, ex0 + 20, ey0 + 26], outline=PENCIL)
    d.ellipse([ex0 + 6, ey0 + 9, ex0 + 14, ey0 + 17], outline=PENCIL)
    p[ex0 + 9, ey0 + 12] = PENCIL

    # 3. 별 낙서 — 왼쪽 위, 제목 옆. 다섯 획으로 그린 별.
    cx_, cy_ = PX + 22, PY + 28
    pts = []
    for k in range(5):
        a = math.radians(-90 + k * 144)
        pts.append((int(round(cx_ + 9 * math.cos(a))), int(round(cy_ + 9 * math.sin(a)))))
    for k in range(5):
        d.line([pts[k], pts[(k + 1) % 5]], fill=PENCIL)

    # "비법" 도장 — 오른쪽 위, 코일 아래. 빨간 테두리에 빨간 글자, 바랜 느낌으로 군데군데 빠뜨린다.
    font = ImageFont.truetype('Assets/Fonts/Galmuri7.ttf', 7)
    sx, sy = PX + PW - 46, PY + 16
    d.rectangle([sx, sy, sx + 25, sy + 12], outline=STAMP)
    d.text((sx + 5, sy + 2), '비법', font=font, fill=STAMP)
    for _ in range(14):
        x = random.randint(sx, sx + 25)
        y = random.randint(sy, sy + 12)
        if p[x, y] == STAMP:
            p[x, y] = PAPER

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print(OUT, img.size)

    # 메뉴 이름 밑에 까는 빨간 크레파스 물결 밑줄. 글자 수(2·3)마다 한 장.
    # 폭 = 글자 수 x 12 + 4. 크레파스는 획 끝이 삐져나가는 게 자연스러워 양쪽 2칸을 더 둔다.
    for chars in (2, 3):
        w = chars * 12 + 4
        u = underline(w)
        path = OUT.replace('RecipeBook.png', 'RecipeUnderline_%d.png' % chars)
        u.save(path)
        print(path, u.size)


CRAYON = (196, 52, 44, 255)
CRAYON_LIGHT = (224, 104, 90, 255)
CRAYON_DARK = (150, 34, 30, 255)


def underline(w):
    """빨간 크레파스로 그은 물결 밑줄. 높이 8.

    물결은 주기 8, 진폭 2. 획 두께는 2~3 칸이고 군데군데 한 칸 비거나 색이 옅다 —
    크레파스가 종이 결에 걸려 고르게 안 묻는 느낌이다. 씨앗을 고정해 다시 돌려도 같다.
    """
    rnd = random.Random(w)
    img = Image.new('RGBA', (w, 8), (0, 0, 0, 0))
    p = img.load()
    for x in range(w):
        cy = 3.5 + 2.0 * math.sin(x / 8.0 * 2 * math.pi)
        thick = 3 if rnd.random() < 0.6 else 2
        y0 = int(round(cy - thick / 2.0))
        for y in range(y0, y0 + thick):
            if not (0 <= y < 8):
                continue
            r = rnd.random()
            if r < 0.08:
                continue                        # 종이 결에 걸려 빈 칸
            c = CRAYON_LIGHT if r < 0.30 else CRAYON_DARK if r > 0.88 else CRAYON
            p[x, y] = c
        # 획 가장자리에 튄 가루
        if rnd.random() < 0.18:
            yy = y0 - 1 if rnd.random() < 0.5 else y0 + thick
            if 0 <= yy < 8:
                p[x, yy] = CRAYON_LIGHT
    return img


if __name__ == '__main__':
    main()
