# -*- coding: utf-8 -*-
"""그릇에 얹는 토핑 30칸(토핑배치.png)을 굽는다.

자리와 기울기는 final.json, 잠기는 깊이는 submerge.json 이 정한다.
둘 다 전 세션이 미리보기 렌더러로 잡아 둔 값이다.

잠긴 부분을 "지우지 않고 옅게" 남기는 것이 이 판의 요점이다.
  지우면      국물이 그대로 비쳐 깨끗하지만 재료가 어디서 끊겼는지 안 보인다
  칠하면      국물색을 섞어야 해서 메뉴 3종마다 시트가 따로 필요해진다
  옅게 남기면 밑에 있는 국물색이 그대로 비쳐 올라와 한 장으로 세 메뉴를 다 쓴다
"""
from PIL import Image
import json, os

ROOT = 'Assets/Art'
ING  = ROOT + '/재료/'
OUT  = ROOT + '/그릇/토핑배치.png'
HERE = os.path.dirname(os.path.abspath(__file__))

ART = {'Nori':'김 그릇용.png', 'Egg':'계란 그릇용.png', 'Menma':'멘마 그릇용.png',
       'Chashu':'차슈 그릇용.png', 'BeanSprout':'숙주 그릇용.png',
       'WoodEar':'목이버섯 그릇용.png', 'GreenOnion':'파 그릇용.png'}

COLS = 6

# 잠긴 부분에 남길 진하기. 0이면 예전처럼 아예 지우는 것과 같다.
# 0.22 면 형태만 겨우 읽히고 국물이 대부분 비쳐 올라온다.
SUNK_ALPHA = 0.22

# 잠긴 부분은 조금 어둡게 깔아야 국물 아래로 들어간 것처럼 보인다.
SUNK_DARKEN = 0.72

def fade_below(img, frac, alpha, darken):
    """내용 영역의 아래 frac 만큼을 옅게 만든다. 수면에 가까울수록 진하다."""
    if frac <= 0:
        return img
    bb = img.getchannel('A').getbbox()
    if bb is None:
        return img

    top, bot = bb[1], bb[3]
    line = bot - (bot - top) * frac     # 이 y 아래가 국물 아래다
    out = img.copy()
    p = out.load()

    for y in range(top, bot):
        if y < line:
            continue
        # 수면 바로 아래가 가장 잘 보이고, 깊을수록 흐려진다
        deep = (y - line) / max(1.0, bot - line)
        k = alpha * (1.0 - 0.45 * deep)
        for x in range(bb[0], bb[2]):
            r, g, b, a = p[x, y]
            if a == 0:
                continue
            p[x, y] = (int(r * darken), int(g * darken), int(b * darken), int(a * k))
    return out

def bake(sunk_alpha=SUNK_ALPHA, darken=SUNK_DARKEN, out_path=OUT):
    final = json.load(open(os.path.join(HERE, 'final.json'), encoding='utf-8'))
    sub = json.load(open(os.path.join(HERE, 'submerge.json'), encoding='utf-8'))
    cell, order, spots = final['cell'], final['order'], final['final']

    total = sum(len(spots[i]) for i in order)
    rows = (total + COLS - 1) // COLS
    sheet = Image.new('RGBA', (COLS * cell, rows * cell), (0, 0, 0, 0))

    i = 0
    index = {}
    for ing in order:
        index[ing] = i
        src = Image.open(ING + ART[ing]).convert('RGBA')
        for (x, y, ang) in spots[ing]:
            img = src.rotate(ang, resample=Image.NEAREST, expand=True,
                             fillcolor=(0, 0, 0, 0)) if ang else src
            img = fade_below(img, sub.get(ing, 0.0), sunk_alpha, darken)

            cx, cy = (i % COLS) * cell + cell // 2, (i // COLS) * cell + cell // 2
            sheet.alpha_composite(img, (cx - img.width // 2, cy - img.height // 2))
            i += 1

    sheet.save(out_path)
    return sheet, index, total

if __name__ == '__main__':
    sheet, index, total = bake()
    print('%s  %dx%d  %d칸' % (OUT, sheet.width, sheet.height, total))
    print('시작 칸:', ', '.join('%s %d' % kv for kv in index.items()))
