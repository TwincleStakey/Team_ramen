# -*- coding: utf-8 -*-
"""먹는 연출용 임시 그림 세 장.
   - 큰번개.png  640x360  11시 → 5시로 화면을 가로지르는 한 방
   - 오우라.png  1280x160 160x160 칸 8개. 감동 오우라가 퍼지는 프레임
   - 따봉.png    40x40    엄지척
   손으로 그린 그림이 들어오면 같은 이름·같은 크기로 갈아 끼우기만 하면 된다."""
from PIL import Image, ImageDraw
import math, random

random.seed(1105)

def line_pixels(a, b):
    x0, y0 = a; x1, y1 = b
    dx, dy = abs(x1-x0), abs(y1-y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err, out = dx-dy, []
    while True:
        out.append((x0, y0))
        if x0 == x1 and y0 == y1: break
        e2 = 2*err
        if e2 > -dy: err -= dy; x0 += sx
        if e2 < dx:  err += dx; y0 += sy
    return out

def zigzag(a, b, steps, spread):
    pts = [a]
    ang = math.atan2(b[1]-a[1], b[0]-a[0]) + math.pi/2
    for i in range(1, steps):
        t = i/steps
        off = spread * (1 if i % 2 else -1) * random.uniform(0.55, 1.0)
        pts.append((int(round(a[0] + (b[0]-a[0])*t + math.cos(ang)*off)),
                    int(round(a[1] + (b[1]-a[1])*t + math.sin(ang)*off))))
    pts.append(b)
    return pts

def stroke(path, base, taper):
    walk = []
    for j in range(len(path)-1):
        walk += line_pixels(path[j], path[j+1])
    out = set()
    n = max(1, len(walk)-1)
    for i, (x, y) in enumerate(walk):
        r = int(round(base * (1.0 - (i/n) * taper)))
        for dy in range(-r, r+1):
            for dx in range(-r, r+1):
                if dx*dx + dy*dy <= r*r + 1:
                    out.add((x+dx, y+dy))
    return out

# ── 큰번개 : 11시 → 5시 한 방 ────────────────────────────────────────────
W, H = 960, 540   # 판 크기와 같아야 한다(DesignResolution)
big = Image.new('RGBA', (W, H), (0, 0, 0, 0))
bp = big.load()

EDGE = (46, 84, 190, 255)
GLOW = (126, 206, 255, 255)
CORE = (255, 255, 255, 255)

main = zigzag((177, 12), (774, 528), 7, 51)
paths = [(main, 5, 0.62)]
# 갈래 둘. 굵기를 반으로 줄여 본줄기가 묻히지 않게 한다.
paths.append((zigzag(main[2], (450, 180), 3, 30), 3, 0.75))
paths.append((zigzag(main[4], (645, 510), 3, 33), 3, 0.75))

for extra, color in ((2, EDGE), (1, GLOW), (0, CORE)):
    for path, base, taper in paths:
        for (x, y) in stroke(path, base + extra, taper):
            if 0 <= x < W and 0 <= y < H:
                bp[x, y] = color
big.save('Assets/Art/나머지/큰번개.png')
print('큰번개 %dx%d' % big.size)

# ── 오우라 : 8프레임 ────────────────────────────────────────────────────
CELL, FRAMES = 160, 8
aura = Image.new('RGBA', (CELL*FRAMES, CELL), (0, 0, 0, 0))
# 거의 흰색이면 금색으로 안 읽힌다. 노랑 쪽으로 확실히 당긴다.
GOLD = [(255, 236, 140, 255), (255, 188, 56, 255), (255, 132, 26, 255)]
cx = cy = CELL // 2

for f in range(FRAMES):
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(cell)
    t = f / (FRAMES - 1)

    # 고리 셋이 시차를 두고 퍼진다. 가늘면 오우라가 아니라 로딩 표시로 보여서 굵게 친다.
    for k in range(3):
        rt = t + k * 0.20
        if rt > 1.0 or rt < 0.02: continue
        r = int(10 + rt * 64)
        a = int(255 * (1.0 - rt) ** 0.7)
        c = GOLD[k % len(GOLD)][:3] + (a,)
        d.ellipse([cx-r, cy-r, cx+r, cy+r], outline=c, width=4 if k == 0 else 3)

    # 바깥으로 뻗는 빗살. 길고 굵어야 "솟는" 느낌이 난다.
    # 짧은 토막이라 대각선 계단이 눈에 안 띈다.
    for i in range(16):
        ang = math.pi * 2 * i / 16 + t * 0.2
        r0 = int(20 + t * 44)
        r1 = r0 + int(14 + (1.0 - t) * 16)
        a = int(255 * (1.0 - t * 0.8))
        d.line([cx + math.cos(ang)*r0, cy + math.sin(ang)*r0,
                cx + math.cos(ang)*r1, cy + math.sin(ang)*r1],
               fill=GOLD[i % 3][:3] + (a,), width=3)

    aura.paste(cell, (f*CELL, 0))
aura.save('Assets/Art/나머지/오우라.png')
print('오우라 %dx%d (%d칸)' % (aura.size + (FRAMES,)))

# ── 따봉 ────────────────────────────────────────────────────────────────
S = 40
thumb = Image.new('RGBA', (S, S), (0, 0, 0, 0))
silo = Image.new('L', (S, S), 0)
sd = ImageDraw.Draw(silo)
sd.rounded_rectangle([6, 17, 33, 35], radius=4, fill=255)     # 주먹
sd.rounded_rectangle([12, 4, 21, 22], radius=5, fill=255)     # 엄지
sd.ellipse([12, 3, 21, 12], fill=255)                        # 엄지 끝은 둥글게
sd.rounded_rectangle([6, 20, 14, 30], radius=3, fill=255)     # 엄지 뿌리

sp = silo.load(); tp = thumb.load()
SKIN, SHADE, LINE = (255, 214, 170, 255), (226, 166, 122, 255), (60, 36, 28, 255)
for y in range(S):
    for x in range(S):
        if sp[x, y]:
            tp[x, y] = SKIN if y < 26 else SHADE
# 1칸 테두리
for y in range(S):
    for x in range(S):
        if not sp[x, y]:
            if any(0 <= x+dx < S and 0 <= y+dy < S and sp[x+dx, y+dy]
                   for dx, dy in ((1,0),(-1,0),(0,1),(0,-1))):
                tp[x, y] = LINE
# 손가락 주름. 세 줄이면 바코드처럼 보인다. 두 줄만 짧게 긋는다.
td = ImageDraw.Draw(thumb)
td.line([19, 26, 30, 26], fill=LINE, width=1)
td.line([19, 31, 30, 31], fill=LINE, width=1)
thumb.save('Assets/Art/나머지/따봉.png')
print('따봉 %dx%d' % thumb.size)
