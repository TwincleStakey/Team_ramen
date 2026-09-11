# -*- coding: utf-8 -*-
"""3컷(반응) 연출용 임시 그림 두 장을 픽셀로 찍는다.
   - 우주 배경 640x360 : 판(640x360)과 같은 크기. 1칸 = 1픽셀 규칙 그대로.
   - 번개 384x64      : 64x64 칸 6개. LoadSpriteSheet(path, 64, 64) 로 잘린다.
   손으로 그린 그림이 들어오면 이 파일을 갈아 끼우기만 하면 된다."""
from PIL import Image
import random, math

random.seed(20260910)   # 돌릴 때마다 별자리가 바뀌면 곤란하다

# ── 우주 배경 ────────────────────────────────────────────────────────────
# 판(960x540)이 아니라 네모로 찍는다. 연출에서 이 판을 천천히 돌리는데,
# 가로세로가 다르면 돌릴 때 구석이 비어 검은 삼각형이 드러난다.
# 한 변은 판의 대각선(√(960²+540²) ≈ 1101)보다 커야 한다.
#
# 별은 1픽셀이 아니라 2x2로 찍는다. 돌아가는 동안 1픽셀 점은 화면 격자에 걸렸다 말았다 하며
# 깜빡이는데, 2x2면 그 떨림이 눈에 안 띈다.
W = H = 1104
sky = Image.new('RGBA', (W, H), (0, 0, 0, 255))
px = sky.load()

# 가운데가 옅고 가장자리가 짙은 단계형 하늘. 픽셀아트라 색을 5단으로 끊고
# 경계는 체크무늬로 섞는다(디더). 부드러운 그라데이션은 색이 수백 개가 되어 겉돈다.
BANDS = [(10, 6, 24), (22, 12, 46), (38, 20, 74), (58, 30, 104), (82, 44, 134)]
# 돌아가는 판이라 한가운데를 중심으로 잡는다. 중심이 어긋나면 띠가 통째로 흔들린다.
cx, cy = W * 0.5, H * 0.5
maxd = math.hypot(cx, cy)

for y in range(H):
    for x in range(W):
        d = math.hypot(x - cx, y - cy) / maxd          # 0(가운데) ~ 1(구석)
        t = (1.0 - d) * (len(BANDS) - 1)
        i = int(t)
        i = max(0, min(len(BANDS) - 2, i))
        # 두 단 사이는 체크무늬로 섞는다
        frac = t - i
        up = ((x + y) % 2 == 0) if frac > 0.5 else ((x % 2 == 0) and (y % 2 == 0))
        px[x, y] = BANDS[i + 1] + (255,) if (frac > 0.75 or (frac > 0.25 and up)) else BANDS[i] + (255,)

# 성운. 같은 색을 여러 번 옅게 얹지 않고, 덩어리 안쪽만 한 단 밝은 색으로 칠한다.
NEBULA = [((190, 90, 200), (200, 120, 40), 0.34, 0.40),
          ((70, 130, 210), (455, 240, 60), 0.30, 0.34)]
