#!/usr/bin/env python3
# Turns a song render (e.g. an FL Studio WAV export with its reverb tail) into a seamless loop:
# cuts it to exactly BARS bars of 4/4 at BPM and mixes everything after the loop point back
# onto the start, so the tail rings into the next pass instead of being cut off.
# Usage: python3 Tools/make_loop.py <in.wav> <out.wav> <bpm> <bars>
#   e.g. python3 Tools/make_loop.py ~/Music/Shop.wav Assets/Audio/Music/Shop.wav 94 8
# 16-bit WAV only. Turns the whole track down slightly if the folded tail would clip.
import wave, array, sys
src, dst, bpm, bars = sys.argv[1], sys.argv[2], float(sys.argv[3]), int(sys.argv[4])
w = wave.open(src); n = w.getnframes(); sr = w.getframerate(); ch = w.getnchannels()
a = array.array('h', w.readframes(n))
loop = round(bars * 4 * 60 / bpm * sr)
out = [float(x) for x in a[:loop * ch]]
tail = a[loop * ch:]
for i, x in enumerate(tail[:len(out)]): out[i] += x
peak = max(abs(x) for x in out)
gain = min(1.0, 32767 / peak)
o = array.array('h', (int(round(x * gain)) for x in out))
v = wave.open(dst, 'wb'); v.setnchannels(ch); v.setsampwidth(2); v.setframerate(sr); v.writeframes(o.tobytes()); v.close()
print(f"loop {loop} frames ({loop/sr:.4f}s), tail folded {len(tail)//ch} frames, peak {peak:.0f}, gain {gain:.4f}")
# seam check: jump from the last frame to the first vs the usual frame-to-frame step
L = o[0::ch]
steps = sorted(abs(L[i+1] - L[i]) for i in range(len(L) - 1))
print(f"seam jump {abs(L[0] - L[-1])} (typical step {steps[len(steps)//2]}, 99th pct {steps[int(len(steps)*.99)]})")
