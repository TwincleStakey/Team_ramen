# -*- coding: utf-8 -*-
"""손님 리뉴얼 그림(397px)을 화면에 쓸 크기(300px)로 구워 낸다.

원본은 Assets/Art/손님리뉴얼 에 손님마다 흩어져 있다.
  01~07  낱장 프레임 여러 개 + 미리보기 gif
  08~14  가로로 이어 붙인 3프레임 시트 한 장

여기서 손님 한 명당 가로 시트 한 장으로 통일해 Assets/Art/손님 에 굽는다.
파일 이름은 대사 DB 의 personaId 와 같게 둔다 — 런타임에서 스프라이트 이름 앞머리로
어느 손님 것인지 찾기 때문이다(CustomerAppearance).

**그림 번호와 personaId 번호는 서로 어긋난다.** 그림 09는 할머니인데 DB 09는 어린이다.
번호로 짝지으면 전부 틀어지므로 아래 표처럼 이름으로 맞춘다.

런타임에 줄이지 않고 여기서 굽는 이유: 397을 0.75배로 줄이면 획이 반칸에 걸려 뭉개진다.
줄이는 것은 한 번만, 좋은 필터로.
"""
from PIL import Image
import os, glob

SRC = 'Assets/Art/손님리뉴얼'
OUT = 'Assets/Art/손님'
CELL = 300

# personaId : 원본이 있는 곳. 폴더면 낱장, 파일이면 3프레임 시트.
SOURCES = [
    ('Polite',      '01_polite'),
    ('Formal',      '02_formal'),
    ('Gyeongsang',  '03_gyeongsang'),
    ('Chungcheong', '04_chungcheong'),
    ('Jeolla',      '05_jeolla'),
    ('Otaku',       '06_otaku'),
    ('Military',    '07_military'),
    ('Sageuk',      '08_sageuk_blink_3frames.png'),
    ('Grandma',     '09_grandma_3frames.png'),
    ('Grandpa',     '10_grandpa_3frames.png'),
    ('Youtuber',    '11_youtuber_3frames.png'),
    ('Gourmet',     '12_gourmet_3frames.png'),
    ('Emotional',   '13_emotional_3frames.png'),
    ('Child',       '14_child_3frames.png'),
]


def frames_from_folder(path):
    """낱장 프레임. 파일 이름 순서가 곧 프레임 순서다."""
    files = sorted(p for p in glob.glob(os.path.join(path, '*.png')))
    return [Image.open(p).convert('RGBA') for p in files]


def frames_from_sheet(path, count=3):
    """가로로 이어 붙인 시트를 칸수로 나눈다."""
    sheet = Image.open(path).convert('RGBA')
    w = sheet.width // count
    return [sheet.crop((i * w, 0, (i + 1) * w, sheet.height)) for i in range(count)]


STEAM_ALPHA = 0.30


def fade_steam(frame):
    """그릇 위로 피어오르는 김만 옅게 만든다.

    김과 그릇은 줄 단위로 갈린다 — 김은 가는 획이라 한 줄에 몇십 픽셀뿐이고,
    그릇은 테두리가 넓어 한 줄에 300픽셀 넘게 찬다. 그 급한 층계가 경계다.
    색으로 가르려 하면 김도 그릇 몸통도 흰색이라 안 갈린다.
    """
    px = frame.load()
    counts = [sum(1 for x in range(frame.width) if px[x, y][3] > 20) for y in range(frame.height)]
    widest = max(counts) if counts else 0
    if widest == 0:
        return frame

    bowl_top = frame.height
    for y, n in enumerate(counts):
        if n > widest * 0.4:
            bowl_top = y
            break

    out = frame.copy()
    q = out.load()
    for y in range(bowl_top):
        for x in range(out.width):
            r, g, b, a = q[x, y]
            if a: q[x, y] = (r, g, b, int(a * STEAM_ALPHA))

    return out


def bake_bowl():
    """손님 앞에 놓는 김 나는 그릇. 4열 2행 8프레임을 한 줄로 편다.

    원본 칸이 443.5 로 딱 안 떨어져서 줄마다 반 칸씩 어긋난다. 칸을 반올림해 잘라 내고
    화면에 쓸 크기로 줄여 둔다 — 유니티 쪽은 칸이 정수여야 시트를 자를 수 있다.
    """
    src = 'Assets/Art/위치미정_손님라멘대접그릇.png'
    if not os.path.exists(src):
        print('!! 손님 그릇 원본이 없다: ' + src)
        return

    sheet = Image.open(src).convert('RGBA')

    # 화면에 쓸 크기 그대로 굽는다. RamenLayoutBuilder.CustomerBowlSize 와 같아야 한다.
    # 런타임에 늘리면 정수배가 아니라 획이 반칸에 걸린다.
    cols, rows, size = 4, 2, 220
    cw, chh = sheet.width / cols, sheet.height / rows

    out = Image.new('RGBA', (size * cols * rows, size), (0, 0, 0, 0))
    for i in range(cols * rows):
        col, row = i % cols, i // cols
        box = (int(round(col * cw)), int(round(row * chh)),
               int(round((col + 1) * cw)), int(round((row + 1) * chh)))
        frame = fade_steam(sheet.crop(box))
        out.alpha_composite(frame.resize((size, size), Image.LANCZOS), (i * size, 0))

    path = 'Assets/Art/나머지/손님그릇.png'
    out.save(path)
    print('손님그릇     %d프레임  %s' % (cols * rows, path))


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    for persona, name in SOURCES:
        path = os.path.join(SRC, name)
        frames = frames_from_folder(path) if os.path.isdir(path) else frames_from_sheet(path)

        if not frames:
            print('!! %s — 프레임을 못 찾음: %s' % (persona, path))
            continue

        shrunk = [f.resize((CELL, CELL), Image.LANCZOS) for f in frames]

        # 머리 위 빈 줄을 잘라 낸다. 그러면 스프라이트 높이가 곧 "머리 꼭대기"가 되어,
        # 말풍선을 손님 머리 위에 올릴 때 런타임에서 픽셀을 읽어 볼 필요가 없다.
        # 프레임마다 따로 자르면 대기 동작에서 머리가 아래위로 튀므로, 가장 위를 기준으로 다 같이 자른다.
        top = CELL
        for f in shrunk:
            box = f.getchannel('A').getbbox()
            if box:
                top = min(top, box[1])

        height = CELL - top
        sheet = Image.new('RGBA', (CELL * len(shrunk), height), (0, 0, 0, 0))
        for i, frame in enumerate(shrunk):
            sheet.alpha_composite(frame.crop((0, top, CELL, CELL)), (i * CELL, 0))

        out = os.path.join(OUT, persona + '.png')
        sheet.save(out)
        print('%-12s %d프레임  높이 %d  %s' % (persona, len(shrunk), height, out))

    bake_bowl()


if __name__ == '__main__':
    main()
