# -*- coding: utf-8 -*-
"""레시피북 한 장을 굽는다 — 재료 아이콘 26칸을 한 화풍으로 다시 그린다.

받은 그림(`Tools/art_source/레시피북_원본.png`, 900x1470)에서 **종이·제목·제목틀·밑줄·연필
낙서는 그대로 두고, 재료 아이콘 26칸만** 갈아 끼운다. 원본은 지우지 않으므로 언제든 돌아갈 수 있다.

## 왜 다시 그렸나

2026-09-16 에 세 칸만 손보려던 것이 전부로 번졌다. 순서대로 걸린 것들이다.

  * **김이 아예 없었다.** `RecipeGenerator.GetBaseRecipe` 의 돈코츠는 10칸인데 그림은 9칸.
  * **향미유가 육수와 안 갈렸다.** 둘 다 스테인리스 통에 주황 액체. 크기만 다른 같은 물건이었다.
  * **목이버섯이 숙주와 안 갈렸다.** 색만 다른 같은 모양의 더미 둘이 나란히 있었다.
  * 나머지도 비슷한 이유로 사용자가 통일을 요청했다.

## 화풍 규칙 — 열넷이 한 벌로 보이게 하는 것

받은 그림에서 재서 맞췄다. 새 아이콘을 보탤 때도 이대로 한다.

  * **먹테 3칸.** 순수 검정. 곧은 변에서 2칸, 굽은 데서 4~5칸이던 것을 3으로 잡았다.
  * **빛은 왼쪽 위에서.** 대각선으로 가른 셀 셰이딩이다. 가로 띠로 가르면 깃발에 줄을 그은 꼴이 된다.
  * **반짝임 하나.** 왼쪽 위에 짧은 빗금. **어두운 재료는 이것이 없으면 안 읽힌다** —
    먹테는 짙은 바탕에 묻히므로 테를 굵혀 봐야 소용없다(김·목이에서 실제로 겪었다).
  * **4배로 그렸다가 줄인다**(`SS`). 받은 그림도 가장자리가 부드럽다.

## 모양이 겹치면 색으로는 안 갈린다

이 장에서 제일 크게 데인 곳이다. 색만 다르고 모양이 같으면 둘 다 안 읽힌다.

  * 얇은면 / 굵은면 — **가락 수와 짜임을 바꾼다.** 굵기만 바꾸면 여전히 헷갈린다.
    얇은면은 가는 가락을 촘촘히 감은 타래, 굵은면은 굵은 가락 넷을 성기게 늘어놓은 모양이다.
  * 숙주 / 얇은면 — 둘 다 가닥 더미라 숙주에는 **노란 콩머리**를 크게 달았다.
  * 목이 / 숙주 — 목이는 주름진 귀로 바꿨다. 테두리 주름만으로는 감자가 되고,
    **속을 파야** 버섯으로 읽힌다.
  * 타래 셋(시오·쇼유·돈코츠)만은 일부러 같은 통에 색만 다르다. 한 묶음으로 읽혀야 한다.

## 자리

`CELLS` 가 전부다. 네모는 **원본에서 잰 값**이고, 재료 순서는 `GetBaseRecipe` 를 그대로 따른다.
지울 네모를 눈으로 재면 틀린다 — 떨어져 나온 조각이나 연필 낙서가 붙어 있다.

**지우기는 가로 복사다.** 종이 줄무늬가 가로라서 같은 y 띠에서 빈 종이를 떠다 덮으면 줄이
저절로 이어진다. 세로로 옮기면 어긋나 바로 티가 난다. 빈 자리가 좁으면 옆으로 이어 붙인다.
빈 자리는 `_clean_run` 이 찾는다 — 손으로 적어 두면 그림이 바뀔 때마다 틀린다.

출력 파일 **이름이 원본과 같으므로 빌더를 다시 돌릴 필요가 없다.** 유니티가 텍스처만 다시 읽는다.
원본을 `_ArtSource/` 가 아니라 `Tools/art_source/` 에 둔 것은 그쪽이 `.gitignore` 에 걸려서다 —
받은 그림이 깃에 없으면 다른 사람은 이 스크립트를 못 돌린다. Assets 밖이라 유니티도 안 건드린다.
"""
import math
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw

SRC = 'Tools/art_source/레시피북_원본.png'
OUT = 'Assets/Art/UI/레시피북.png'
RECIPE_CS = 'Assets/Scripts/Customer/RecipeGenerator.cs'

# 종이에서 그림이 올라갈 수 있는 x 범위. 왼쪽 빨간 여백선과 수첩 테두리 안쪽이다.
PAPER_X = (125, 872)

