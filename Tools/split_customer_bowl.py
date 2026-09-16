# -*- coding: utf-8 -*-
"""손님 앞 그릇에서 김을 떼어낸다.

## 왜

`손님그릇.png` 는 그릇과 김이 한 장에 같이 그려진 8칸 시트다. 그래서

  * 먹기 전에는 애니메이션이 안 돌아 **김이 멈춰 있다.** 뜨거운 그릇인데 김이 정지화면이다.
  * 다 먹을 때쯤 **김만 옅게** 만들 수가 없다. 알파를 내리면 그릇까지 같이 사라진다.

그릇은 여덟 칸이 사실상 같다(bbox 가 ±1칸, 불투명 픽셀 수가 ±40). 움직이는 것은 김뿐이다.
그러니 그릇 한 장 + 김 여덟 장으로 나누면 둘 다 풀린다.

## 어떻게 나누나

**줄로 자르지 않는다.** 김 밑동이 그릇 테두리 위로 겹쳐 있어서(y 121 아래에도 김이 있다),
가로선으로 자르면 밑동이 잘려 김이 공중에서 시작한다.

대신 **여덟 칸의 픽셀별 중앙값**을 그릇으로 삼는다. 김은 칸마다 다른 자리에 있으므로
중앙값에는 안 남는다 — 한 자리에 김이 있는 칸이 절반을 넘지 않기 때문이다.

김은 그 그릇과 다른 부분이다.

  그릇이 비어 있는 자리(위쪽)  칸 그림을 그대로 가져온다
  그릇이 있는 자리(겹친 밑동)  밝기 차이만큼만 알파를 준다 — 김은 그릇 위에 옅게 얹힌 회색이라
                             차이가 곧 김의 짙기다
"""
import os
import sys

import numpy as np
from PIL import Image

SRC = 'Assets/Art/나머지/손님그릇.png'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/나머지'
TILE = 220
FRAMES = 8

# 그릇 위에 겹친 김을 알파로 되살릴 때 쓰는 배수. 1 이면 너무 옅어 밑동이 끊겨 보인다.
OVERLAP_GAIN = 2.6


def main():
    sheet = Image.open(SRC).convert('RGBA')
    frames = [np.asarray(sheet.crop((TILE * i, 0, TILE * (i + 1), TILE)), dtype=np.int16)
              for i in range(FRAMES)]

    # 그릇 — 픽셀별 최솟값. 김이 없는 상태가 가장 어둡고 가장 투명하다.
    base = np.min(np.stack(frames), axis=0).astype(np.int16)

    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(base.astype(np.uint8), 'RGBA').save(os.path.join(OUT, '손님그릇_바닥.png'))

    solid = base[:, :, 3] > 40                      # 그릇이 있는 자리

    steam = Image.new('RGBA', (TILE * FRAMES, TILE), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out = np.zeros((TILE, TILE, 4), dtype=np.int16)

        # 그릇이 없는 자리 — 칸 그림 그대로가 곧 김이다.
        out[~solid] = f[~solid]

        # 그릇이 있는 자리 — 밝아진 만큼이 김이다.
        lift = (f[:, :, :3].astype(np.float32) - base[:, :, :3].astype(np.float32)).mean(axis=2)
        a = np.clip(lift * OVERLAP_GAIN, 0, 255)
        over = solid & (a > 6)
        out[over, 0] = f[over, 0]
        out[over, 1] = f[over, 1]
        out[over, 2] = f[over, 2]
        out[over, 3] = a[over]

        steam.paste(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA'), (TILE * i, 0))

    steam.save(os.path.join(OUT, '손님그릇_김.png'))

    a = np.asarray(steam)[:, :, 3]
    print('손님그릇_바닥  %dx%d' % (TILE, TILE))
    print('손님그릇_김    %dx%d (%d칸)  알파 최대 %d' % (steam.width, steam.height, FRAMES, a.max()))


if __name__ == '__main__':
    main()
