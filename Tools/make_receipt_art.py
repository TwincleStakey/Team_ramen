# -*- coding: utf-8 -*-
"""정산 팝업 그림(영수증·버튼 둘)을 캔버스 크기로 굽는다.

받은 원본은 큼직한 그림 한 장씩이다(`_ArtSource/`). 고유색이 3만 개가 넘어 진짜 픽셀아트가
아니라 **픽셀아트 화풍의 그림**이다 — 손님 그림·로고와 같은 쪽이다. 그래서 정수 배율로 줄일
방법이 없고, **화면에 뜰 크기로 여기서 한 번 줄여 두는 것**이 유일하게 안 흐려지는 길이다.
유니티에서 줄이면 canvas 배율(1920에서 2배)과 곱해져 배율이 정수에서 어긋난다.

영수증 700x1220 은 35:61 이라 8배인 280x488 이 가로세로 모두 정수로 떨어진다.
버튼 1024x342 는 224x75 로 줄인다(비율 오차 0.1%, 한 칸도 안 어긋난다).
"""
import os
from PIL import Image

SRC = "_ArtSource"
OUT = "Assets/Art/UI"

JOBS = [
    ("정산표_원본.png", "정산표.png", (280, 488)),
    ("버튼_확인_원본.png", "버튼_확인.png", (224, 75)),
    ("버튼_다시하기_원본.png", "버튼_다시하기.png", (224, 75)),
]


def main():
    os.makedirs(OUT, exist_ok=True)
    for src, dst, size in JOBS:
        im = Image.open(os.path.join(SRC, src)).convert("RGBA")
        baked = im.resize(size, Image.LANCZOS)
        path = os.path.join(OUT, dst)
        baked.save(path)
        print("%s  %dx%d → %dx%d" % (path, im.width, im.height, size[0], size[1]))


main()
