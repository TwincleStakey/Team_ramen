# -*- coding: utf-8 -*-
"""해금 연출에 쓰는 소리 셋을 굽는다.

    python Tools/make_unlock_sounds.py

    sfx_unlock_rattle   자물쇠가 덜컹거릴 때 (통마다 한 번)
    sfx_unlock_pop      고리가 팟 하고 열릴 때 (통마다 한 번)
    sfx_unlock_fanfare  새 재료 판이 뜰 때 (한 번)

만드는 재료(osc·env·mix…)는 Tools/make_temp_sounds.py 것을 그대로 가져다 쓴다.
같은 도구로 구워야 이미 있는 46개와 결이 맞는다.

**픽셀 소리로 만든다.** 사인파 대신 사각파·펄스파만 쓰고, 음을 훑지(글리산도) 않고
반음 계단으로 끊어 올린다. 8비트 칩이 낼 수 있는 소리만 쓴 셈이다. 잡음도 흰 잡음을
그대로 쓰지 않고 잘게 끊어 쓴다 — 칩의 잡음 채널이 그렇게 들린다.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from make_temp_sounds import (  # noqa: E402
    N, SR, osc, noise, env, expdecay, gain, mix, place, silence,
    lowpass, highpass, finish, write_wav, OUT,
)

# 반음 열두 개. 칩튠은 음을 훑지 않고 이 칸으로만 뛴다.
def note(name):
    table = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}
    step = table[name[0]]
    if '#' in name:
        step += 1
    octave = int(name[-1])
    return 440.0 * (2.0 ** ((step - 9) / 12.0 + (octave - 4)))


def blip(sec, name, shape="square", k=28.0):
    """음 하나. 사각파를 지수로 뚝 떨어뜨린다."""
    return expdecay(osc(sec, note(name), shape=shape), k)


def arp(names, each=0.045, shape="square", k=22.0):
    """음을 계단으로 이어 붙인다. 칩튠의 아르페지오."""
    total = each * len(names)
    out = silence(total + 0.12)
    for i, n in enumerate(names):
        place(out, blip(each + 0.12, n, shape, k), i * each)
    return out


def chip_noise(sec, seed, cutoff=3000.0, step=220):
    """칩의 잡음 채널. 흰 잡음을 step 샘플마다 한 값으로 뭉개 계단으로 만든다."""
    raw = noise(sec, seed)
    out = [0.0] * len(raw)
    held = 0.0
    for i in range(len(raw)):
        if i % step == 0:
            held = raw[i]
        out[i] = held
    return lowpass(out, cutoff)


def rattle():
    """덜컹. 쇠가 한 번 걸리는 소리 — 낮은 펄스 한 점에 잡음 한 꼬집."""
    body = expdecay(osc(0.09, 150.0, 120.0, shape="pulse"), 46.0)
    grit = expdecay(highpass(chip_noise(0.07, 11, 5200.0, 90), 1400.0), 70.0)
    return finish(mix(gain(body, 0.85), gain(grit, 0.5)), peak=0.34)


def pop():
    """팟! 위로 뛰는 아르페지오 세 음에 잡음 한 방."""
    tune = arp(["C5", "G5", "C6"], each=0.035, k=26.0)
    spark = expdecay(highpass(chip_noise(0.14, 23, 7000.0, 60), 2600.0), 34.0)
    tail = expdecay(osc(0.16, note("C6"), shape="pulse"), 20.0)
    return finish(mix(gain(tune, 0.9), gain(spark, 0.45), gain(tail, 0.25)), peak=0.46)


def fanfare():
    """새 재료 판. 네 음 짧은 팡파르 — 끝 음만 길게 남긴다."""
    lead = arp(["G4", "C5", "E5"], each=0.075, k=14.0)
    out = silence(0.95)
    place(out, lead, 0.0)

    # 끝 음은 두 음(C6+E6)을 겹쳐 반짝이게. 칩에서는 채널 둘을 같이 울리는 자리다.
    place(out, expdecay(osc(0.55, note("C6"), shape="square"), 7.0), 0.225, 0.55)
    place(out, expdecay(osc(0.55, note("E6"), shape="pulse"), 7.5), 0.225, 0.35)

    # 반짝임 한 줌.
    place(out, expdecay(highpass(chip_noise(0.3, 37, 9000.0, 40), 4000.0), 16.0), 0.22, 0.28)

    return finish(out, peak=0.5)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, make in [("sfx_unlock_rattle", rattle),
                       ("sfx_unlock_pop", pop),
                       ("sfx_unlock_fanfare", fanfare)]:
        path = os.path.join(OUT, name + ".wav")
        write_wav(path, make())
        print("구웠습니다:", path)


if __name__ == "__main__":
    main()
