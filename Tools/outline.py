# -*- coding: utf-8 -*-
"""에셋마다 제각각인 검은 외곽선 두께를 그릇(1픽셀)에 맞춘다.

측정: 실루엣 바깥부터 껍질을 한 겹씩 벗기며, 그 겹이 대부분 어두우면 외곽선으로 센다.
수정: 외곽선이 두꺼우면 바깥 겹을 지워 1픽셀만 남긴다. 실루엣이 그만큼 작아진다.
"""
from PIL import Image
import os, sys

DARK = 90        # 이보다 어두우면 외곽선으로 본다
DARK_RATIO = 0.85  # 겹의 이만큼이 어두우면 그 겹 전체를 외곽선으로 본다

def cells(im, cell):
    if not cell:
        return [(0, 0, im.width, im.height)]
    out = []
    for y in range(0, im.height, cell):
        for x in range(0, im.width, cell):
            out.append((x, y, x + cell, y + cell))
    return out

def lum(px):
    return px[0]*0.299 + px[1]*0.587 + px[2]*0.114

def rings(p, box, mask):
    """지금 남은 실루엣의 바깥 한 겹."""
    x0, y0, x1, y1 = box
    out = []
    for y in range(y0, y1):
        for x in range(x0, x1):
            if not mask[(x, y)]: continue
            if any((x+dx, y+dy) not in mask or not mask[(x+dx, y+dy)]
                   for dx, dy in ((1,0),(-1,0),(0,1),(0,-1))):
                out.append((x, y))
    return out

def depth_of(im, box):
    """이 칸의 외곽선이 몇 겹인지."""
    p = im.load()
    x0, y0, x1, y1 = box
    mask = {(x, y): p[x, y][3] > 128 for y in range(y0, y1) for x in range(x0, x1)}
    d = 0
    for _ in range(6):
        ring = rings(p, box, mask)
        if not ring: break
        dark = sum(1 for x, y in ring if lum(p[x, y]) < DARK)
        if dark / len(ring) < DARK_RATIO: break
        d += 1
        for xy in ring: mask[xy] = False
    return d

def thin(im, box, remove):
    """바깥 remove 겹을 지운다."""
    p = im.load()
    x0, y0, x1, y1 = box
    mask = {(x, y): p[x, y][3] > 128 for y in range(y0, y1) for x in range(x0, x1)}
    for _ in range(remove):
        ring = rings(p, box, mask)
        for xy in ring:
            mask[xy] = False
            p[xy[0], xy[1]] = (0, 0, 0, 0)

def measure(path, cell=None):
    im = Image.open(path).convert('RGBA')
    ds = [depth_of(im, b) for b in cells(im, cell)]
    ds = [d for d in ds if d > 0]
    if not ds: return None
    from collections import Counter
    return Counter(ds).most_common(1)[0][0], min(ds), max(ds), len(ds)

if __name__ == '__main__':
    TARGETS = [
        ('Assets/Art/그릇/Sio_Ani.png', 128),
        ('Assets/Art/조리/타래_시오.png', None),
        ('Assets/Art/조리/타래_쇼유.png', None),
        ('Assets/Art/조리/타래_돈코츠.png', None),
        ('Assets/Art/조리/향미유통.png', None),
        ('Assets/Art/조리/육수 냄비.png', 128),
        ('Assets/Art/조리/면통.png', 128),
        ('Assets/Art/조리/면통_왼쪽만.png', 128),
        ('Assets/Art/조리/면통_오른쪽만.png', 128),
        ('Assets/Art/조리/국자_시오향미유.png', 128),
        ('Assets/Art/조리/국자_쇼유.png', 128),
        ('Assets/Art/조리/국자_돈코츠.png', 128),
        ('Assets/Art/조리/면털기_얇은면.png', 128),
        ('Assets/Art/조리/면털기_굵은면.png', 128),
        ('Assets/Art/조리/면붓기_얇은면.png', 128),
        ('Assets/Art/조리/면붓기_굵은면.png', 128),
        ('Assets/Art/재료/차슈 재료통.png', None),
        ('Assets/Art/나머지/시치미.png', None),
    ]
    print('%-32s %s' % ('에셋', '외곽선 겹 (최빈 / 최소~최대 / 칸수)'))
    for path, cell in TARGETS:
        if not os.path.exists(path):
            print('%-32s (없음)' % os.path.basename(path)); continue
        r = measure(path, cell)
        if r is None: print('%-32s (실루엣 없음)' % os.path.basename(path)); continue
        mode, lo, hi, n = r
        mark = '  ← 두꺼움' if mode >= 2 else ''
        print('%-32s %d겹  (%d~%d, %d칸)%s' % (os.path.basename(path), mode, lo, hi, n, mark))