# 칸 — (지울 네모, 그릴 재료). 네모는 원본 그림의 테두리를 잰 값이다.
# 순서는 RecipeGenerator.GetBaseRecipe 그대로다. 멘마 2 · 차슈 2 처럼 수량이 둘이면 두 칸이다.
CELLS = (
    # 시오 — 시오타래 육수 얇은면 차슈 / 멘마 멘마 파 향미유
    ((140, 330, 236, 427), 'shio_tare'),
    ((287, 317, 414, 433), 'broth'),
    ((460, 317, 577, 427), 'thin_noodles'),
    ((632, 328, 759, 429), 'chashu'),
    ((137, 445, 241, 537), 'menma'),
    ((300, 445, 405, 537), 'menma'),
    ((463, 442, 573, 537), 'green_onion'),
    ((644, 448, 745, 540), 'oil'),
    # 쇼유 — 쇼유타래 육수 얇은면 차슈 / 차슈 멘마 파 향미유
    ((140, 689, 236, 785), 'shoyu_tare'),
    ((287, 676, 414, 790), 'broth'),
    ((460, 676, 577, 786), 'thin_noodles'),
    ((632, 689, 759, 790), 'chashu'),
    ((128, 802, 250, 898), 'chashu'),
    ((299, 805, 404, 898), 'menma'),
    ((463, 803, 573, 898), 'green_onion'),
    ((644, 807, 745, 902), 'oil'),
    # 돈코츠 — 돈코츠베이스 육수 굵은면 차슈 / 계란 김 숙주 목이 / 파 향미유
    ((140, 1047, 236, 1146), 'tonkotsu_base'),
    ((284, 1035, 409, 1148), 'broth'),
    ((456, 1029, 577, 1144), 'thick_noodles'),
    ((632, 1044, 759, 1147), 'chashu'),
    ((128, 1159, 242, 1259), 'egg'),
    ((284, 1162, 402, 1255), 'nori'),
    ((455, 1161, 577, 1262), 'bean_sprout'),
    # 목이버섯은 왼쪽에 **떨어져 나온 조각**이 608~615 에 따로 있고(본체는 631부터),
    # 아래로 1261 에서 끝나고 **1264부터가 연필 낙서**다. 본체에만 맞추면 조각이 남는다.
    ((606, 1157, 756, 1262), 'ear'),
    ((133, 1279, 239, 1376), 'green_onion'),
    # 셋째 줄 둘째 칸은 원본에서 비어 있었다. 향미유를 여기로 내린다 — 마지막에 오는 것이 자연스럽다.
    ((287, 1279, 393, 1376), 'oil'),
)

MARGIN = 5                                 # 지울 때 그림 테두리 바깥으로 더 먹는 칸

SS = 4                                     # 4배로 그렸다가 줄인다
INK = (0, 0, 0, 255)
OUTLINE = 3 * SS                           # 먹테. 줄이면 3칸이 된다


# ============================================================================
# 그리는 틀
# ============================================================================
def _cel(img, pts, base, inside=None, outline=OUTLINE):
    """
    바깥선 하나를 받아 안쪽을 칠하고 먹테를 두른다.

    안쪽 칠(`inside`)은 바깥선 밖까지 넉넉히 그려도 된다 — 바깥선 모양으로 오려 낸다.
    띠나 빗금을 굽은 가장자리에 일일이 맞추지 않으려고 이렇게 했다.
    """
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)

    paint = Image.new('RGBA', img.size, base)
    if inside is not None:
        inside(ImageDraw.Draw(paint))

    img.paste(paint, (0, 0), mask)
    ImageDraw.Draw(img).polygon(pts, outline=INK, width=outline)


def _oval(cx, cy, w, h, n=56, ang=0.0, wobble=None):
    """타원 바깥선. `wobble(각)` 을 주면 반지름을 흔들어 울퉁불퉁하게 만든다."""
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    pts = []
    for i in range(n):
        th = 2 * math.pi * i / n
        r = 1.0 if wobble is None else wobble(th)
        x, y = math.cos(th) * w / 2 * r, math.sin(th) * h / 2 * r
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return pts


def _shade(pd, cx, cy, w, h, light, dark):
    """대각선으로 가른 셀 셰이딩. 빛은 늘 왼쪽 위에서 온다."""
    pd.polygon([(cx - 2 * w, cy - 2 * h), (cx + 2 * w, cy - 2 * h),
                (cx - 2 * w, cy + 2 * h)], fill=light)
    pd.polygon([(cx + 2 * w, cy + h * 0.04), (cx + 2 * w, cy + 2 * h),
                (cx - w * 0.06, cy + 2 * h)], fill=dark)


def _spec(pd, cx, cy, w, h, color, width=2):
    """반짝임 한 줄. 왼쪽 위에 짧게."""
    pd.line([(cx - w * 0.30, cy - h * 0.24), (cx - w * 0.10, cy - h * 0.08)],
            fill=color, width=width * SS)


