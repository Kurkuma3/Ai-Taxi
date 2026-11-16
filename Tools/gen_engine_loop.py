"""Synthesizes a seamless engine loop: Assets/Audio/EngineLoop.wav (mono, 22050 Hz, 16-bit, 1.0 s).

Pure Python (wave + math), no dependencies. Every periodic component has a whole number of cycles in the loop,
so the loop point is click-free. The game raises pitch and volume with speed (EngineAudio), so this is the idle tone:
a 55 Hz fundamental with falling harmonics, amplitude-modulated at 27.5 Hz (two "firings" per cycle of the
modulator, a four-cylinder burble), plus a little low-passed noise for texture, faded at the loop edges.

Run from the project root:  python Tools/gen_engine_loop.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
SECONDS = 1.0
OUT = os.path.join("Assets", "Audio", "EngineLoop.wav")


def main():
    n = int(RATE * SECONDS)
    base = 55.0                                   # 55 cycles per second -> whole cycles in 1 s
    harmonics = [(1, 1.0), (2, 0.55), (3, 0.35), (4, 0.22), (5, 0.12), (6, 0.08)]
    rnd = random.Random(3)
    noise_state = 0.0
    samples = []
    for i in range(n):
        t = i / RATE
        tone = sum(a * math.sin(2 * math.pi * base * h * t) for h, a in harmonics)
        firing = 0.65 + 0.35 * math.sin(2 * math.pi * base * 0.5 * t)       # 27.5 Hz, whole cycles too
        noise_state += 0.08 * ((rnd.random() * 2 - 1) - noise_state)        # one-pole low-pass noise
        edge = min(1.0, i / 200.0, (n - 1 - i) / 200.0)                       # tiny fade only for the noise
        samples.append(tone * firing * 0.32 + noise_state * 0.25 * edge)
    peak = max(abs(s) for s in samples) or 1.0
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * 0.9 * 32767)) for s in samples))
    print("wrote", OUT, n, "samples")


if __name__ == "__main__":
    main()