for color, center, rx, ry in NEBULA:
    ncx, ncy = center[0], center[1]
    for y in range(H):
        for x in range(W):
            dx = (x - ncx) / (W * rx)
            dy = (y - ncy) / (H * ry)
            d = dx * dx + dy * dy
            if d < 1.0:
                # 안쪽일수록 촘촘한 디더로 색이 진해진다
                strength = 1.0 - d
                if random.random() < strength * 0.30:
                    r, g, b, _ = px[x, y]
                    px[x, y] = (min(255, (r + color[0]) // 2),
                                min(255, (g + color[1]) // 2),
                                min(255, (b + color[2]) // 2), 255)

# 별. 밝기 세 단으로만 찍고, 한 점이 아니라 2x2로 찍는다.
STAR = [(255, 255, 255), (198, 214, 255), (140, 158, 210)]
for _ in range(1200):
    x, y = random.randrange(W - 1), random.randrange(H - 1)
    c = STAR[random.choice([0, 1, 1, 2, 2, 2])] + (255,)
    for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        px[x + dx, y + dy] = c

# 큰 별 몇 개는 십자로. 반짝이는 티가 나야 우주로 읽힌다.
for _ in range(36):
    x, y = random.randrange(8, W - 8), random.randrange(8, H - 8)
    arm = random.choice([3, 3, 4])
    for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        px[x + dx, y + dy] = (255, 255, 255, 255)
    for k in range(2, arm + 2):
        c = (255, 255, 255, 255) if k == 2 else (190, 205, 255, 255)
        for dx, dy in ((k, 0), (-k + 1, 0), (0, k), (0, -k + 1)):
            px[x + dx, y + dy] = c
            px[x + dx + (1 if dy else 0), y + dy + (1 if dx else 0)] = c

sky.save('Assets/Art/화면/우주 배경.png')
print('우주 배경 %dx%d' % sky.size)

# ── 번개 ────────────────────────────────────────────────────────────────
CELL, COLS = 64, 6
bolt_sheet = Image.new('RGBA', (CELL * COLS, CELL), (0, 0, 0, 0))

EDGE = (58, 96, 200, 255)     # 바깥 테두리
GLOW = (120, 200, 255, 255)   # 중간
CORE = (255, 255, 255, 255)   # 심

def line_pixels(a, b):
    """브레젠험. 두 점 사이 픽셀을 잇는다."""
    x0, y0 = a; x1, y1 = b
    dx, dy = abs(x1 - x0), abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx - dy
    out = []
    while True:
        out.append((x0, y0))
        if x0 == x1 and y0 == y1: break
        e2 = 2 * err
        if e2 > -dy: err -= dy; x0 += sx
        if e2 < dx:  err += dx; y0 += sy
    return out

def grow(pixels, radius):
    """굵게. 반지름만큼 사방으로 부풀린다."""
    out = set()
    for (x, y) in pixels:
        for dy in range(-radius, radius + 1):
            for dx in range(-radius, radius + 1):
                if dx * dx + dy * dy <= radius * radius + 1:
                    out.add((x + dx, y + dy))
    return out

def zigzag(start, end, steps, spread):
    """시작에서 끝까지 좌우로 꺾어 가며 내려가는 길."""
    pts = [start]
    for i in range(1, steps):
        t = i / steps
        bx = start[0] + (end[0] - start[0]) * t
        by = start[1] + (end[1] - start[1]) * t
        # 진행 방향의 직각으로 흔든다
        ang = math.atan2(end[1] - start[1], end[0] - start[0]) + math.pi / 2
        off = random.uniform(-spread, spread)
        pts.append((int(round(bx + math.cos(ang) * off)), int(round(by + math.sin(ang) * off))))
    pts.append(end)
    return pts

# 칸마다 방향이 다른 번개. 회전은 못 쓰니 각도를 미리 구워 둔다.
PATHS = [
    ((32,  2), (32, 61), 6, 9),    # 수직
    (( 4,  4), (58, 60), 6, 8),    # 왼위 → 오른아래
    ((60,  4), ( 6, 60), 6, 8),    # 오른위 → 왼아래
    (( 2, 32), (61, 32), 6, 10),   # 수평
    ((14,  8), (50, 54), 4, 6),    # 짧고 굵게
    ((32,  2), (32, 61), 7, 11),   # 갈래가 갈라지는 것
]

for i, (a, b, steps, spread) in enumerate(PATHS):
    path = zigzag(a, b, steps, spread)
    core = []
    for j in range(len(path) - 1):
        core += line_pixels(path[j], path[j + 1])

    if i == 5:   # 가운데에서 한 갈래 튀어나오게
        mid = path[len(path) // 2]
        branch = zigzag(mid, (58, 46), 3, 7)
        for j in range(len(branch) - 1):
            core += line_pixels(branch[j], branch[j + 1])

    layers = [(grow(core, 2), EDGE), (grow(core, 1), GLOW), (set(core), CORE)]
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    cp = cell.load()
    for pixels, color in layers:
        for (x, y) in pixels:
            if 0 <= x < CELL and 0 <= y < CELL:
                cp[x, y] = color

    bolt_sheet.paste(cell, (i * CELL, 0))

bolt_sheet.save('Assets/Art/나머지/번개.png')
print('번개 %dx%d (64칸 %d개)' % (bolt_sheet.size + (COLS,)))