def _strand(pd, pts, color, width, shade=None, ink=4 * SS):
    """
    가락 하나 — 먹테를 깔고 그 위에 색을 얹는다. 겹쳐 그으면 저절로 앞뒤가 생긴다.

    `ink` 를 폭에 맞춰 줄이지 않으면 **테가 가락보다 굵어져 검은 덩어리가 된다.**
    얇은 가락에 4칸 테를 둘렀다가 실제로 그랬다.
    """
    pd.line(pts, fill=INK, width=width + ink, joint='curve')
    pd.line(pts, fill=color, width=width, joint='curve')
    if shade is not None and width >= 5 * SS:
        pd.line([(x, y + width * 0.26) for x, y in pts], fill=shade,
                width=max(SS, width // 4), joint='curve')


def _gloss(img, pts, light=(255, 255, 255, 44), dark=(0, 0, 0, 38)):
    """
    이미 결이 그려진 것 위에 **빛만 비스듬히 얹는다.**

    `_shade` 는 불투명하게 덮어 버리므로 차슈처럼 안에 층이 있는 그림에는 못 쓴다.
    비치는 쐐기 둘을 바깥선 모양으로 오려서 덧씌운다.
    """
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    w, h = max(xs) - min(xs), max(ys) - min(ys)

    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)

    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.polygon([(cx - 2 * w, cy - 2 * h), (cx + 2 * w, cy - 2 * h),
                (cx - 2 * w, cy + 2 * h)], fill=light)
    ld.polygon([(cx + 2 * w, cy + h * 0.04), (cx + 2 * w, cy + 2 * h),
                (cx - w * 0.06, cy + 2 * h)], fill=dark)
    layer.putalpha(ImageChops.multiply(layer.getchannel('A'), mask))
    img.alpha_composite(layer)


def _arc_poly(cx, cy, rw, rh, t_out, t_in, a0, a1, n=56):
    """고리 조각. 바깥 호를 a0→a1 로 돌고 안쪽 호를 되짚어 닫는다. t 는 반지름 비율."""
    out_pts, in_pts = [], []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        ca, sa = math.cos(a), math.sin(a)
        out_pts.append((cx + ca * rw * t_out, cy + sa * rh * t_out))
        in_pts.append((cx + ca * rw * t_in, cy + sa * rh * t_in))
    return out_pts + in_pts[::-1]


def _canvas(w, h):
    return Image.new('RGBA', (w * SS, h * SS), (0, 0, 0, 0))


# ============================================================================
# 재료 열넷
# ============================================================================
STEEL = (176, 183, 193, 255)
STEEL_L = (228, 232, 238, 255)
STEEL_D = (124, 132, 144, 255)

# --- 타래 통 셋 -------------------------------------------------------------
# 셋은 **일부러 같은 통에 색만 다르다.** 시오·쇼유·돈코츠가 한 묶음으로 읽혀야 한다.
# 다른 재료끼리는 반대로 모양을 벌려 놓는다 — 아래 면·숙주 주석 참고.
SHIO = (244, 206, 86, 255)
SHOYU = (96, 42, 28, 255)
TONKOTSU = (242, 230, 212, 255)


def _tin(w, h, liquid):
    """
    타래 통. 비스듬히 내려다본 납작한 통에 내용물이 고여 있다.

    위에서 본 면을 크게 두는 것이 요령이다. 앞면만 그리면 육수 냄비와 덩어리가 비슷해진다.
    """
    img = _canvas(w, h)
    W, H = img.size
    cx, cy = W * 0.5, H * 0.5

    body = [(cx - W * 0.42, cy - H * 0.10), (cx + W * 0.42, cy - H * 0.10),
            (cx + W * 0.34, cy + H * 0.34), (cx - W * 0.34, cy + H * 0.34)]
    _cel(img, body, STEEL,
         lambda pd: (_shade(pd, cx, cy + H * 0.12, W * 0.8, H * 0.44, STEEL_L, STEEL_D),
                     _spec(pd, cx, cy + H * 0.12, W * 0.8, H * 0.44, (255, 255, 255, 255))))

    top = [(cx - W * 0.42, cy - H * 0.10), (cx + W * 0.42, cy - H * 0.10),
           (cx + W * 0.36, cy - H * 0.36), (cx - W * 0.36, cy - H * 0.36)]
    _cel(img, top, STEEL, lambda pd: _shade(pd, cx, cy - H * 0.23, W * 0.8, H * 0.26,
                                            STEEL_L, STEEL_D))

    pool = [(cx - W * 0.34, cy - H * 0.14), (cx + W * 0.34, cy - H * 0.14),
            (cx + W * 0.29, cy - H * 0.32), (cx - W * 0.29, cy - H * 0.32)]
    _cel(img, pool, liquid,
         lambda pd: _spec(pd, cx - W * 0.06, cy - H * 0.23, W * 0.5, H * 0.18,
                          (255, 255, 255, 200)),
         outline=3 * SS)
    return img.resize((w, h), Image.LANCZOS)


def shio_tare(w, h):
    return _tin(w, h, SHIO)


def shoyu_tare(w, h):
    return _tin(w, h, SHOYU)


def tonkotsu_base(w, h):
    return _tin(w, h, TONKOTSU)


# --- 육수 -------------------------------------------------------------------
BROTH = (238, 148, 44, 255)
BROTH_L = (250, 194, 96, 255)
BROTH_D = (198, 100, 18, 255)


def broth(w, h):
    """
    육수 냄비. **손잡이 둘이 타래 통과 갈라 주는 것**이라 크게 붙인다.

    통과 냄비를 몸통 모양만으로 가르려 하면 작게 줄었을 때 둘 다 회색 덩어리가 된다.
    """
    img = _canvas(w, h)
    W, H = img.size
    cx, cy = W * 0.5, H * 0.54

    for sx in (-1, 1):                                        # 손잡이 — 몸통보다 먼저(뒤로 간다)
        hx = cx + sx * W * 0.36
        _cel(img, _oval(hx, cy - H * 0.06, W * 0.20, H * 0.13), STEEL_D, outline=2 * SS)

    body = [(cx - W * 0.34, cy - H * 0.18), (cx + W * 0.34, cy - H * 0.18),
            (cx + W * 0.27, cy + H * 0.34), (cx - W * 0.27, cy + H * 0.34)]
    _cel(img, body, STEEL,
         lambda pd: (_shade(pd, cx, cy + H * 0.08, W * 0.7, H * 0.52, STEEL_L, STEEL_D),
                     _spec(pd, cx, cy + H * 0.08, W * 0.7, H * 0.52, (255, 255, 255, 255))))

    _cel(img, _oval(cx, cy - H * 0.18, W * 0.68, H * 0.24), BROTH,
         lambda pd: (_shade(pd, cx, cy - H * 0.18, W * 0.68, H * 0.24, BROTH_L, BROTH_D),
                     _spec(pd, cx, cy - H * 0.18, W * 0.68, H * 0.24, (255, 240, 200, 255))))
    return img.resize((w, h), Image.LANCZOS)


# --- 면 둘 -----------------------------------------------------------------
# **굵기만 바꾸면 안 갈린다.** 가락 수와 짜임을 통째로 바꾼다.
#   얇은면 — 가는 가락 열둘을 촘촘히 감은 타래. 멀리서 보면 결이 곱다.
#   굵은면 — 굵은 가락 넷을 성기게 늘어놓은 더미. 멀리서 보면 덩어리가 성기다.
NOODLE = (248, 212, 104, 255)
NOODLE_D = (206, 152, 38, 255)
THICK = (240, 190, 70, 255)
THICK_D = (188, 132, 28, 255)


def thin_noodles(w, h):
    """
    얇은면 — 가는 가락 **열둘**을 일자로 깐다. 굵은면은 굵은 가락 **넷**이다.

    세 번 헛돌았다. 가락마다 먹테를 4칸씩 둘렀더니 테가 가락(8칸)보다 굵어 **검은 덩어리**가
    됐고, 한 덩이로 뭉쳤더니 **감자빵**이 됐고, 굽이를 주었더니 꾸불꾸불해 보였다.

    지금은 곧게 눕히고 **길이만 조금씩 어긋내** 바코드가 되지 않게 한다.
    굵은면과는 **성긴가 촘촘한가**로 갈린다 — 화면 39칸에서 굵기만으로는 절대 안 갈린다.
    """
    img = _canvas(w, h)
    W, H = img.size
    pd = ImageDraw.Draw(img)
    jag = (0.00, 0.05, 0.02, 0.07, 0.01, 0.06, 0.03, 0.08, 0.01, 0.05, 0.02, 0.06)
    for i in range(12):
        y = H * (0.12 + 0.76 * i / 11.0)
        pd_x0, pd_x1 = W * (0.02 + jag[i]), W * (0.98 - jag[(i + 5) % 12])
        _strand(pd, [(pd_x0, y), (pd_x1, y)], NOODLE, 4 * SS, ink=2 * SS)
    return img.resize((w, h), Image.LANCZOS)


def thick_noodles(w, h):
    img = _canvas(w, h)
    W, H = img.size
    pd = ImageDraw.Draw(img)
    for i, jag in enumerate((0.02, 0.08, 0.03, 0.07)):
        y = H * (0.20 + 0.60 * i / 3.0)
        _strand(pd, [(W * (0.02 + jag), y), (W * (0.98 - jag), y)],
                THICK, 12 * SS, THICK_D, ink=3 * SS)
    return img.resize((w, h), Image.LANCZOS)


# --- 차슈 -------------------------------------------------------------------
# **동그란 조각이 아니다.** 게임에서 쓰는 `Assets/Art/재료/차슈.png` 를 보고 고쳤다 —
# 통삼겹을 말아 썬 **C 자 단면**이고, 껍질·비계·살이 층으로 감겨 있으며 후추 점이 박혀 있다.
# 층 순서는 바깥부터 껍질 / 비계 / 살 / 비계 / 옅은 살 / 비계다.
# 색은 눈대중이 아니라 `Assets/Art/재료/차슈.png` 에서 **뽑은 값**이다.
# 처음엔 베이지로 그렸는데 실제는 훨씬 붉고 짙다.
CH_CRUST = (78, 27, 9, 255)        # 겉껍질
CH_FAT = (228, 188, 139, 255)      # 비계
CH_MEAT = (116, 42, 12, 255)       # 짙은 살
CH_MEAT_L = (170, 61, 20, 255)     # 붉은 살
CH_PEPPER = (40, 16, 6, 255)

# (바깥 반지름 비율, 안쪽 반지름 비율, 색) — 1.0 이 바깥, 0.34 가 C 자 안쪽 구멍이다.
CH_BANDS = ((1.000, 0.930, CH_CRUST), (0.930, 0.815, CH_FAT), (0.815, 0.665, CH_MEAT),
            (0.665, 0.600, CH_FAT), (0.600, 0.430, CH_MEAT_L), (0.430, 0.340, CH_FAT))

# 후추 — (각도, 반지름 비율). 비계 층에만 박는다. 무작위로 뿌리면 돌려도 달라진다.
CH_PEPPER_DOTS = ((168, 0.89), (196, 0.51), (214, 0.88), (232, 0.47), (248, 0.90),
                  (266, 0.53), (284, 0.87), (302, 0.49), (320, 0.89), (344, 0.52),
                  (362, 0.88), (182, 0.66), (300, 0.65))

CH_A0, CH_A1 = 150, 390            # 왼쪽 아래에서 위를 넘어 오른쪽 아래까지. 입은 아래로 벌어진다


def chashu(w, h):
    """차슈 한 점. C 자 단면에 층이 감겨 있다."""
    img = _canvas(w, h)
    W, H = img.size
    cx, cy = W * 0.5, H * 0.56
    rw, rh = W * 0.92, H * 0.96

    def inside(pd):
        for t_out, t_in, color in CH_BANDS[1:]:                 # 바깥 껍질은 _cel 의 바탕색
            pd.polygon(_arc_poly(cx, cy, rw / 2, rh / 2, t_out, t_in, CH_A0, CH_A1),
                       fill=color)
        for deg, t in CH_PEPPER_DOTS:
            a = math.radians(deg)
            px_, py_ = cx + math.cos(a) * rw / 2 * t, cy + math.sin(a) * rh / 2 * t
            pd.ellipse([px_ - 1.4 * SS, py_ - 1.4 * SS, px_ + 1.4 * SS, py_ + 1.4 * SS],
                       fill=CH_PEPPER)

    pts = _arc_poly(cx, cy, rw / 2, rh / 2, 1.0, 0.34, CH_A0, CH_A1)
    _cel(img, pts, CH_CRUST, inside)
    # 층을 불투명하게 덮으면 안 되므로 빛만 비쳐 얹는다.
    _gloss(img, pts, light=(255, 255, 255, 26), dark=(0, 0, 0, 32))
    return img.resize((w, h), Image.LANCZOS)


# --- 멘마 -------------------------------------------------------------------
# 게임에서 쓰는 `Assets/Art/재료/멘마.png` 를 보고 고쳤다 — 납작한 띠가 아니라
# **두께가 보이는 각재**다. 윗면·옆면·끝면 세 면을 다 칠해야 토막 난 막대로 읽힌다.
# 색도 베이지가 아니라 꽤 진한 주황빛이다.
# 색은 `Assets/Art/재료/멘마.png` 에서 뽑았다. 윗면에 **흰 빗금**이 그어져 있는 것이 특징이다.
MENMA_TOP = (244, 159, 49, 255)
MENMA_TOP_L = (252, 196, 112, 255)
MENMA_WHITE = (253, 253, 252, 255)
MENMA_SIDE = (182, 88, 14, 255)   # 더 어두우면 막대 밑에 검은 판이 깔린 꼴이 된다
MENMA_END = (201, 97, 9, 255)


def _menma_bar(img, cx, cy, L, T, thick, ang):
    """각재 하나. 윗면을 그리고 아래로 `thick` 만큼 민 것이 옆면·끝면이다."""
    a = math.radians(ang)
    d = (math.cos(a), math.sin(a))
    p = (-d[1], d[0])

    def c(u, v):
        return (cx + d[0] * u * L / 2 + p[0] * v * T / 2,
                cy + d[1] * u * L / 2 + p[1] * v * T / 2)

    def down(pt):
        return (pt[0], pt[1] + thick)

    top = [c(-1, -1), c(1, -1), c(1, 1), c(-1, 1)]
    # 옆면·끝면을 먼저 깔고 윗면을 덮는다. 순서가 반대면 윗면 테가 먹힌다.
    _cel(img, [top[3], top[2], down(top[2]), down(top[3])], MENMA_SIDE, outline=2 * SS)
    _cel(img, [top[1], top[2], down(top[2]), down(top[1])], MENMA_END, outline=2 * SS)
    _cel(img, top, MENMA_TOP,
         lambda pd: (pd.line([c(-0.78, 0.30), c(0.78, 0.30)], fill=MENMA_END,
                             width=max(SS, int(T * 0.20))),
                     pd.line([c(-0.70, -0.30), c(0.70, -0.30)], fill=MENMA_TOP_L,
                             width=max(SS, int(T * 0.26))),
                     pd.line([c(-0.52, -0.34), c(0.10, -0.34)], fill=MENMA_WHITE,
                             width=max(SS, int(T * 0.14)))),
         outline=2 * SS)


def menma(w, h):
    """
    죽순 각재 다섯을 엇갈려 쌓았다.

    **길이를 화면 폭보다 짧게 끊고 기울기를 벌리는 것**이 요령이다. 처음엔 다섯을 다
    가로로 길게 눕혔더니 판자 더미가 됐고, 바로 옆 굵은면(굵은 가락 넷)과 덩어리가 똑같았다.
    """
    img = _canvas(w, h)
    W, H = img.size
    thick = H * 0.055
    for cx, cy, L, ang in ((W * 0.40, H * 0.22, 0.56, -22),
                           (W * 0.62, H * 0.32, 0.50, 20),
                           (W * 0.38, H * 0.48, 0.54, 12),
                           (W * 0.60, H * 0.62, 0.48, -18),
                           (W * 0.46, H * 0.79, 0.58, 5)):
        _menma_bar(img, cx, cy, W * L, H * 0.17, thick, ang)
    return img.resize((w, h), Image.LANCZOS)


# --- 파 ---------------------------------------------------------------------
# 게임에서 쓰는 `Assets/Art/재료/파.png` 를 보고 고쳤다. 전부 같은 크기의 같은 고리가 아니다 —
#   * **구멍이 뚫린 고리**와 **속이 찬 토막**이 섞여 있다(고리는 대궁, 토막은 푸른 잎이다)
#   * 밝은 연두와 짙은 초록 두 가지가 섞여 있다
#   * 크기가 제각각이고 더미가 가운데로 모여 있다
# 구멍은 실제로 **뚫는다.** 밝은 색으로 채우면 도넛 그림이 되고 종이 줄이 안 비친다.
# 색은 `Assets/Art/재료/파.png` 에서 뽑았다. **파랑이 0 인 연두**다 —
# 처음에 b 를 50~80 으로 줬더니 민트색이 되어 파로 안 보였다.
ON_A = (160, 212, 0, 255)          # 밝은 연두 — 대궁
ON_A_L = (214, 242, 70, 255)
ON_A_D = (95, 168, 1, 255)
ON_B = (74, 152, 0, 255)           # 짙은 초록 — 잎
ON_B_L = (120, 186, 2, 255)
ON_B_D = (28, 116, 1, 255)
ON_SPEC = (247, 251, 114, 255)

# (가로, 세로 비율, 지름 비율, 구멍 비율 — 0 이면 속이 찬 토막, 밝은 쪽인가)
ON_BITS = ((0.30, 0.22, 0.32, 0.54, True), (0.58, 0.18, 0.22, 0.00, False),
           (0.78, 0.26, 0.29, 0.52, True), (0.17, 0.42, 0.24, 0.00, False),
           (0.44, 0.40, 0.33, 0.56, True), (0.70, 0.46, 0.26, 0.50, True),
           (0.88, 0.50, 0.25, 0.52, False), (0.26, 0.62, 0.30, 0.54, False),
           (0.55, 0.64, 0.22, 0.00, True), (0.80, 0.74, 0.31, 0.54, True),
           (0.36, 0.82, 0.26, 0.52, False), (0.60, 0.86, 0.20, 0.00, False),
           (0.12, 0.78, 0.18, 0.00, True))


def _onion_bit(img, cx, cy, rw, rh, hole, base, light, dark):
    """고리 한 알. `hole` 이 0 보다 크면 가운데를 실제로 뚫는다."""
    mask = Image.new('L', img.size, 0)
    md = ImageDraw.Draw(mask)
    md.ellipse([cx - rw / 2, cy - rh / 2, cx + rw / 2, cy + rh / 2], fill=255)
    if hole > 0:
        md.ellipse([cx - rw * hole / 2, cy - rh * hole / 2,
                    cx + rw * hole / 2, cy + rh * hole / 2], fill=0)

    paint = Image.new('RGBA', img.size, base)
    pd = ImageDraw.Draw(paint)
    _shade(pd, cx, cy, rw, rh, light, dark)
    _spec(pd, cx, cy, rw, rh, ON_SPEC, width=1)

    img.paste(paint, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - rw / 2, cy - rh / 2, cx + rw / 2, cy + rh / 2], outline=INK, width=2 * SS)
    if hole > 0:
        d.ellipse([cx - rw * hole / 2, cy - rh * hole / 2,
                   cx + rw * hole / 2, cy + rh * hole / 2], outline=INK, width=2 * SS)


