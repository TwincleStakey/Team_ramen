# -*- coding: utf-8 -*-
"""
임시 사운드 46개를 코드로 만든다. (2026-09-14 사운드 배선표 기준)

    python Tools/make_temp_sounds.py

효과음 → Assets/Resources/Audio/*.wav   44.1kHz 16bit mono, 피크 -6dB
루프   → Assets/Resources/Audio/*.ogg   ffmpeg 로 변환 (없으면 wav 로 남긴다)

진짜 음원이 오면 같은 이름으로 덮어쓰기만 하면 된다. 코드는 파일 이름으로 찾는다(Sfx.cs).
numpy 없이 순수 파이썬으로 돈다. 전부 만드는 데 1분쯤 걸린다.
"""
import array
import math
import os
import random
import shutil
import subprocess
import sys
import wave

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Assets", "Resources", "Audio"))
PEAK = 0.5  # -6 dB


# ───────────────────────── 기본 재료 ─────────────────────────

def N(sec):
    return int(round(SR * sec))


def silence(sec):
    return [0.0] * N(sec)


def osc(sec, f0, f1=None, shape="sine", curve=1.0, fmod=None):
    """f0→f1 로 훑는 파형. fmod 는 샘플마다 곱할 주파수 배율(비브라토)."""
    count = N(sec)
    out = [0.0] * count
    if f1 is None:
        f1 = f0
    ph = 0.0
    two_pi = 2.0 * math.pi
    sin = math.sin
    for i in range(count):
        t = i / count
        f = f0 + (f1 - f0) * (t ** curve)
        if fmod is not None:
            f *= fmod[i]
        ph += f / SR
        if ph >= 1.0:
            ph -= 1.0
        if shape == "sine":
            v = sin(two_pi * ph)
        elif shape == "tri":
            v = 4.0 * abs(ph - 0.5) - 1.0
        elif shape == "saw":
            v = 2.0 * ph - 1.0
        elif shape == "square":
            v = 1.0 if ph < 0.5 else -1.0
        else:  # pulse
            v = 1.0 if ph < 0.25 else -1.0
        out[i] = v
    return out


def noise(sec, seed=0):
    r = random.Random(seed)
    u = r.uniform
    return [u(-1.0, 1.0) for _ in range(N(sec))]


def lowpass(x, cutoff):
    """한 극 저역 통과. cutoff 는 Hz 하나이거나 샘플마다 다른 리스트."""
    out = [0.0] * len(x)
    y = 0.0
    if isinstance(cutoff, (int, float)):
        a = 1.0 - math.exp(-2.0 * math.pi * cutoff / SR)
        for i, v in enumerate(x):
            y += a * (v - y)
            out[i] = y
    else:
        exp = math.exp
        k = -2.0 * math.pi / SR
        for i, v in enumerate(x):
            y += (1.0 - exp(k * cutoff[i])) * (v - y)
            out[i] = y
    return out


def highpass(x, cutoff):
    low = lowpass(x, cutoff)
    return [v - l for v, l in zip(x, low)]


def resonator(x, freq, r=0.98):
    """2극 공명기. 잡음을 넣으면 그 높이에서 우는 소리가 된다."""
    out = [0.0] * len(x)
    y1 = y2 = 0.0
    scalar = isinstance(freq, (int, float))
    cos = math.cos
    two_pi_sr = 2.0 * math.pi / SR
    rr = r * r
    for i, v in enumerate(x):
        f = freq if scalar else freq[i]
        y = v + 2.0 * r * cos(two_pi_sr * f) * y1 - rr * y2
        y2 = y1
        y1 = y
        out[i] = y
    return out


def env(x, attack=0.005, hold=0.0, decay=None, curve=2.0):
    """attack 초 동안 올리고, hold 뒤 남은 길이(또는 decay 초)를 curve 제곱으로 꺼뜨린다."""
    n = len(x)
    a = N(attack)
    h = N(hold)
    d = (n - a - h) if decay is None else N(decay)
    out = [0.0] * n
    for i in range(n):
        if i < a:
            e = i / a
        elif i < a + h:
            e = 1.0
        else:
            p = (i - a - h) / d if d > 0 else 1.0
            e = max(0.0, 1.0 - p) ** curve
        out[i] = x[i] * e
    return out


