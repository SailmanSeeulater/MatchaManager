#!/usr/bin/env python3
"""Synthesizes Matcha Manager's retro sound effects into Assets/Audio/*.wav.

Pure standard library, so it runs anywhere: python3 Tools/make_sfx.py
File names match the Sfx enum in Assets/Scripts/SoundFX.cs. Tweak a recipe
below, re-run, and Unity re-imports the changed .wav files.
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Audio")


# --- building blocks: each returns a list of float samples in [-1, 1] -------

def wave_at(shape, phase):
    p = phase % 1.0
    if shape == "sine":
        return math.sin(2 * math.pi * p)
    if shape == "square":
        return 1.0 if p < 0.5 else -1.0
    if shape == "triangle":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    raise ValueError(shape)


def tone(freq, dur, shape="square", vol=0.5, attack=0.004, decay=None, freq_end=None, tremolo=0.0, tremolo_hz=0.0):
    """A note that can slide from freq to freq_end; decay=None holds it, else exponential fade time."""
    n = int(dur * RATE)
    out, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        f = freq if freq_end is None else freq + (freq_end - freq) * (i / n)
        phase += f / RATE
        env = min(1.0, t / attack) if attack > 0 else 1.0
        env *= math.exp(-t / decay) if decay else 1.0
        env *= 1 - min(1.0, max(0.0, (t - (dur - 0.01)) / 0.01))  # 10ms fade-out, no clicks
        if tremolo:
            env *= 1 - tremolo * (0.5 + 0.5 * math.sin(2 * math.pi * tremolo_hz * t))
        out.append(vol * env * wave_at(shape, phase))
    return out


def noise(dur, vol=0.4, decay=None, smooth=0.0, seed=1):
    """White noise; smooth (0..0.99) low-passes it, higher = duller."""
    rng = random.Random(seed)
    n = int(dur * RATE)
    out, last = [], 0.0
    for i in range(n):
        t = i / RATE
        last = smooth * last + (1 - smooth) * rng.uniform(-1, 1)
        env = math.exp(-t / decay) if decay else 1.0
        env *= 1 - min(1.0, max(0.0, (t - (dur - 0.01)) / 0.01))
        out.append(vol * env * last)
    return out


def silence(dur):
    return [0.0] * int(dur * RATE)


def seq(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    return [sum(p[i] for p in parts if i < len(p)) for i in range(n)]


def save(name, samples, peak=0.8):
    top = max(1e-9, max(abs(s) for s in samples))
    scale = peak / top if top > peak else 1.0
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))
    print(f"{name}.wav  {len(samples) / RATE:.2f}s")


NOTE = {"G4": 392.0, "C5": 523.3, "D5": 587.3, "E5": 659.3, "G5": 784.0, "A5": 880.0,
        "C6": 1046.5, "E6": 1318.5, "G6": 1568.0, "A6": 1760.0, "C7": 2093.0}


def bell(freq, dur, vol=0.4):
    """Sine with a quieter octave on top, ringing out."""
    return mix(tone(freq, dur, "sine", vol, decay=dur / 3), tone(freq * 2.01, dur, "sine", vol * 0.3, decay=dur / 5))


# --- the sounds ---------------------------------------------------------------

def main():
    os.makedirs(OUT, exist_ok=True)
    # UI buttons: a short soft tick
    save("Click", tone(1300, 0.035, "triangle", 0.5, decay=0.012))
    # ingredient into the cup: a rising "bloop"
    save("Pour", tone(320, 0.13, "sine", 0.6, decay=0.08, freq_end=760, tremolo=0.4, tremolo_hz=45))
    # cup can't take more: dull double thud
    save("CupFull", seq(tone(150, 0.07, "square", 0.25, decay=0.04), silence(0.03), tone(110, 0.09, "square", 0.25, decay=0.05)))
    # pouring the cup out / tossing the tray: falling whoosh
    save("Dump", mix(noise(0.35, 0.35, decay=0.15, smooth=0.85, seed=2), tone(520, 0.3, "sine", 0.25, decay=0.12, freq_end=140)))
    # happy customer: coin "ka-ching"
    save("ServeGood", seq(tone(NOTE["E6"], 0.07, "square", 0.22, decay=0.05), tone(NOTE["A6"], 0.32, "square", 0.22, decay=0.12)))
    # wrong order: descending buzz
    save("ServeBad", tone(240, 0.38, "square", 0.25, decay=0.3, freq_end=120, tremolo=0.5, tremolo_hz=18))
    # a customer walks in: door bell ding-dong
    save("Arrive", seq(bell(NOTE["G6"], 0.18, 0.4), bell(NOTE["E6"], 0.45, 0.4)))
    # someone gives up and leaves: two sad notes
    save("WalkOut", seq(tone(NOTE["D5"], 0.12, "triangle", 0.45, decay=0.1), tone(NOTE["G4"] * 0.9, 0.28, "triangle", 0.45, decay=0.15, freq_end=320)))
    # picking up a hot snack
    save("Pick", tone(700, 0.05, "triangle", 0.45, decay=0.03, freq_end=900))
    # snack lands on the tray: plate clink
    save("Tray", mix(tone(2400, 0.12, "triangle", 0.3, decay=0.03), tone(3150, 0.1, "sine", 0.2, decay=0.025), noise(0.03, 0.15, decay=0.008, seed=3)))
    # appliance starts: keypad beep
    save("HeatStart", seq(tone(1000, 0.06, "square", 0.18), silence(0.04), tone(1000, 0.06, "square", 0.18)))
    # snack is warm: microwave ding
    save("Ready", bell(NOTE["C7"], 0.7, 0.45))
    # snack burnt: sizzle over a low hum
    save("Burnt", mix(noise(0.6, 0.3, decay=0.3, smooth=0.2, seed=4), tone(95, 0.6, "square", 0.12, decay=0.3)))
    # closing time: little arpeggio down to the root
    save("DayEnd", seq(*(tone(NOTE[n], d, "triangle", 0.45, decay=d * 0.8) for n, d in
                         [("G5", 0.1), ("E5", 0.1), ("C5", 0.1), ("D5", 0.1), ("C5", 0.4)])))
    # a customer's order goes up as a ticket: two quick "noted!" blips
    save("Order", seq(tone(NOTE["C6"], 0.05, "triangle", 0.4, decay=0.04), silence(0.02), tone(NOTE["G6"], 0.09, "triangle", 0.4, decay=0.06)))
    # the cup now makes someone's order: a quick sparkle up
    save("Finished", seq(*(bell(NOTE[n], d, 0.35) for n, d in [("E6", 0.05), ("G6", 0.05), ("C7", 0.25)])))
    # opening: quick rising arpeggio
    save("DayStart", seq(*(tone(NOTE[n], d, "triangle", 0.45, decay=d * 0.8) for n, d in
                           [("C5", 0.07), ("E5", 0.07), ("G5", 0.07), ("C6", 0.3)])))


if __name__ == "__main__":
    main()