def green_onion(w, h):
    """송송 썬 파. 고리와 토막이 섞인 더미."""
    img = _canvas(w, h)
    W, H = img.size
    for u, v, size, hole, bright in ON_BITS:
        base, light, dark = (ON_A, ON_A_L, ON_A_D) if bright else (ON_B, ON_B_L, ON_B_D)
        _onion_bit(img, W * u, H * v, W * size, H * size, hole, base, light, dark)
    return img.resize((w, h), Image.LANCZOS)


# --- 계란 -------------------------------------------------------------------
WHITE = (252, 248, 238, 255)
WHITE_D = (222, 212, 194, 255)
YOLK = (248, 166, 40, 255)
YOLK_L = (252, 210, 98, 255)
YOLK_D = (208, 110, 16, 255)


def egg(w, h):
    """반숙 계란 반쪽 둘. 노른자를 크게 잡아야 작게 줄어도 계란으로 읽힌다."""
    img = _canvas(w, h)
    W, H = img.size
    for cx, cy, ang in ((W * 0.33, H * 0.46, -8), (W * 0.68, H * 0.54, 7)):
        rw, rh = W * 0.46, H * 0.80
        _cel(img, _oval(cx, cy, rw, rh, ang=ang), WHITE,
             lambda pd, cx=cx, cy=cy, rw=rw, rh=rh: _shade(pd, cx, cy, rw, rh,
                                                           (255, 255, 250, 255), WHITE_D))
        _cel(img, _oval(cx, cy, rw * 0.58, rh * 0.52, ang=ang), YOLK,
             lambda pd, cx=cx, cy=cy, rw=rw, rh=rh: (
                 _shade(pd, cx, cy, rw, rh, YOLK_L, YOLK_D),
                 _spec(pd, cx, cy, rw * 0.5, rh * 0.4, (255, 244, 208, 255))),
             outline=2 * SS)
    return img.resize((w, h), Image.LANCZOS)


