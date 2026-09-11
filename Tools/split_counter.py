# -*- coding: utf-8 -*-
"""주문 화면 배경에서 카운터(테이블)만 떼어 따로 굽는다.

배경 한 장에 노렌·야경·카운터가 다 들어 있어서, 손님도 그릇도 카운터 **앞**에만 그릴 수 있었다.
그래서 손님 아래를 RectMask2D 로 잘라 "카운터 뒤에 서 있는 척" 을 시키고 있었다.
카운터를 한 겹 떼어 손님보다 나중에 그리면 그 흉내가 필요 없다. 진짜로 뒤에 서게 된다.

**떼기 좋은 그림이다.** 카운터 윗변이 사진 941줄 중 596줄에 가로로 곧게 뻗어 있고,
그 줄이 빌더가 쓰는 CounterLineY 와 같은 줄이다(RamenLayoutBuilder 의 CounterRow).
기둥도 거기서 끝나 위로 삐져나오는 것이 없다.

배경 전체를 먼저 화면 크기(960x540)로 줄인 뒤 자른다. 잘라서 줄이면 가장자리 한 줄이
어긋나 배경과 이음매가 생긴다.

나오는 그림은 960x198 이고, 윗변을 CounterLineY 에 맞춰 놓으면 배경과 정확히 겹친다.
"""
from PIL import Image
import os

SRC = 'Assets/Art/화면/주문화면 배경.png'
OUT = 'Assets/Art/화면/주문화면 카운터.png'

DESIGN = (960, 540)

# 사진 941 줄 중 나무가 화면을 덮기 시작하는 줄. RamenLayoutBuilder.CounterRow 와 같은 값이다.
COUNTER_ROW = 596 / 941


def main():
    if not os.path.exists(SRC):
        raise SystemExit('배경이 없다: ' + SRC)

    full = Image.open(SRC).convert('RGBA').resize(DESIGN, Image.LANCZOS)

    top = round(DESIGN[1] * COUNTER_ROW)
    counter = full.crop((0, top, DESIGN[0], DESIGN[1]))
    counter.save(OUT)

    print('카운터  %dx%d  (배경 %d줄부터)  %s' % (counter.width, counter.height, top, OUT))
    print('  빌더에서 윗변을 캔버스 y=%d 에 맞춘다' % (DESIGN[1] // 2 - top))


if __name__ == '__main__':
    main()
