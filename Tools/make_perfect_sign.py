# -*- coding: utf-8 -*-
"""「완벽한 한 그릇!」 팻말을 굽는다 — 가게에 걸린 나무 현판.

## 왜 만들었나

정확도 100% 일 때 뜨는 한 방인데, 그동안은 **버튼용 나무 아이콘(`Icon_Wood`)을 300x56 으로
늘려 회색 톤(C7C7C7)을 입히고** 그 위에 TMP 글자를 얹은 것이었다. 게다가 외곽선을 내려고
**같은 글자를 여덟 벌 더 깔아** 한 팻말에 TMP 가 아홉 개 붙어 있었다.

「주문마감」이 붓글씨 그림인데 이쪽만 회색 판에 픽셀 폰트라 격이 안 맞았다.

## 글자를 그림에 굽는다

굽는 이유는 두 가지다.

  1. **외곽선을 TMP 로 내려면 글자를 여덟 벌 깔아야 한다.** 그림이면 한 장이다.
  2. 「완벽한 한 그릇!」은 **늘 같은 말**이라 구워도 안전하다.

색은 프로젝트에서 뽑았다 — 나무는 카운터(`주문화면 카운터.png`)의 (107,45,4) 계열,
글자 처리는 영수증 제목과 같은 방식(짙은 테 + 획 윗줄 한 겹)이다.

## 나무가 아니라 붉은 옻칠이다

처음에 나무 현판으로 구웠다가 갈아엎었다 — **조리 화면 배경이 나무판**이라 갈색 현판을
얹으니 벽에 뚫린 구멍처럼 묻혔다. 색을 어둡게 해도 같은 색 계열이라 안 떨어진다.

붉은 옻칠에 금빛 글자로 바꿨다. 주황 나무 위에서 확실히 떠오르고, 주문 화면의 노렌(붉은 천)과도
한 결이다. 축하하는 한 방이라 붉은색·금색이 뜻으로도 맞는다.

**배경과 같은 재질은 피한다** — 이 판의 교훈이다.

## 현판처럼 보이게 하는 것

판을 네모로만 두면 그냥 막대다. 넷이 필요했다.

  * **양끝을 비스듬히 깎는다** — 현판은 끝이 각져 있다
  * **금테** 한 겹
  * **못 두 개**를 양끝에. 벽에 걸린 물건이라는 표시다
  * **바깥에 짙은 테와 그림자** — 없으면 배경이 무엇이든 붙어 보인다
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated/완벽팻말.png'
FONT_PATH = 'Assets/Fonts/Galmuri11.ttf'

W, H = 344, 84
CUT = 18                            # 양끝을 깎는 깊이

LACQUER = (168, 34, 30, 255)        # 붉은 옻칠
LACQUER_LIT = (202, 62, 50, 255)    # 윗면
LACQUER_DARK = (118, 18, 16, 255)   # 아랫면
GOLD = (222, 176, 66, 255)          # 금테
GOLD_DARK = (150, 112, 30, 255)
EDGE = (36, 10, 8, 255)             # 바깥 테두리
NAIL = (226, 200, 132, 255)
NAIL_DARK = (140, 110, 40, 255)

TEXT = '완벽한 한 그릇!'
TEXT_PX = 33                        # 11의 정수배
INK = (250, 222, 140, 255)          # 글자 — 금빛
INK_EDGE = (58, 12, 10, 255)        # 테·그림자
INK_LIT = (255, 246, 206, 255)      # 획 윗줄 한 겹


def font(px):
    return ImageFont.truetype(FONT_PATH, px)


def board_points():
    """현판 바깥선. 양끝이 비스듬히 깎여 있다."""
    return [(CUT, 0), (W - CUT, 0), (W - 1, H // 2), (W - CUT, H - 1),
            (CUT, H - 1), (0, H // 2)]


def main():
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = board_points()

    d.polygon(pts, fill=LACQUER)

    # 윗면·아랫면 — 위에서 빛을 받는다. 다른 아이콘들과 같은 규칙이다.
    d.polygon([(CUT, 0), (W - CUT, 0), (W - CUT, 11), (CUT, 11)], fill=LACQUER_LIT)
    d.polygon([(CUT, H - 12), (W - CUT, H - 12), (W - CUT, H - 1), (CUT, H - 1)], fill=LACQUER_DARK)

    # 금테 — 바깥선에서 6칸 안쪽으로 한 겹
    inner = [(CUT + 8, 6), (W - CUT - 8, 6), (W - 8, H // 2), (W - CUT - 8, H - 7),
             (CUT + 8, H - 7), (8, H // 2)]
    d.polygon(inner, outline=GOLD, width=2)

    # 못 두 개
    for nx in (CUT + 2, W - CUT - 2):
        d.ellipse([nx - 3, H // 2 - 3, nx + 3, H // 2 + 3], fill=NAIL, outline=NAIL_DARK)

    # 글자 — 영수증 제목과 같은 처리. 마스크를 밀어 테와 그림자를 만든다.
    f = font(TEXT_PX)
    tw = int(d.textlength(TEXT, font=f))
    bb = f.getbbox(TEXT)
    tx, ty = (W - tw) // 2, (H - (bb[3] - bb[1])) // 2 - bb[1]

    glyph = Image.new('L', (W, H), 0)
    ImageDraw.Draw(glyph).text((tx, ty), TEXT, font=f, fill=255)
    solid = glyph.point(lambda v: 255 if v > 96 else 0)

    def shifted(dx, dy):
        out = Image.new('L', (W, H), 0)
        out.paste(solid, (dx, dy))
        return out

    parts = [shifted(dx, dy) for dx, dy in
             ((-2, 0), (2, 0), (0, -2), (0, 2), (2, 3), (3, 3), (-2, -2), (2, -2))]
    loads = [p.load() for p in parts]
    sp = solid.load()

    edge_mask = Image.new('L', (W, H), 0)
    ep = edge_mask.load()
    for y in range(H):
        for x in range(W):
            if sp[x, y]:
                continue
            for lp in loads:
                if lp[x, y]:
                    ep[x, y] = 255
                    break

    img.paste(Image.new('RGBA', (W, H), INK_EDGE), (0, 0), edge_mask)
    img.paste(Image.new('RGBA', (W, H), INK), (0, 0), solid)

    lit = Image.new('L', (W, H), 0)
    lp2, dp = lit.load(), shifted(0, 2).load()
    for y in range(H):
        for x in range(W):
            if sp[x, y] and not dp[x, y]:
                lp2[x, y] = 255
    img.paste(Image.new('RGBA', (W, H), INK_LIT), (0, 0), lit)

    # 바깥 테두리 — 맨 마지막에. 글자가 판 밖으로 삐져나와도 여기서 잘린다.
    d = ImageDraw.Draw(img)
    d.polygon(pts, outline=EDGE, width=3)

    # 판 밖으로 나간 글자를 잘라 낸다
    shape = Image.new('L', (W, H), 0)
    ImageDraw.Draw(shape).polygon(pts, fill=255)
    img.putalpha(Image.eval(img.getchannel('A'), lambda v: v).point(lambda v: v))
    cut = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    cut.paste(img, (0, 0), shape)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    cut.save(OUT)
    print('%s  %dx%d' % (OUT, W, H))


if __name__ == '__main__':
    main()