# --- 김 ---------------------------------------------------------------------
# 김은 거의 검정이라 먹테를 둘러도 테가 바탕에 묻힌다. **명암 폭을 18~200 으로 넓게** 잡고
# 반짝임으로 읽힌다. 테 굵기를 올려 봐야 소용없다.
NORI_DARK = (16, 32, 26, 255)
NORI_BASE = (34, 62, 46, 255)
NORI_LIGHT = (62, 102, 72, 255)
NORI_SHEEN = (158, 200, 150, 255)
NORI_EDGE = (108, 150, 104, 255)


def _sheet_poly(cx, cy, w, h, ang, wave=0.05, n=10):
    """김 한 장의 바깥선. 곧은 네모로 두면 종이 조각으로 보여 긴 변에 굽이를 하나 준다."""
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    local = []
    for i in range(n + 1):
        t = i / n
        local.append((-w / 2 + w * t, -h / 2 + math.sin(t * math.pi) * h * wave))
    for i in range(n + 1):
        t = i / n
        local.append((w / 2 - w * t, h / 2 + math.sin((1 - t) * math.pi) * h * wave))
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in local]


def _draw_sheet(img, cx, cy, w, h, ang):
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)

    def loc(u, v):
        x, y = u * w, v * h
        return (cx + x * ca - y * sa, cy + x * sa + y * ca)

    def inside(pd):
        pd.polygon([loc(-1, -1), loc(1, -1), loc(-1, 1)], fill=NORI_LIGHT)
        pd.polygon([loc(1, 0.12), loc(1, 1), loc(0.12, 1)], fill=NORI_DARK)
        pd.polygon([loc(-1, -1), loc(1, -1), loc(1, -0.44), loc(-1, -0.44)], fill=NORI_EDGE)
        for u in (-0.26, 0.0, 0.26):                          # 결은 세로로 간다
            pd.line([loc(u, -1), loc(u, 1)], fill=NORI_DARK, width=SS)
        pd.line([loc(-0.33, -0.33), loc(-0.12, 0.04)], fill=NORI_SHEEN, width=3 * SS)
        pd.line([loc(0.04, -0.28), loc(0.14, -0.14)], fill=NORI_SHEEN, width=2 * SS)

    _cel(img, _sheet_poly(cx, cy, w, h, ang), NORI_BASE, inside)