def expdecay(x, k, attack=0.002):
    """지수로 꺼뜨린다. k 가 클수록 빨리 꺼진다."""
    a = N(attack)
    exp = math.exp
    return [x[i] * (i / a if i < a else 1.0) * exp(-k * i / SR) for i in range(len(x))]


def gain(x, g):
    return [v * g for v in x]


def mix(*layers):
    length = max(len(l) for l in layers)
    out = [0.0] * length
    for l in layers:
        for i, v in enumerate(l):
            out[i] += v
    return out


def mul(x, y):
    return [a * b for a, b in zip(x, y)]


def place(dst, src, at, g=1.0, wrap=False):
    """dst 의 at 초 자리에 src 를 얹는다. wrap 이면 끝을 넘긴 부분을 앞으로 돌린다(루프용)."""
    n = len(dst)
    s = N(at)
    for i, v in enumerate(src):
        j = s + i
        if j >= n:
            if not wrap:
                break
            j %= n
        dst[j] += v * g
    return dst


def lfo(sec, hz, depth, base=1.0, shape="sine"):
    return [base + depth * v for v in osc(sec, hz, shape=shape)]


def ramp(sec, a, b, curve=1.0):
    n = N(sec)
    return [a + (b - a) * ((i / n) ** curve) for i in range(n)]


def loop_seam(x, sec=0.25):
    """뒤 sec 초를 앞에 겹쳐 섞어 이음매를 없앤다. 길이가 sec 만큼 줄어든다."""
    n = len(x)
    f = N(sec)
    body = x[: n - f]
    tail = x[n - f:]
    for i in range(f):
        t = i / f
        body[i] = body[i] * t + tail[i] * (1.0 - t)
    return body


