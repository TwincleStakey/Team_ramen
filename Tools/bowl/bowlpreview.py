# Bowl.cs 의 AddIcon 계산을 그대로 옮긴 미리보기 렌더러.
# 좌표는 그릇 원본(128px) 기준. Unity 쪽 BowlPixelScale(2배)는 마지막에 한 번만 곱한다.
import math, os
from PIL import Image

ROOT = r'C:/UnityProject/Team_ramen/Assets/Art'
ING  = ROOT + '/재료/'
BOWL = ROOT + '/그릇/'

ART = {
    'Nori':       '김 그릇용.png',
    'Egg':        '계란 그릇용.png',
    'Menma':      '멘마 그릇용.png',
    'Chashu':     '차슈 그릇용.png',
    'BeanSprout': '숙주 그릇용.png',
    'WoodEar':    '목이버섯 그릇용.png',
    'GreenOnion': '파 그릇용.png',
}
KO = {'Nori':'김','Egg':'계란','Menma':'멘마','Chashu':'차슈',
      'BeanSprout':'숙주','WoodEar':'목이버섯','GreenOnion':'파'}

# 기본 레시피. 돈코츠에 김 1회는 기획서 v1.2 4.1 에서 왔다.
BASE = {
    'Shio':     {'Chashu':1,'Menma':2,'GreenOnion':1},
    'Shoyu':    {'Chashu':2,'Menma':1,'GreenOnion':1},
    'Tonkotsu': {'Chashu':1,'Egg':1,'BeanSprout':1,'WoodEar':1,'Nori':1,'GreenOnion':1},
}
SHEET = {'Shio':'Sio_Ani.png','Shoyu':'Syo_Ani.png','Tonkotsu':'Don_Ani.png'}

# 투입 상한은 전 재료 4다(기획서 v1.2 3.2·10.1·11.3).
# 예전에는 "그 메뉴 기본 수량 + 3"이었고, 자리표가 그 값에 맞춰져 있었다.
MAX_TOPPING = 4

def cap(menu, ing):
    return MAX_TOPPING

_cache = {}
def sprite(ing):
    if ing not in _cache:
        _cache[ing] = Image.open(ING + ART[ing]).convert('RGBA')
    return _cache[ing]

def bowl_base(menu):
    """붓기 시트의 마지막 프레임(면까지 담긴 정지 그림). 4열 x 2행 중 7번."""
    sh = Image.open(BOWL + SHEET[menu]).convert('RGBA')
    return sh.crop((3 * 128, 128, 4 * 128, 256))

def render(menu, counts, layouts, depths, scale=3, bg=(58, 52, 48)):
    """counts: {재료: 개수}. layouts: {재료: [(x,y,angle,scale), ...]}"""
    canvas = Image.new('RGBA', (128, 128), bg + (255,))
    canvas.alpha_composite(bowl_base(menu))

    placed = []   # (depth, 순서, 이미지, 붙일 위치)
    order = 0
    for ing, n in counts.items():
        if n <= 0:
            continue
        spots = layouts[ing]
        src = sprite(ing)
        side = src.size[0]
        for i in range(n):
            spot = spots[i % len(spots)]
            lap = i // len(spots)
            x, y, ang = spot[0], spot[1], spot[2]
            sc = spot[3] if len(spot) > 3 else 1.0
            x += 2 * lap
            y -= 2 * lap
            ang += 5 * lap

            img = src
            if sc != 1.0:
                s = max(1, round(side * sc))
                img = src.resize((s, s), Image.NEAREST)
            if ang:
                # PIL 의 회전은 화면에서 반시계. Unity 의 +Z 와 같은 방향이다.
                img = img.rotate(ang, resample=Image.NEAREST, expand=True,
                                 fillcolor=(0, 0, 0, 0))
            w, h = img.size
            # anchoredPosition 은 y 가 위로 +. 이미지 좌표는 아래로 + 라 뒤집는다.
            pos = (round(64 + x - w / 2), round(64 - y - h / 2))
            placed.append((depths[ing], order, img, pos))
            order += 1

    placed.sort(key=lambda p: (p[0], p[1]))
    for _, _, img, pos in placed:
        canvas.alpha_composite(img, pos)

    return canvas.resize((128 * scale, 128 * scale), Image.NEAREST).convert('RGB')