def nori(w, h):
    """김 두 장이 비스듬히 겹친 그림."""
    img = _canvas(w, h)
    W, H = img.size
    sw, sh = W * 0.56, H * 0.78
    _draw_sheet(img, W * 0.36, H * 0.40, sw, sh, -11)
    _draw_sheet(img, W * 0.62, H * 0.60, sw, sh, 8)
    return img.resize((w, h), Image.LANCZOS)


# --- 숙주 -------------------------------------------------------------------
# 얇은면과 둘 다 '가닥 더미' 라 색만으로는 안 갈린다. **노란 콩머리를 크게** 달아 가른다.
SPROUT = (250, 246, 232, 255)
SPROUT_D = (206, 198, 174, 255)
BEAN = (246, 214, 104, 255)
BEAN_D = (200, 158, 48, 255)


def bean_sprout(w, h):
    img = _canvas(w, h)
    W, H = img.size
    pd = ImageDraw.Draw(img)
    # 콩머리는 왼쪽에 모으되 **x 를 흩어야** 한다. 한 줄로 세웠더니 사다리로 보였다.
    # 꼬리 끝도 높이를 벌려 놔야 가닥이 나란히 붙지 않는다.
    heads = ((0.16, 0.20, 0.90, 0.40), (0.30, 0.44, 0.94, 0.30),
             (0.14, 0.66, 0.88, 0.80), (0.34, 0.88, 0.92, 0.62))
    for hu, hv, tu, tv in heads:
        hx, hy, tx, ty = W * hu, H * hv, W * tu, H * tv
        mid = ((hx + tx) / 2, (hy + ty) / 2 + H * 0.12)
        tail = [((1 - t) ** 2 * hx + 2 * (1 - t) * t * mid[0] + t * t * tx,
                 (1 - t) ** 2 * hy + 2 * (1 - t) * t * mid[1] + t * t * ty)
                for t in (i / 12 for i in range(13))]
        _strand(pd, tail, SPROUT, 8 * SS, SPROUT_D, ink=3 * SS)
    for hu, hv, _, _ in heads:
        cx, cy = W * hu, H * hv
        _cel(img, _oval(cx, cy, W * 0.26, H * 0.24), BEAN,
             lambda pd, cx=cx, cy=cy: (
                 _shade(pd, cx, cy, W * 0.26, H * 0.24, (252, 234, 160, 255), BEAN_D),
                 _spec(pd, cx, cy, W * 0.26, H * 0.24, (255, 252, 226, 255), width=1)),
             outline=2 * SS)
    return img.resize((w, h), Image.LANCZOS)


