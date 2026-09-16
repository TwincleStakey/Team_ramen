# -*- coding: utf-8 -*-
"""손님 얼굴 도장 — 주문 결과창 대사 왼쪽에 붙는 작은 초상.

## 왜 굽는가

손님 그림(`Assets/Art/손님/*.png`)은 한 장이 300x280 인데 여기서는 40칸으로 쓴다.
그 그림은 **Point 필터로 들어와 있어서**(meta `filterMode: 0`) 유니티가 줄이면 픽셀을
그냥 건너뛰며 골라 쓴다 — 눈 한 줄이 통째로 사라진다. 여기서 LANCZOS 로 미리 줄여 둔다.

## 테를 같이 굽는다

전표에 증명사진을 붙인 모양이다. 흰 여백 2칸 + 판 테두리와 같은 먹갈색 테 2칸이라
얼굴 40칸에 다 해서 48칸이 된다. 유니티에서 판을 따로 깔지 않고 한 장으로 끝낸다.

## 어디를 자르나

`CustomerAppearance` 가 쓰는 값을 그대로 가져왔다.

  머리 꼭대기 = 그림 위끝   (bake_customers.py 가 머리 위 빈 줄을 잘라 구웠다)
  귀          = 그림 높이의 0.44 지점   (`EarFromTop`, 손님 열넷을 재서 잡은 중앙값)

얼굴은 위끝에서 귀까지가 절반이므로, 높이는 `귀 x 2` 다. 가로 가운데는 **머리 구간만**
불투명 픽셀을 훑어 잡는다. 그림 전체로 잡으면 어깨가 넓은 쪽으로 얼굴이 밀린다.

## 눈 뜬 장

첫 장이 눈 뜬 장이다. **충청만 거꾸로 구워져 있어**(`CustomerAppearance.reversedEyes`)
마지막 장을 쓴다. 전라는 두 장 다 감은 눈이라 여기서도 감고 있다 — 그림 쪽 일이다.
"""
import os
import sys

from PIL import Image, ImageDraw

SRC = 'Assets/Art/손님'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated/손님얼굴'

FRAME_W = 300
EAR_FROM_TOP = 0.44
MARGIN = 1.12          # 얼굴 둘레 여유. 1.0 이면 귀와 턱이 잘린다
SIZE = 40              # 얼굴 한 변(칸)
PAD = 2                # 얼굴 둘레 흰 여백
BORDER = 2             # 테 두께

MATTE = (255, 253, 243, 255)   # 종이보다 한 단 밝다. 붙인 사진으로 읽힌다
RING = (58, 26, 12, 255)       # 주문결과판 테두리와 같은 색

REVERSED = {'Chungcheong'}   # 첫 장이 눈 감은 장


def head_box(frame):
    """(왼, 위, 오른, 아래) — 얼굴을 담을 정사각형."""
    w, h = frame.size
    ear = h * EAR_FROM_TOP

    # 머리 구간(위끝~귀)에서만 가로 폭을 잡는다.
    band = frame.crop((0, 0, w, int(ear))).getbbox()
    if band is None:
        band = (0, 0, w, int(ear))
    cx = (band[0] + band[2]) / 2.0

    side = ear * 2.0 * MARGIN
    return (cx - side / 2.0, ear - side / 2.0, cx + side / 2.0, ear + side / 2.0)


def thumb(path, persona):
    sheet = Image.open(path).convert('RGBA')
    count = sheet.width // FRAME_W
    index = count - 1 if persona in REVERSED else 0
    frame = sheet.crop((index * FRAME_W, 0, (index + 1) * FRAME_W, sheet.height))

    # 자를 자리가 그림 밖으로 나가면 투명으로 메운다. 얼굴이 한쪽으로 쏠리지 않게.
    box = head_box(frame)
    side = int(round(box[2] - box[0]))
    canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(frame, (int(round(-box[0])), int(round(-box[1]))))

    face = canvas.resize((SIZE, SIZE), Image.LANCZOS)

    side = SIZE + (PAD + BORDER) * 2
    out = Image.new('RGBA', (side, side), MATTE)
    ImageDraw.Draw(out).rectangle([0, 0, side - 1, side - 1], outline=RING, width=BORDER)
    out.alpha_composite(face, (PAD + BORDER, PAD + BORDER))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)

    for name in sorted(os.listdir(SRC)):
        if not name.endswith('.png'):
            continue
        persona = name[:-4]
        thumb(os.path.join(SRC, name), persona).save(os.path.join(OUT, persona + '.png'))
        print(persona)


if __name__ == '__main__':
    main()