def grid(images, cols, pad=6, bg=(20, 20, 24)):
    if not images:
        return None
    w, h = images[0].size
    rows = (len(images) + cols - 1) // cols
    out = Image.new('RGB', (cols * w + (cols + 1) * pad, rows * h + (rows + 1) * pad), bg)
    for i, im in enumerate(images):
        r, c = divmod(i, cols)
        out.paste(im, (pad + c * (w + pad), pad + r * (h + pad)))
    return out


# ── 경계 ────────────────────────────────────────────────────────
# 하드: 그릇 실루엣을 5픽셀 안쪽으로 민 것. 여기를 넘으면 그릇 밖에 뜬 것처럼 보인다.
# 소프트: 국물 면 타원. 떠 있는 재료(파·숙주·목이버섯)는 이 안에 있어야 한다.
INSET = 5
ROW_MIN, ROW_MAX = 26, 82
BROTH = (0.0, 2.0, 47.0, 21.5)   # UI 좌표 중심(x,y), 반지름(rx,ry)

def bowl_bounds(menu='Tonkotsu'):
    """줄마다 (왼쪽, 오른쪽) 허용 x. 그릇 원본 좌표."""
    px = bowl_base(menu).load()
    out = {}
    for y in range(ROW_MIN, ROW_MAX + 1):
        xs = [x for x in range(128) if px[x, y][3] > 128]
        if xs:
            out[y] = (min(xs) + INSET, max(xs) - INSET)
    return out

def check(counts, layouts, menu='Tonkotsu'):
    """재료별로 그릇 밖으로 나간 픽셀 수를 센다."""
    bounds = bowl_bounds(menu)
    bad = {}
    for ing, n in counts.items():
        if n <= 0:
            continue
        spots = layouts[ing]
        src = sprite(ing)
        worst = 0
        for i in range(n):
            spot = spots[i % len(spots)]
            lap = i // len(spots)
            x, y, ang = spot[0] + 2 * lap, spot[1] - 2 * lap, spot[2] + 5 * lap
            img = src.rotate(ang, resample=Image.NEAREST, expand=True,
                             fillcolor=(0, 0, 0, 0)) if ang else src
            w, h = img.size
            ox, oy = round(64 + x - w / 2), round(64 - y - h / 2)
            ip = img.load()
            out = 0
            for yy in range(h):
                gy = oy + yy
                lo, hi = bounds.get(gy, (999, -999))
                for xx in range(w):
                    if ip[xx, yy][3] > 128:
                        gx = ox + xx
                        if gx < lo or gx > hi:
                            out += 1
            worst = max(worst, out)
        if worst:
            bad[ing] = worst
    return bad

def floats_outside_broth(counts, layouts):
    """국물 위에 떠 있어야 할 재료가 국물 면을 벗어난 픽셀 수."""
    cx, cy, rx, ry = BROTH
    bad = {}
    for ing in ('GreenOnion', 'BeanSprout', 'WoodEar'):
        n = counts.get(ing, 0)
        if n <= 0:
            continue
        src = sprite(ing)
        worst = 0
        for i in range(n):
            spot = layouts[ing][i % len(layouts[ing])]
            lap = i // len(layouts[ing])
            x, y, ang = spot[0] + 2 * lap, spot[1] - 2 * lap, spot[2] + 5 * lap
            img = src.rotate(ang, resample=Image.NEAREST, expand=True,
                             fillcolor=(0, 0, 0, 0)) if ang else src
            w, h = img.size
            ip = img.load()
            out = 0
            for yy in range(h):
                for xx in range(w):
                    if ip[xx, yy][3] > 128:
                        ux = x - w / 2 + xx + 0.5
                        uy = y + h / 2 - yy - 0.5
                        if ((ux - cx) / rx) ** 2 + ((uy - cy) / ry) ** 2 > 1:
                            out += 1
            worst = max(worst, out)
        if worst:
            bad[ing] = worst
    return bad
