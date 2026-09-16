# -*- coding: utf-8 -*-
"""5일차 최종 결과창 판을 굽는다 — 받아 온 그림을 화면 크기로 다듬는다.

## 어디서 왔나

처음엔 이 스크립트가 판을 통째로 그렸다(종이·테두리·톱니·접힌 귀퉁이·제목을 전부 코드로).
2026-09-16 에 **사용자가 그 결과를 가져다 다듬어 왔고**, 그쪽이 훨씬 나아서 갈아 끼웠다.
받은 파일은 `Tools/art_source/최종결산판_원본.png` (1184x1328) 다.

## 받은 그림을 그대로는 못 쓴다

거기에는 **숫자 석 줄과 [확인] 버튼까지 그려져 있다.**
누적 매출·평균 정확도·완벽한 한 그릇은 **매번 값이 달라지고**, 버튼은 눌려야 한다.
그래서 여기서 그 자리를 **깨끗한 종이로 덮는다.**

남기는 것 — 종이 · 테두리 · 톱니 · 접힌 귀퉁이 · 그릇 낙서 · 제목 · 점선.

제목 「5일 영업 종료」는 늘 같은 말이라 그림에 든 채로 둔다(`FinalResultUI.Open` 이 매번
같은 문자열을 넣는다). 그래서 빌더는 `TitleText` 를 만들지 않고 참조를 비워 둔다.

## 지우는 자리

원본 좌표다. **그림을 다시 받으면 이 값부터 다시 잡아야 한다.**

  * 숫자 석 줄  y 505~845
  * 버튼        y 925~1165

덮을 종이는 **점선 바로 아래(y 455~500)의 빈 띠**에서 떠 온다. 세로로 이어 붙여도 티가 안 나는
것은 이 종이에 가로 줄무늬가 없기 때문이다 — 영수증(`레시피북`·`정산표`)은 줄이 있어서
같은 y 안에서 가로로만 옮겨야 했다. 여기는 반대다.

## 생성 배경 지우기

받은 그림은 알파가 없고 뒤에 (35,32,40) 이 깔려 있다(내가 보낸 시안의 배경색이다).
**그 색을 통째로 지우면 안 된다** — 테두리의 짙은 갈색 중 어두운 칸이 같이 날아가 구멍이 난다.
가장자리에서 이어진 것만 물을 채우듯 지운다.
"""
import os
import sys

from PIL import Image

SRC = 'Tools/art_source/최종결산판_원본.png'
OUT = 'Assets/Art/UI/Generated/최종결산판.png'

BG = (35, 32, 40)
BG_TOL = 18

ERASE_BANDS = ((505, 845), (925, 1165))     # 숫자 석 줄, 버튼
# 덮을 종이를 떠 올 빈 띠. **줄마다 밝기 편차를 재서 가장 평평한 곳을 골랐다.**
# 처음엔 점선 바로 아래(455~500)를 썼는데 점선 그림자가 딸려 들어와, 세로로 이어 붙이자
# 40칸마다 가로줄이 생겼다. 610~650 은 첫 줄과 둘째 줄 사이의 빈 종이다(편차 2.0).
CLEAN_BAND = (610, 650)

# 받은 그림 맨 위에 **잘린 글자 띠**가 남아 있다(시안을 캡처해 다듬은 흔적).
# 배경색으로 덮어 두면 아래 물 채우기가 같이 지운다.
TOP_STRIP = 78

TARGET_W = 300                              # 캔버스에 뜰 폭. 정산표(280)와 한 벌로 보이는 크기


def is_bg(c):
    return (abs(c[0] - BG[0]) < BG_TOL and abs(c[1] - BG[1]) < BG_TOL
            and abs(c[2] - BG[2]) < BG_TOL)


def main():
    if not os.path.isfile(SRC):
        sys.exit('받은 원본이 없다: %s' % SRC)

    im = Image.open(SRC).convert('RGB')
    W, H = im.size

    # 1. 맨 위 잘린 글자 띠를 배경색으로 덮는다
    im.paste(Image.new('RGB', (W, TOP_STRIP), BG), (0, 0))

    # 2. 지울 띠를 빈 종이로 덮는다
    ch0, ch1 = CLEAN_BAND
    patch = im.crop((0, ch0, W, ch1))
    for y0, y1 in ERASE_BANDS:
        y = y0
        while y < y1:
            take = min(patch.height, y1 - y)
            im.paste(patch.crop((0, 0, W, take)), (0, y))
            y += take

    # 3. 가장자리에서 이어진 배경만 투명으로
    px = im.load()
    rgba = im.convert('RGBA')
    ap = rgba.load()
    seen = bytearray(W * H)
    stack = []
    for x in range(W):
        for y in (0, H - 1):
            stack.append((x, y))
    for y in range(H):
        for x in (0, W - 1):
            stack.append((x, y))
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= W or y >= H:
            continue
        i = y * W + x
        if seen[i] or not is_bg(px[x, y]):
            continue
        seen[i] = 1
        ap[x, y] = (0, 0, 0, 0)
        stack.append((x + 1, y))
        stack.append((x - 1, y))
        stack.append((x, y + 1))
        stack.append((x, y - 1))

    # 4. 판만 남기고 자른다
    box = rgba.getbbox()
    board = rgba.crop(box)

    # 5. 화면 크기로 줄인다. 픽셀아트가 아니라 그린 그림이라 LANCZOS 로 줄여야 가장자리가
    #    계단지지 않는다(정산표를 8배로 줄인 것과 같은 이유).
    th = round(TARGET_W * board.height / board.width)
    board = board.resize((TARGET_W, th), Image.LANCZOS)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    board.save(OUT)
    print('%s  %dx%d   (원본에서 잘라 낸 자리 %s)' % (OUT, TARGET_W, th, box))


if __name__ == '__main__':
    main()
