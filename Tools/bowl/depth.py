# 2단계(원근)와 3단계(잠김)를 미리보기에 얹는다.
import sys; sys.path.insert(0,'.')
from bowlpreview import *
from PIL import Image

# 재료별 잠김 비율 — 내용 높이의 아래 몇 할이 국물 아래인가
SUBMERGE = {'Nori':0.25, 'GreenOnion':0.05, 'Chashu':0.33, 'Egg':0.33,
            'Menma':0.50, 'BeanSprout':0.50, 'WoodEar':0.85}

def broth_color(menu):
    """그 메뉴 국물의 대표색."""
    px = bowl_base(menu).load()
    from collections import Counter
    c = Counter(px[x, y] for x in range(40, 90) for y in range(64, 76) if px[x, y][3] > 200)
    return c.most_common(1)[0][0][:3]

def tint_below(img, frac, color, alpha=0.55, darken=0.82):
    """내용 영역의 아래 frac 만큼을 국물색 쪽으로 섞는다. 위쪽은 그대로 둔다."""
    if frac <= 0:
        return img
    bb = img.getchannel('A').getbbox()
    if bb is None:
        return img
    top, bot = bb[1], bb[3]
    line = bot - (bot - top) * frac          # 이 y 아래가 잠긴 부분
    out = img.copy()
    p = out.load()
    for y in range(top, bot):
        if y < line:
            continue
        # 수면 바로 아래가 가장 옅고, 깊을수록 진하게
        k = alpha * min(1.0, 0.45 + 0.55 * (y - line) / max(1.0, bot - line))
        for x in range(bb[0], bb[2]):
            r, g, b, a = p[x, y]
            if a == 0:
                continue
            p[x, y] = (int((r * (1 - k) + color[0] * k) * darken),
                       int((g * (1 - k) + color[1] * k) * darken),
                       int((b * (1 - k) + color[2] * k) * darken), a)
    return out

def shade(img, k):
    """멀리 있는 것을 살짝 어둡게. 크기를 안 건드리므로 픽셀이 안 깨진다."""
    if k == 1.0:
        return img
    out = img.copy(); p = out.load(); w, h = out.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = p[x, y]
            if a:
                p[x, y] = (int(r*k), int(g*k), int(b*k), a)
    return out

def render2(menu, counts, layouts, depths, scale=3, persp='none', submerge=True,
            bg=(58,52,48)):
    """persp: 'none' | 'size'(크기) | 'shade'(밝기)"""
    canvas = Image.new('RGBA', (128,128), bg+(255,))
    canvas.alpha_composite(bowl_base(menu))
    col = broth_color(menu)

    placed, order = [], 0
    for ing, n in counts.items():
        if n <= 0: continue
        spots, src = layouts[ing], sprite(ing)
        for i in range(n):
            spot = spots[i % len(spots)]
            lap = i // len(spots)
            x, y, ang = spot[0]+2*lap, spot[1]-2*lap, spot[2]+5*lap

            img = src
            if persp == 'size':
                s = 1.0 - y * 0.006                      # 뒤(y 큼)는 작게, 앞은 크게
                side = max(1, round(src.size[0] * s))
                img = src.resize((side, side), Image.NEAREST)
            if ang:
                img = img.rotate(ang, resample=Image.NEAREST, expand=True, fillcolor=(0,0,0,0))
            if submerge:
                img = tint_below(img, SUBMERGE.get(ing, 0.0), col)
            if persp == 'shade':
                img = shade(img, 1.0 - y * 0.004)        # 뒤는 어둡게

            w, h = img.size
            placed.append((depths[ing], order, img,
                           (round(64+x-w/2), round(64-y-h/2))))
            order += 1
    placed.sort(key=lambda p: (p[0], p[1]))
    for _, _, img, pos in placed:
        canvas.alpha_composite(img, pos)
    return canvas.resize((128*scale, 128*scale), Image.NEAREST).convert('RGB')
