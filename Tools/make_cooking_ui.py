# -*- coding: utf-8 -*-
"""조리 화면 UI 조각 둘 — 마무리 버튼과 튜토리얼 안내판.

## 왜 바꿨나

**마무리 버튼** 은 `Icon.png` 에서 잘라 쓰던 연두색 바였다. 화면에서 **유일하게 채도가 높은
색면**인데, 양옆 두 판(날짜·수익)은 흰 판에 베벨이 들어간 결이라 혼자만 납작했다.
초록은 「제출」이라는 뜻을 이미 갖고 있어 버리지 않고, 짙게 눕히고 금테를 둘러
같은 「칠한 판」 가족으로 넣었다.

**튜토리얼 안내판** 은 크림색 불투명 판이었다. 그릇 위에 떠서 **재료통 하나를 통째로 가렸다** —
「면을 그릇에 담아 주세요」라면서 면 통을 덮고 있으면 곤란하다. 어두운 반투명으로 내리면
밝은 나무 카운터 위에서 글자가 더 잘 뜨면서 통 안도 계속 보인다.

## 9-슬라이스로 굽는다

둘 다 크기가 고정이 아니다.

  안내판  `TutorialPrompt` 가 글 길이에 맞춰 판을 다시 잡는다(두 줄이면 높아진다)
  버튼    지금은 200x36 고정이지만 글이 바뀌면 같이 바뀐다

그래서 **작은 타일로 굽고 유니티가 늘려 쓴다.** 통짜로 구워 늘리면 둥근 모서리와 테가
같이 늘어나 뭉개진다. 빌더가 `LoadSlicedSprite` 로 테두리 폭을 지정한다.

버튼만 **세로를 36에 맞춰** 굽는다. 위아래 띠(밝은 면·어두운 면)가 세로로 늘어나면
칠한 판이 아니라 눌린 금속처럼 보인다. 세로가 원본과 같으면 배율이 1이라 안 늘어난다.
"""
import os
import sys

from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/UI/Generated'

GOLD = (206, 164, 78, 255)
GOLD_DIM = (150, 112, 48, 255)


def tutorial_panel():
    """어두운 반투명 안내판. 32x32 타일, 테두리 10."""
    w = h = 32
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 순수 검정이 아니라 살짝 붉은 검정이다. 배경이 나무(주황)라 회색으로 누르면 싸늘해진다.
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, fill=(18, 12, 9, 185))

    # 아주 옅은 크림 테. 없으면 판 가장자리가 어디인지 안 보여 글자가 공중에 뜬다.
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, outline=(236, 214, 178, 95), width=1)
    return img


def submit_button():
    """짙은 초록 옻칠 판. 48x36 타일, 테두리 10. 세로는 쓰는 크기와 같다."""
    w, h = 48, 36
    base = (42, 88, 52, 255)
    lit = (60, 114, 70, 255)
    dark = (28, 62, 36, 255)

    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=5, fill=base)

    # 위는 밝고 아래는 어둡게. 빛이 위에서 오는 칠한 나무로 읽힌다.
    d.rectangle([4, 3, w - 5, 9], fill=lit)
    d.rectangle([4, h - 10, w - 5, h - 4], fill=dark)

    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=5, outline=GOLD, width=2)
    d.rounded_rectangle([4, 4, w - 5, h - 5], radius=3, outline=GOLD_DIM, width=1)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)

    p = tutorial_panel()
    p.save(os.path.join(OUT, '튜토리얼안내판.png'))
    print('튜토리얼안내판  %dx%d  (테두리 10)' % p.size)

    b = submit_button()
    b.save(os.path.join(OUT, '버튼_마무리.png'))
    print('버튼_마무리    %dx%d  (테두리 10)' % b.size)


if __name__ == '__main__':
    main()