def finish(x, peak=PEAK, edge_ms=2.0):
    """크기를 peak 로 맞추고 앞뒤를 살짝 다듬는다. 안 다듬으면 툭 끊기는 '틱' 이 난다."""
    m = max(abs(v) for v in x) or 1.0
    g = peak / m
    out = [max(-1.0, min(1.0, v * g)) for v in x]
    f = min(N(edge_ms / 1000.0), len(out) // 4)
    for i in range(f):
        out[i] *= i / f
        out[-1 - i] *= i / f
    return out


def write_wav(path, x):
    a = array.array("h", (int(max(-1.0, min(1.0, v)) * 32767) for v in x))
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(a.tobytes())


# ───────────────────────── 악기 ─────────────────────────

def midi(n):
    return 440.0 * 2.0 ** ((n - 69) / 12.0)


def pluck(freq, sec, shape="tri", k=6.0, harm=0.0, harm_n=2):
    """튕겨서 꺼지는 한 음."""
    x = osc(sec, freq, shape=shape)
    if harm:
        x = mix(x, gain(osc(sec, freq * harm_n), harm))
    return expdecay(x, k)


def marimba(freq, sec, k=7.0):
    return expdecay(mix(osc(sec, freq), gain(osc(sec, freq * 4), 0.18)), k)


def pad(freq, sec, detune=0.4, attack=0.4, curve=1.5):
    """두 사인파를 살짝 어긋나게 겹친 넓은 소리."""
    a = osc(sec, freq * (1 - detune / 100.0))
    b = osc(sec, freq * (1 + detune / 100.0))
    c = osc(sec, freq, shape="tri")
    return env(mix(a, b, gain(c, 0.35)), attack=attack, curve=curve)


def click(sec=0.004, seed=1):
    return env(highpass(noise(sec, seed), 2500), attack=0.0005, curve=1.5)


def drop_tone(f0, f1, sec, k=30.0):
    """물방울. 높이가 떨어지며 금방 꺼진다."""
    return expdecay(osc(sec, f0, f1, curve=0.5), k)


def bubble(f0, sec, seed=0):
    """보글. 물방울과 반대로 높이가 올라간다."""
    return expdecay(osc(sec, f0, f0 * 1.7, curve=0.7), 40.0)


# ───────────────────────── A. 이미 나는 소리 (교체) ─────────────────────────

def sfx_voice_blip():
    x = mix(osc(0.055, 440), gain(osc(0.055, 880), 0.25))
    return env(x, attack=0.005, curve=1.2)


def step_wood(f, seed):
    body = osc(0.09, f, f * 0.8)
    scuff = gain(env(noise(0.09, seed), attack=0.0, decay=0.015, curve=1.0), 0.35)
    return env(mix(body, scuff), attack=0.001, curve=3.0)


def sfx_step_wood_a():
    return step_wood(92, 11)


def sfx_step_wood_b():
    return step_wood(84, 12)


def sfx_step_wood_c():
    return step_wood(101, 13)


def sfx_cut_slurp():
    sec = 0.5
    x = resonator(noise(sec, 3), ramp(sec, 480, 1900, curve=2.0), 0.985)
    x = mul(x, lfo(sec, 34, 0.35))
    return env(x, attack=0.06, curve=1.6)


def sfx_cut_thunder():
    sec = 1.15
    nz = noise(sec, 4)
    crack = expdecay(lowpass(nz, 4000), 60.0)
    rumble = expdecay(mul(lowpass(nz, 140), lfo(sec, 7, 0.25, 0.75)), 3.2)
    return mix(gain(crack, 0.9), gain(rumble, 6.0))


def sfx_cut_crow():
    out = silence(0.62)
    for at, ln, f0, f1 in ((0.0, 0.16, 760, 610), (0.26, 0.30, 700, 480)):
        saw = osc(ln, f0, f1, shape="saw", fmod=lfo(ln, 58, 0.06))
        call = mix(gain(saw, 0.75), gain(noise(ln, 5), 0.25))
        place(out, env(call, attack=ln * 0.07, curve=1.3), at)
    return out


def sfx_cut_dot():
    x = mix(osc(0.05, 900), gain(osc(0.05, 1800), 0.4))
    return env(x, attack=0.001, curve=5.0)


# ───────────────────────── B. 조리 ─────────────────────────

def sfx_cook_pinch():
    out = silence(0.07)
    for at, f, seed in ((0.0, 1500, 21), (0.028, 1900, 22)):
        ping = expdecay(osc(0.035, f), 90.0)
        place(out, mix(ping, gain(click(0.003, seed), 0.8)), at)
    return out


def sfx_cook_drop():
    plop = env(osc(0.14, 520, 160, curve=0.5), attack=0.003, curve=2.0)
    splash = env(lowpass(noise(0.05, 31), 2500), attack=0.002, curve=1.5)
    return mix(plop, gain(splash, 0.35))


def sfx_cook_reject():
    out = silence(0.28)
    place(out, env(osc(0.12, 330, shape="tri"), attack=0.01, curve=1.5), 0.0)
    place(out, env(osc(0.15, 262, shape="tri"), attack=0.01, curve=1.5), 0.13)
    return out


def sfx_cook_scoop():
    sec = 0.3
    ring = expdecay(mix(osc(0.08, 2400), gain(osc(0.08, 3100), 0.5)), 45.0)
    liquid = lowpass(resonator(noise(sec, 41), ramp(sec, 300, 900), 0.97), 3000)
    liquid = env(liquid, attack=0.03, curve=1.8)
    return mix(gain(ring, 0.5), liquid)


def sfx_cook_pour_ladle():
    sec = 0.7  # 그림과 맞춰 둔 길이. 바꾸지 말 것.
    nz = noise(sec, 51)
    low = resonator(nz, ramp(sec, 250, 650), 0.975)
    high = gain(resonator(nz, ramp(sec, 900, 1600), 0.97), 0.35)
    x = lowpass(mix(low, high), 3000)
    x = mul(x, lfo(sec, 11, 0.3))
    return env(x, attack=0.05, hold=0.45, curve=1.0)


def sfx_cook_noodle_shake():
    sec = 1.7  # 이음매 없이 도는 루프
    out = silence(sec)
    shakes = 6
    for i in range(shakes):
        burst = highpass(lowpass(noise(0.12, 60 + i), 2500), 400)
        burst = env(burst, attack=0.004, curve=2.5)
        place(out, burst, i * sec / shakes, 1.0 if i % 2 == 0 else 0.7, wrap=True)
    return out


def sfx_cook_noodle_pour():
    sec = 1.2  # 그림과 맞춰 둔 길이. 바꾸지 말 것.
    slide = lowpass(resonator(noise(sec, 71), ramp(sec, 1400, 500), 0.975), 4000)
    slide = env(slide, attack=0.1, hold=0.3, curve=1.4)
    out = gain(slide, 1.0)
    for at, f in ((0.5, 480), (0.75, 380), (0.95, 300)):
        place(out, gain(drop_tone(f, f * 0.4, 0.1, 35.0), 0.5), at)
    return out


def sfx_cook_ripple():
    sec = 0.35
    x = mix(osc(sec, 220, fmod=lfo(sec, 5, 0.08)), gain(osc(sec, 330), 0.4))
    return env(x, attack=0.02, curve=2.0)


def sfx_cook_shaker():
    sec = 0.1
    x = mul(highpass(noise(sec, 81), 2000), lfo(sec, 90, 0.5))
    return env(x, attack=0.005, curve=2.0)


def sfx_cook_discard():
    sec = 0.6
    splash = env(lowpass(noise(sec, 91), ramp(sec, 5000, 800)), attack=0.01, curve=1.5)
    gurgle = osc(sec, 300, 90, curve=0.6, fmod=lfo(sec, 14, 0.5))
    gurgle = env(gurgle, attack=0.02, curve=2.0)
    out = mix(splash, gain(gurgle, 0.6))
    place(out, gain(drop_tone(400, 150, 0.12, 25.0), 0.7), 0.35)
    return out


def sfx_cook_serve():
    ring = expdecay(mix(osc(0.12, 1800), gain(osc(0.12, 2600), 0.4)), 40.0)
    thump = expdecay(osc(0.15, 110, 90), 25.0)
    return mix(click(0.004, 101), gain(ring, 0.6), gain(thump, 0.9))


def sfx_cook_hover():
    return env(osc(0.03, 2200), attack=0.001, curve=5.0)


# ───────────────────────── C. 화면과 버튼 ─────────────────────────

def sfx_ui_press():
    thump = env(osc(0.08, 170, 120), attack=0.002, curve=2.5)
    return mix(thump, gain(click(0.003, 111), 0.5))


def sfx_ui_dialog_open():
    sec = 0.35
    tone = env(osc(0.25, 880, 440, shape="tri"), attack=0.01, curve=1.5)
    whoosh = env(lowpass(noise(sec, 121), 700), attack=0.05, curve=1.5)
    return mix(tone, gain(whoosh, 0.6))


def paper(sec, f0, f1, seed, curve):
    x = highpass(lowpass(noise(sec, seed), 6000), 1500)
    x = resonator(x, ramp(sec, f0, f1), 0.9)
    x = mul(x, lfo(sec, 40, 0.4))
    return env(x, attack=0.03, curve=curve)


def sfx_ui_note_open():
    return paper(0.25, 2000, 4000, 131, 1.2)


def sfx_ui_note_close():
    return paper(0.22, 4000, 1800, 132, 2.0)


def page(sec, f0, f1, seed, thump_at):
    x = env(lowpass(noise(sec, seed), ramp(sec, f0, f1)), attack=0.05, curve=1.0)
    place(x, gain(expdecay(osc(0.1, 140, 100), 30.0), 0.9), thump_at)
    return x


def sfx_ui_book_open():
    return page(0.4, 6000, 1500, 141, 0.3)


def sfx_ui_book_close():
    return page(0.35, 1500, 5000, 142, 0.27)


def sfx_ui_result():
    out = silence(0.5)
    place(out, pluck(midi(76), 0.3, harm=0.3, k=5.0), 0.0)
    place(out, pluck(midi(79), 0.38, harm=0.3, k=5.0), 0.12)
    return out


def sfx_ui_dayend():
    out = silence(0.9)
    for i, n in enumerate((60, 64, 67, 72)):
        place(out, pluck(midi(n), 0.5, harm=0.3, k=3.0), i * 0.15)
    place(out, gain(pad(midi(72), 0.45, attack=0.05), 0.4), 0.45)
    return out


def sfx_ui_final():
    out = silence(2.0)
    for i, n in enumerate((72, 76, 79, 84)):
        place(out, pluck(midi(n), 0.5, harm=0.3, k=3.0), i * 0.18)
    for n in (72, 76, 79, 84):
        place(out, gain(pad(midi(n), 1.25, attack=0.05), 0.35), 0.72)
        place(out, gain(expdecay(osc(1.25, midi(n), shape="square"), 2.5), 0.06), 0.72)
    return out


# ───────────────────────── D. 연출 ─────────────────────────

def sfx_flow_logo():
    sec = 0.8
    riser = resonator(noise(0.5, 151), ramp(0.5, 400, 3000, curve=2.0), 0.97)
    riser = env(highpass(riser, 300), attack=0.4, curve=1.0)
    out = silence(sec)
    place(out, gain(riser, 0.5), 0.0)
    boom = mix(expdecay(osc(0.3, 80, 45), 6.0), gain(env(lowpass(noise(0.1, 152), 200), attack=0.002), 1.5))
    place(out, boom, 0.5)
    for f in (1568, 2093):
        place(out, gain(expdecay(osc(0.3, f), 8.0), 0.15), 0.5)
    return out


def sfx_flow_stamp():
    """「주문마감」 한 글자가 퉁 하고 박히는 소리. 나무 도장을 내리찍는 결이다.

    때리는 순간의 딱(click) · 판이 울리는 통(저음) · 나무의 짧은 꼬리(잡음) 셋을 겹친다.
    셋 중 하나라도 빠지면 도장이 아니라 북이나 문 닫는 소리로 들린다.
    """
    sec = 0.3
    boom = expdecay(osc(sec, 150, 62, curve=0.7), 16.0)
    body = env(lowpass(noise(0.12, 211), 900), attack=0.001, curve=3.0)
    out = mix(boom, gain(body, 0.7))
    place(out, gain(click(0.004, 212), 0.5), 0.0)
    return out


def sfx_flow_cascade():
    pop = env(osc(0.12, 320, 760, curve=0.5), attack=0.003, curve=2.0)
    return mix(pop, gain(click(0.003, 161), 0.4))


def sfx_flow_fade():
    return env(lowpass(noise(0.6, 171), 400), attack=0.25, curve=1.5)


def sfx_flow_iris():
    sec = 1.2
    rise = env(osc(sec, 220, 880, shape="tri", curve=1.5), attack=0.4, curve=1.0)
    shimmer = mul(highpass(noise(sec, 181), 3000), ramp(sec, 0.0, 1.0, curve=2.0))
    shimmer = env(shimmer, attack=0.0, decay=sec, curve=0.5)
    out = mix(rise, gain(shimmer, 0.25))
    place(out, gain(expdecay(osc(0.3, 1760), 8.0), 0.35), 0.9)
    return out


def sfx_flow_next():
    x = mix(osc(0.06, 660), gain(osc(0.06, 1320), 0.2))
    return env(x, attack=0.002, curve=2.0)


def sfx_flow_hint():
    out = silence(0.2)
    place(out, pluck(midi(72), 0.09, k=10.0), 0.0)
    place(out, pluck(midi(79), 0.11, k=8.0), 0.09)
    return out


def sfx_cut_bars():
    sec = 0.3
    x = env(lowpass(noise(sec, 191), ramp(sec, 3000, 300)), attack=0.02, curve=1.0)
    place(x, expdecay(osc(0.05, 90), 40.0), 0.25)
    return x


def sfx_cut_zoom():
    x = env(osc(0.22, 300, 600, shape="tri"), attack=0.01, hold=0.05, curve=2.0)
    return mix(x, gain(click(0.003, 201), 0.5))


def sfx_cut_cosmos():
    sec = 3.5
    out = silence(sec)
    for n in (60, 64, 67, 71, 72, 76):
        p = pad(midi(n), sec, detune=0.6, attack=1.0, curve=2.0)
        place(out, gain(p, 0.5 if n < 70 else 0.3), 0.0)
    out = mul(out, lfo(sec, 0.5, 0.15))
    sparkle = env(highpass(noise(sec, 211), 6000), attack=1.2, curve=1.5)
    return mix(out, gain(sparkle, 0.08))


def sfx_cut_aura():
    sec = 1.1
    out = silence(sec)
    for f in (1047, 1319, 1568):
        place(out, osc(sec, f, fmod=lfo(sec, 6, 0.01)), 0.0, 0.5)
    out = env(out, attack=0.3, curve=1.5)
    return mix(out, gain(env(highpass(noise(sec, 221), 5000), attack=0.3, curve=1.5), 0.12))


def sfx_cut_sweat():
    out = silence(0.55)
    place(out, drop_tone(1300, 650, 0.12, 25.0), 0.0)
    place(out, gain(drop_tone(1100, 550, 0.1, 30.0), 0.6), 0.35)
    return out


def sfx_cut_thumb_up():
    boing = env(osc(0.25, 180, 820, shape="tri", curve=0.6), attack=0.005, hold=0.05, curve=2.0)
    return mix(boing, gain(click(0.003, 231), 0.6))


def sfx_cut_thumb_down():
    sec = 0.12
    swish = env(lowpass(highpass(noise(sec, 241), 1500), ramp(sec, 6000, 800)), attack=0.01, curve=1.5)
    tone = env(osc(sec, 600, 250), attack=0.005, curve=2.0)
    return mix(swish, gain(tone, 0.5))


def sfx_cut_sparkle():
    x = mix(osc(0.15, 3136), gain(osc(0.15, 4186), 0.5))
    return env(x, attack=0.001, curve=4.0)


def sfx_flow_perfect():
    sec = 1.5
    out = silence(sec)
    for i, n in enumerate((72, 76, 79, 84, 88)):
        place(out, pluck(midi(n), 0.4, harm=0.3, k=4.0), i * 0.08)
    for n in (72, 76, 79):
        place(out, gain(pad(midi(n), 1.1, attack=0.05, curve=2.0), 0.35), 0.4)
    r = random.Random(251)
    for _ in range(7):
        at = r.uniform(0.5, 1.2)
        f = r.choice((2637, 3136, 3520, 4186))
        place(out, gain(env(osc(0.12, f), attack=0.001, curve=4.0), 0.2), at)
    riser = env(highpass(noise(0.4, 252), 4000), attack=0.35, curve=1.0)
    place(out, gain(riser, 0.2), 0.0)
    return out


# ───────────────────────── E. 배경음과 앰비언스 (루프) ─────────────────────────

def bgm_title():
    bpm = 100
    beat = 60.0 / bpm
    bars = 4
    sec = beat * 4 * bars  # 9.6초
    out = silence(sec)
    bass = ((45, 45, 45, 57), (41, 41, 41, 53), (48, 48, 48, 60), (43, 43, 43, 55))
    lead = ((69, 72, 76, 74, 72, 69, 67, 69),
            (65, 69, 72, 69, 67, 64, 67, 69),
            (72, 76, 79, 76, 74, 72, 74, 76),
            (67, 71, 74, 76, 74, 71, 67, 69))
    for b in range(bars):
        for i, n in enumerate(bass[b]):
            place(out, gain(pluck(midi(n), beat * 0.9, shape="pulse", k=4.0), 0.5), (b * 4 + i) * beat, wrap=True)
        for i, n in enumerate(lead[b]):
            place(out, gain(pluck(midi(n), beat * 0.6, shape="tri", k=3.0, harm=0.2), 0.55), (b * 4 + i * 0.5) * beat, wrap=True)
        for i in range(8):
            hat = env(highpass(noise(0.03, 300 + b * 8 + i), 6000), attack=0.001, curve=2.0)
            place(out, hat, (b * 4 + i * 0.5) * beat, 0.25 if i % 2 == 0 else 0.12, wrap=True)
    return out


def bgm_shop():
    bpm = 84
    beat = 60.0 / bpm
    bars = 4
    sec = beat * 4 * bars  # 11.4초
    out = silence(sec)
    bass = (48, 45, 41, 43)
    melody = ((64, 67, 69, 67, 64, 62, 60, 62),
              (64, 67, 69, 72, 69, 67, 64, 67),
              (69, 67, 65, 67, 69, 72, 69, 67),
              (62, 64, 67, 69, 67, 64, 62, 60))
    chords = ((60, 64, 67), (57, 60, 64), (53, 57, 60), (55, 59, 62))
    for b in range(bars):
        for half in range(2):
            place(out, gain(expdecay(osc(beat * 1.8, midi(bass[b])), 3.0), 0.55), (b * 4 + half * 2) * beat, wrap=True)
        for i, n in enumerate(melody[b]):
            place(out, gain(marimba(midi(n), beat * 0.7), 0.5), (b * 4 + i * 0.5) * beat, wrap=True)
        for i, n in enumerate(chords[b]):
            place(out, gain(marimba(midi(n), beat * 1.2, k=5.0), 0.16), (b * 4 + 1 + i * 0.03) * beat, wrap=True)
            place(out, gain(marimba(midi(n), beat * 1.2, k=5.0), 0.16), (b * 4 + 3 + i * 0.03) * beat, wrap=True)
        for i in (1, 3):
            brush = env(lowpass(highpass(noise(0.08, 400 + b * 4 + i), 3000), 9000), attack=0.01, curve=1.5)
            place(out, brush, (b * 4 + i) * beat, 0.12, wrap=True)
    return out


def amb_street_night():
    sec = 13.0
    wind = lowpass(noise(sec, 501), 250)
    wind = mul(wind, lfo(sec, 0.08, 0.4))
    wind = mul(wind, lfo(sec, 0.23, 0.2))
    out = gain(wind, 1.0)
    car_len = 3.0
    car = resonator(noise(car_len, 502), ramp(car_len, 300, 900), 0.97)
    car = mul(car, lfo(car_len, 1.0 / car_len, 0.5, 0.5))
    car = env(car, attack=1.5, curve=1.5)
    place(out, gain(car, 0.03), 4.0, wrap=True)
    r = random.Random(503)
    for k in range(6):
        at = k * 2.1 + r.uniform(0.0, 0.5)
        for c in range(3):
            chirp = mul(osc(0.05, 4200), lfo(0.05, 25, 0.5, 0.5))
            place(out, gain(env(chirp, attack=0.005, curve=1.5), 0.02), at + c * 0.09, wrap=True)
    return loop_seam(out, 1.0)


def boil(sec, bed_cut, bed_gain, bed_lfo, count, fmin, fmax, blen, seed):
    bed = mul(lowpass(noise(sec, seed), bed_cut), lfo(sec, bed_lfo, 0.25))
    out = gain(bed, bed_gain)
    r = random.Random(seed + 1)
    for _ in range(count):
        f = r.uniform(fmin, fmax)
        place(out, gain(bubble(f, blen * r.uniform(0.7, 1.3), seed), r.uniform(0.15, 0.4)), r.uniform(0.0, sec), wrap=True)
    return loop_seam(out, 0.5)


def amb_broth_boil():
    return boil(6.5, 900, 0.3, 3.0, 34, 120, 300, 0.07, 601)


def amb_noodle_pot():
    sec = 6.5
    hiss = highpass(lowpass(noise(sec, 701), 4000), 800)
    hiss = mul(hiss, lfo(sec, 6.0, 0.3))
    out = gain(hiss, 0.35)
    r = random.Random(702)
    for _ in range(70):
        f = r.uniform(300, 700)
        place(out, gain(bubble(f, 0.04 * r.uniform(0.7, 1.3)), r.uniform(0.1, 0.3)), r.uniform(0.0, sec), wrap=True)
    return loop_seam(out, 0.5)


def bgm_cosmos():
    sec = 8.0
    out = silence(sec)
    chords = ((60, 64, 67, 71), (57, 60, 64, 67))
    for c, notes in enumerate(chords):
        for n in notes:
            p = pad(midi(n), 5.0, detune=0.7, attack=2.0, curve=1.5)
            place(out, gain(p, 0.35), c * 4.0, wrap=True)
            p2 = pad(midi(n + 12), 5.0, detune=0.5, attack=2.5, curve=1.5)
            place(out, gain(p2, 0.12), c * 4.0 + 0.5, wrap=True)
    out = mul(out, lfo(sec, 0.3, 0.12))
    r = random.Random(801)
    for _ in range(10):
        f = r.choice((2093, 2637, 3136, 3520))
        place(out, gain(env(osc(0.5, f), attack=0.05, curve=3.0), 0.08), r.uniform(0.0, sec), wrap=True)
    return out


# ───────────────────────── 실행 ─────────────────────────

ONESHOTS = [
    sfx_voice_blip, sfx_step_wood_a, sfx_step_wood_b, sfx_step_wood_c,
    sfx_cut_slurp, sfx_cut_thunder, sfx_cut_crow, sfx_cut_dot,
    sfx_cook_pinch, sfx_cook_drop, sfx_cook_reject, sfx_cook_scoop, sfx_cook_pour_ladle,
    sfx_cook_noodle_shake, sfx_cook_noodle_pour, sfx_cook_ripple, sfx_cook_shaker,
    sfx_cook_discard, sfx_cook_serve, sfx_cook_hover,
    sfx_ui_press, sfx_ui_dialog_open, sfx_ui_note_open, sfx_ui_note_close,
    sfx_ui_book_open, sfx_ui_book_close, sfx_ui_result, sfx_ui_dayend, sfx_ui_final,
    sfx_flow_logo, sfx_flow_cascade, sfx_flow_fade, sfx_flow_iris, sfx_flow_next, sfx_flow_hint,
    sfx_flow_stamp,
    sfx_cut_bars, sfx_cut_zoom, sfx_cut_cosmos, sfx_cut_aura, sfx_cut_sweat,
    sfx_cut_thumb_up, sfx_cut_thumb_down, sfx_cut_sparkle, sfx_flow_perfect,
]

LOOPS = [bgm_title, bgm_shop, amb_street_night, amb_broth_boil, amb_noodle_pot, bgm_cosmos]


def main():
    os.makedirs(OUT, exist_ok=True)
    ffmpeg = shutil.which("ffmpeg")
    only = set(sys.argv[1:])

    for fn in ONESHOTS + LOOPS:
        name = fn.__name__
        if only and name not in only:
            continue
        # 루프는 앞뒤를 다듬으면 이음매가 생기므로 다듬지 않는다.
        is_loop = fn in LOOPS
        x = finish(fn(), edge_ms=0.0 if is_loop else 2.0)
        wav = os.path.join(OUT, name + ".wav")
        write_wav(wav, x)
        if is_loop and ffmpeg:
            ogg = os.path.join(OUT, name + ".ogg")
            subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "5", ogg], check=True)
            os.remove(wav)
            print(f"{name}.ogg  {len(x) / SR:.2f}s")
        else:
            print(f"{name}.wav  {len(x) / SR:.2f}s")


if __name__ == "__main__":
    main()