# --- 목이버섯 ---------------------------------------------------------------
# 받은 그림은 채 썬 더미였는데 옆 칸 숙주와 같은 모양이었다. 주름진 귀로 바꿔 모양부터 가른다.
EAR_DARK = (66, 28, 18, 255)
EAR_BASE = (116, 52, 30, 255)
EAR_LIGHT = (162, 84, 46, 255)
EAR_SHEEN = (206, 130, 78, 255)


def _frill_poly(cx, cy, w, h, ang, phase, n=96):
    """
    주름진 귀 한 조각의 바깥선.

    두 번 헛돌았다. 윗변에만 굽이를 줬더니 **삼각 고깔**이 나왔고(윗변과 아랫변이 양끝에서
    뾰족하게 만난다), 테두리 전체에 굽이를 주되 주기를 하나(0.18 x sin 5θ)만 썼더니
    **불가사리**가 나왔다. 규칙적인 굽이는 별이 된다.

    주기 셋을 진폭을 줄여 겹친다. 서로 안 떨어지는 배수라 굽이가 불규칙해지고,
    위상(`phase`)만 달리하면 세 조각이 저마다 다른 모양이 된다.
    """
    def wobble(th):
        return (1 + 0.125 * math.sin(3 * th + phase)
                  + 0.080 * math.sin(5 * th + phase * 1.7)
                  + 0.045 * math.sin(7 * th + phase * 0.6))

    return _oval(cx, cy, w, h, n=n, ang=ang, wobble=wobble)


def _draw_frill(img, cx, cy, w, h, ang, phase):
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)

    def loc(u, v):
        x, y = u * w, v * h
        return (cx + x * ca - y * sa, cy + x * sa + y * ca)

    def inside(pd):
        _shade(pd, cx, cy, w, h, EAR_LIGHT, EAR_DARK)
        # **속을 판다.** 주름만으로는 감자로 보였다. 남는 바깥 테가 귀의 전이 되고,
        # 그 전이 형태를 설명한다. 속에도 먹테를 둘러야 전과 속이 갈린다.
        hollow = _frill_poly(cx + h * 0.09 * sa, cy - h * 0.09 * ca,
                             w * 0.62, h * 0.58, ang, phase * 1.3)
        pd.polygon(hollow, fill=EAR_DARK, outline=INK, width=2 * SS)
        pd.line([loc(-0.44, -0.20), loc(-0.30, -0.36)], fill=EAR_SHEEN, width=2 * SS)

    _cel(img, _frill_poly(cx, cy, w, h, ang, phase), EAR_BASE, inside)


def ear(w, h):
    """목이버섯 세 조각. 위상과 기울기를 달리해 셋이 같은 그림으로 안 보이게 한다."""
    img = _canvas(w, h)
    W, H = img.size
    _draw_frill(img, W * 0.30, H * 0.37, W * 0.50, H * 0.54, -14, 0.4)
    _draw_frill(img, W * 0.72, H * 0.35, W * 0.48, H * 0.50, 17, 2.4)
    _draw_frill(img, W * 0.50, H * 0.69, W * 0.58, H * 0.56, -4, 4.1)
    return img.resize((w, h), Image.LANCZOS)


# --- 향미유 -----------------------------------------------------------------
# 통에 담긴 액체로 두면 육수 냄비와 안 갈린다. 방울은 열넷 중 하나뿐인 모양이다.
OIL_DARK = (186, 92, 12, 255)
OIL_BASE = (240, 156, 28, 255)
OIL_LIGHT = (250, 200, 80, 255)
OIL_SHEEN = (255, 246, 198, 255)


def _drop_poly(cx, cy, w, h, n=14):
    """방울 하나. 위는 뾰족하고 아래는 둥글다."""
    r = w / 2.0
    bc = cy + h / 2.0 - r
    tip, left, right = (cx, cy - h / 2.0), (cx - r, bc), (cx + r, bc)

    def bez(p0, p1, p2):
        return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                 (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
                for t in (i / n for i in range(n + 1))]

    pts = bez(tip, (cx - r * 0.88, cy - h * 0.04), left)
    # 아래 반원. 화면은 y 가 아래로 커지므로 각이 pi 에서 0 으로 내려가야 아래를 지난다.
    pts += [(cx + r * math.cos(a), bc + r * math.sin(a))
            for a in (math.pi * (1 - i / n) for i in range(1, n))]
    pts += bez(right, (cx + r * 0.88, cy - h * 0.04), tip)
    return pts


