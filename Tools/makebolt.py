# -*- coding: utf-8 -*-
"""번개 6칸. 64x64 칸, LoadSpriteSheet(path, 64, 64) 로 잘린다."""
from PIL import Image
import random, math

random.seed(77)

CELL, COLS = 64, 6
sheet = Image.new('RGBA', (CELL * COLS, CELL), (0, 0, 0, 0))

EDGE = (46, 84, 190, 255)     # 바깥
GLOW = (126, 206, 255, 255)   # 중간
CORE = (255, 255, 255, 255)   # 심

def line_pixels(a, b):
    x0, y0 = a; x1, y1 = b
    dx, dy = abs(x1 - x0), abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err, out = dx - dy, []
    while True:
        out.append((x0, y0))
        if x0 == x1 and y0 == y1: break
        e2 = 2 * err
        if e2 > -dy: err -= dy; x0 += sx
        if e2 < dx:  err += dx; y0 += sy
    return out

def zigzag(start, end, steps, spread):
    """꺾이는 각이 커야 번개로 보인다. 마디를 적게, 흔들림을 크게 준다."""
    pts = [start]
    ang = math.atan2(end[1] - start[1], end[0] - start[0]) + math.pi / 2
    for i in range(1, steps):
        t = i / steps
        bx = start[0] + (end[0] - start[0]) * t
        by = start[1] + (end[1] - start[1]) * t
        # 좌우로 번갈아 꺾는다. 무작위로만 두면 한쪽으로 쏠려 그냥 휜 선이 된다.
        off = spread * (1 if i % 2 else -1) * random.uniform(0.6, 1.0)
        pts.append((int(round(bx + math.cos(ang) * off)), int(round(by + math.sin(ang) * off))))
    pts.append(end)
    return pts

def stroke(path, extra):
    """길을 따라가며 굵기를 줄인다. 뿌리는 굵고 끝은 가늘어야 번개다."""
    walk = []
    for j in range(len(path) - 1):
        walk += line_pixels(path[j], path[j + 1])

    out = set()
    n = max(1, len(walk) - 1)
    for i, (x, y) in enumerate(walk):
        t = i / n
        r = int(round((1.0 - t * 0.75) * 2.0)) + extra   # 뿌리 굵게 → 끝 가늘게
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r + 1:
                    out.add((x + dx, y + dy))
    return out

PATHS = [
    ((32,  1), (30, 62), 4, 15),
    (( 3,  3), (59, 61), 4, 14),
    ((61,  3), ( 5, 61), 4, 14),
    (( 1, 30), (62, 34), 4, 16),
    ((12,  6), (52, 58), 3, 13),
    ((32,  1), (26, 62), 5, 16),
]

for i, (a, b, steps, spread) in enumerate(PATHS):
    path = zigzag(a, b, steps, spread)
    paths = [path]

    if i == 5:   # 가운데에서 한 갈래 갈라진다
        mid = path[len(path) // 2]
        paths.append(zigzag(mid, (60, 44), 3, 11))

    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    cp = cell.load()
    for extra, color in ((2, EDGE), (1, GLOW), (0, CORE)):
        for p in paths:
            for (x, y) in stroke(p, extra):
                if 0 <= x < CELL and 0 <= y < CELL:
                    cp[x, y] = color

    sheet.paste(cell, (i * CELL, 0))

sheet.save('Assets/Art/나머지/번개.png')
print('번개 %dx%d' % sheet.size)
