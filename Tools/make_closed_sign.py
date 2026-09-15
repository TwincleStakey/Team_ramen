# -*- coding: utf-8 -*-
"""「주문마감」 붓글씨를 네 글자로 갈라 굽는다.

원본은 손으로 그린 붓글씨 한 장(`_ArtSource/주문마감_원본.png`)이다. 로고와 같은 결이라
게임 화면에 그대로 쓰지만, 한 글자씩 퉁 하고 박히는 연출을 하려면 글자가 따로 놀아야 한다.

  1. 여백을 잘라 내고 캔버스 좌표 폭(WIDTH)으로 한 번에 줄인다.
     → 줄이는 건 여기서 끝낸다. 유니티에서 줄이면 확대 배율이 정수에서 어긋나 획이 흐려진다.
  2. 글자 사이의 빈 세로줄을 찾아 넷으로 가른다.
  3. 글자마다 여백을 바짝 잘라 따로 저장하고, 말 한가운데를 기준으로 한 자리(칸)를 찍어 준다.
     빌더가 그 자리에 그대로 놓으면 원본과 똑같이 늘어선다.

마지막에 네 장을 그 자리대로 도로 겹쳐 원본과 같은지 검산한 그림을 함께 남긴다.
"""
import os
from PIL import Image, ImageChops

SRC = "_ArtSource/주문마감_원본.png"
OUT = "Assets/Art/UI"
NAME = "주문마감"

# 캔버스 좌표(960x540) 기준 글자 전체 폭. 화면의 73%.
WIDTH = 700

# 이만큼 이어지는 빈 세로줄을 글자 사이로 본다. 획 사이 틈보다 넉넉히 넓게 잡는다.
GAP = 3

# 알파가 이보다 옅으면 빈 칸으로 친다. 붓끝의 옅은 자락까지 글자로 세면 틈이 안 보인다.
FAINT = 8


def columns(im):
    """세로줄마다 가장 진한 알파값."""
    a = im.getchannel("A")
    return [max(a.crop((x, 0, x + 1, im.height)).getextrema()) for x in range(im.width)]


def split_points(cols):
    """빈 구간의 한가운데를 자를 자리로 돌려준다."""
    cuts, start = [], None
    for x, v in enumerate(cols + [255]):
        if v < FAINT:
            if start is None:
                start = x
        else:
            if start is not None:
                if x - start >= GAP and start > 0:
                    cuts.append((start + x) // 2)
                start = None
    return cuts


def main():
    im = Image.open(SRC).convert("RGBA")
    im = im.crop(im.getbbox())

    height = round(im.height * WIDTH / im.width)
    word = im.resize((WIDTH, height), Image.LANCZOS)

    # 세로도 짝수로 맞춰 둔다. 글자 자리를 말 한가운데에서 재는데, 홀수면 그 한가운데가
    # 반칸에 걸려 자리가 정수로 안 떨어진다.
    if height % 2:
        padded = Image.new("RGBA", (WIDTH, height + 1), (0, 0, 0, 0))
        padded.paste(word, (0, 0))
        word, height = padded, height + 1

    cuts = split_points(columns(word))
    if len(cuts) != 3:
        raise SystemExit("글자 사이를 셋으로 가르지 못했습니다: %r "
                         "(GAP/FAINT 를 손보거나 원본을 확인할 것)" % cuts)

    os.makedirs(OUT, exist_ok=True)
    bounds = [0] + cuts + [WIDTH]
    placed = []

    for i in range(4):
        cell = word.crop((bounds[i], 0, bounds[i + 1], height))
        box = list(cell.getbbox())                 # 글자에 바짝 붙여 자른다

        # 가로·세로를 짝수로 맞춘다. 홀수면 글자 한가운데가 반칸에 걸려 자리가
        # 정수로 안 떨어지고, 반칸 어긋난 채로 놓여 획 가장자리가 흐려진다.
        if (box[2] - box[0]) % 2:
            if box[2] < cell.width:
                box[2] += 1
            else:
                box[0] -= 1
        if (box[3] - box[1]) % 2:
            if box[3] < cell.height:
                box[3] += 1
            else:
                box[1] -= 1
        glyph = cell.crop(tuple(box))

        # 말 한가운데를 원점으로 한 글자 중심. 유니티 좌표라 y 는 위가 +.
        cx = bounds[i] + box[0] + glyph.width / 2 - WIDTH / 2
        cy = height / 2 - (box[1] + glyph.height / 2)

        path = "%s/%s_%d.png" % (OUT, NAME, i)
        glyph.save(path)
        placed.append((glyph, round(cx), round(cy)))
        print("%s  크기 %dx%d  자리 (%d, %d)" % (path, glyph.width, glyph.height, round(cx), round(cy)))

    print("\n// RamenLayoutBuilder 에 넣을 표 (Tools/make_closed_sign.py 가 찍어 준다)")
    print("    private static readonly Vector2[] ClosedGlyphSizes =\n    {")
    for g, _, _ in placed:
        print("        new Vector2(%df, %df)," % (g.width, g.height))
    print("    };")
    print("    private static readonly Vector2[] ClosedGlyphSpots =\n    {")
    for _, cx, cy in placed:
        print("        new Vector2(%df, %df)," % (cx, cy))
    print("    };")

    # 검산: 네 장을 그 자리대로 도로 겹쳐 원본과 견준다.
    redone = Image.new("RGBA", word.size, (0, 0, 0, 0))
    for g, cx, cy in placed:
        redone.alpha_composite(g, (round(WIDTH / 2 + cx - g.width / 2),
                                   round(height / 2 - cy - g.height / 2)))
    diff = ImageChops.difference(word.convert("RGB"), redone.convert("RGB"))
    print("\n합쳐 본 것과 원본의 차이(0이면 똑같다):", diff.getbbox() and diff.getextrema())


main()
