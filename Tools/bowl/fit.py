# 손으로 잡은 구도를 경계 안으로 밀어 넣는다. 구도는 그대로 두고 위반한 것만 당긴다.
import sys; sys.path.insert(0,'.')
from bowlpreview import *

BOUNDS = bowl_bounds()
CX, CY, RX, RY = BROTH
FLOATS = ('GreenOnion', 'BeanSprout', 'WoodEar')   # 국물에 떠 있는 것 = 타원도 지켜야

def violate(ing, x, y, ang):
    """(그릇 밖 픽셀, 국물 밖 픽셀)"""
    src = sprite(ing)
    img = src.rotate(ang, resample=Image.NEAREST, expand=True, fillcolor=(0,0,0,0)) if ang else src
    w, h = img.size
    ox, oy = round(64 + x - w/2), round(64 - y - h/2)
    ip = img.load()
    hard = soft = 0
    for yy in range(h):
        lo, hi = BOUNDS.get(oy+yy, (999, -999))
        for xx in range(w):
            if ip[xx, yy][3] <= 128: continue
            gx = ox + xx
            if gx < lo or gx > hi: hard += 1
            ux = gx - 64 + 0.5
            uy = 64 - (oy + yy) - 0.5
            if ((ux-CX)/RX)**2 + ((uy-CY)/RY)**2 > 1: soft += 1
    return hard, soft

def fit(ing, spot, pull=(0.0, 2.0)):
    x, y, ang = spot[0], spot[1], spot[2]
    need_soft = ing in FLOATS
    for _ in range(60):
        hard, soft = violate(ing, x, y, ang)
        if hard == 0 and (soft == 0 or not need_soft):
            return (round(x), round(y), ang), hard, soft
        dx, dy = pull[0]-x, pull[1]-y
        n = max(1e-6, (dx*dx+dy*dy)**0.5)
        x += dx/n; y += dy/n
    return (round(x), round(y), ang), hard, soft

def fit_all(layout):
    out, report = {}, []
    for ing, spots in layout.items():
        fixed = []
        for s in spots:
            f, hard, soft = fit(ing, s)
            moved = (abs(f[0]-s[0]) + abs(f[1]-s[1]))
            fixed.append(f)
            if moved: report.append('%s %s → %s (%d칸 이동)' % (KO[ing], s[:2], f[:2], moved))
        out[ing] = fixed
    return out, report