def _draw_drop(img, cx, cy, w, h):
    def inside(pd):
        _shade(pd, cx, cy, w, h, OIL_LIGHT, OIL_DARK)
        # 반짝임 — 방울은 이것이 있어야 물방울로 읽힌다.
        pd.ellipse([cx - w * 0.34, cy - h * 0.20, cx - w * 0.10, cy + h * 0.02],
                   fill=OIL_SHEEN)

    _cel(img, _drop_poly(cx, cy, w, h), OIL_BASE, inside)


def oil(w, h):
    """기름 방울 셋. 큰 것 하나에 작은 것 둘이 따라붙는다."""
    img = _canvas(w, h)
    W, H = img.size
    _draw_drop(img, W * 0.76, H * 0.30, W * 0.36, H * 0.50)
    _draw_drop(img, W * 0.78, H * 0.77, W * 0.30, H * 0.41)
    _draw_drop(img, W * 0.36, H * 0.50, W * 0.62, H * 0.92)
    return img.resize((w, h), Image.LANCZOS)


ICONS = {
    'shio_tare': shio_tare, 'shoyu_tare': shoyu_tare, 'tonkotsu_base': tonkotsu_base,
    'broth': broth, 'thin_noodles': thin_noodles, 'thick_noodles': thick_noodles,
    'chashu': chashu, 'menma': menma, 'green_onion': green_onion, 'egg': egg,
    'nori': nori, 'bean_sprout': bean_sprout, 'ear': ear, 'oil': oil,
}


# ============================================================================
# 지우기
# ============================================================================
def _is_paper(p):
    """종이(줄무늬 포함)면 참. 그림·글·연필 낙서면 거짓."""
    return p[3] > 250 and p[0] >= 205 and p[1] >= 190 and p[2] >= 150 and (p[0] - p[2]) < 80


def _clean_run(pristine, box):
    """
    지울 네모와 **같은 y 띠**에서 아무것도 없는 가장 긴 x 구간을 찾는다.

    빈 자리를 손으로 적어 두면 그림이 바뀔 때마다 틀린다. 종이 줄무늬가 가로라
    같은 띠 안이기만 하면 어디서 떠 오든 줄이 맞는다.
    """
    x0, y0, x1, y1 = box
    px = pristine.load()
    best, run = None, None
    for x in range(PAPER_X[0], PAPER_X[1]):
        clean = x0 - 4 > x or x >= x1 + 4                       # 지울 자리 자체는 안 쓴다
        if clean:
            clean = all(_is_paper(px[x, y]) for y in range(y0, y1))
        if clean:
            run = x if run is None else run
            if best is None or x + 1 - run > best[1] - best[0]:
                best = (run, x + 1)
        else:
            run = None
    if best is None or best[1] - best[0] < 16:
        sys.exit('빈 종이를 못 찾았다: %s' % (box,))
    return best


def erase(img, pristine, box):
    """네모 안을 같은 띠의 빈 종이로 덮는다. 자리가 좁으면 옆으로 이어 붙인다."""
    x0, y0, x1, y1 = box
    sx0, sx1 = _clean_run(pristine, box)
    patch = pristine.crop((sx0, y0, sx1, y1))
    x = x0
    while x < x1:
        img.paste(patch.crop((0, 0, min(patch.width, x1 - x), y1 - y0)), (x, y0))
        x += patch.width


# ============================================================================
# 코드와 견주기
# ============================================================================
def code_cell_counts():
    """RecipeGenerator.GetBaseRecipe 를 읽어 라멘별 칸 수(수량 합)를 센다."""
    if not os.path.isfile(RECIPE_CS):
        return None
    with open(RECIPE_CS, encoding='utf-8-sig') as f:
        text = f.read()

    counts = {}
    for name in ('Shio', 'Shoyu', 'Tonkotsu'):
        m = re.search(r'case RamenType\.%s:(.*?)break;' % name, text, re.S)
        if not m:
            return None
        counts[name] = sum(int(n) for n in re.findall(r'recipe\[[^\]]+\]\s*=\s*(\d+)', m.group(1)))
    return counts


def check():
    """그림에 그린 칸 수와 코드의 기본 레시피를 견준다."""
    drawn = {'Shio': len(CELLS[:8]), 'Shoyu': len(CELLS[8:16]), 'Tonkotsu': len(CELLS[16:])}
    counts = code_cell_counts()
    if counts is None:
        print('! RecipeGenerator.cs 를 읽지 못했다. 칸 수를 못 견줬다.')
        return
    for name in ('Shio', 'Shoyu', 'Tonkotsu'):
        mark = 'ok' if counts[name] == drawn[name] else '!! 어긋남'
        print('  %-9s 코드 %2d칸 / 그림 %2d칸  %s' % (name, counts[name], drawn[name], mark))
    if any(counts[k] != v for k, v in drawn.items()):
        print('\n!! 코드의 기본 레시피가 그림과 다르다. CELLS 를 고쳐야 한다.')


# ============================================================================
# 본체
# ============================================================================
def main():
    if not os.path.isfile(SRC):
        sys.exit('받은 원본이 없다: %s' % SRC)

    img = Image.open(SRC).convert('RGBA')
    pristine = img.copy()                       # 빈 종이는 늘 손대기 전 그림에서 뜬다

    boxes = [(x0 - MARGIN, y0 - MARGIN, x1 + MARGIN, y1 + MARGIN) for (x0, y0, x1, y1), _ in CELLS]
    for box in boxes:
        erase(img, pristine, box)

    for (x0, y0, x1, y1), name in CELLS:
        w, h = x1 - x0 + 2 * MARGIN, y1 - y0 + 2 * MARGIN
        img.alpha_composite(ICONS[name](w, h), (x0 - MARGIN, y0 - MARGIN))

    img.save(OUT)
    print('%s  %s  칸 %d' % (OUT, img.size, len(CELLS)))
    check()


if __name__ == '__main__':
    main()
