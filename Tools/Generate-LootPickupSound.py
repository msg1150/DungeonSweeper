"""Generate an original loot pickup chime using only the Python standard library.

SPDX-License-Identifier: CC0-1.0
No recordings, sample libraries, sound fonts, or external audio are used.
Run from any directory; the output path is relative to this repository.
"""

import math
from pathlib import Path
import random
import struct
import wave


SAMPLE_RATE = 48000
DURATION = 0.8
OUTPUT = Path(__file__).resolve().parents[1] / "Assets/Audio/SFX/LootPickup.wav"


def synthesize():
    samples = [0.0] * round(SAMPLE_RATE * DURATION)
    rng = random.Random(20261008)
    # A small, dry metallic contact precedes the ascending musical confirmation.
    previous_noise = 0.0
    for i in range(round(0.045 * SAMPLE_RATE)):
        t = i / SAMPLE_RATE
        noise = rng.uniform(-1.0, 1.0)
        smooth_noise = 0.75 * previous_noise + 0.25 * noise
        previous_noise = smooth_noise
        attack = 1.0 - math.exp(-t / 0.0007)
        samples[i] += attack * math.exp(-t / 0.008) * (
            0.11 * smooth_noise + 0.055 * math.sin(math.tau * 310 * t)
        )

    # Each bell is synthesized from damped partials, with softer high overtones.
    for start, frequency, gain, decay in (
        (0.008, 587.3295, 0.48, 0.105),
        (0.065, 880.0, 0.39, 0.115),
        (0.135, 1174.659, 0.34, 0.14),
    ):
        offset = round(start * SAMPLE_RATE)
        for i in range(offset, len(samples)):
            t = (i - offset) / SAMPLE_RATE
            attack = 1.0 - math.exp(-t / 0.0015)
            bell = 0.0
            for ratio, weight, decay_factor in (
                (1.0, 1.0, 1.0),
                (2.003, 0.22, 0.55),
                (2.756, 0.075, 0.38),
                (4.071, 0.025, 0.23),
            ):
                bell += weight * math.exp(-t / (decay * decay_factor)) * math.sin(
                    math.tau * frequency * ratio * t
                )
            samples[i] += gain * attack * bell

    # Very short echoes add a tail without turning a frequent pickup into a wash.
    dry = samples[:]
    for delay, gain in ((0.031, 0.075), (0.053, 0.04)):
        offset = round(delay * SAMPLE_RATE)
        for i in range(offset, len(samples)):
            samples[i] += gain * dry[i - offset]

    # Remove DC and fade both ends to avoid clicks when playback starts or stops.
    dc = sum(samples) / len(samples)
    fade_in = round(0.001 * SAMPLE_RATE)
    fade_out = round(0.08 * SAMPLE_RATE)
    for i in range(len(samples)):
        envelope = min(1.0, i / fade_in)
        remaining = len(samples) - 1 - i
        if remaining < fade_out:
            envelope *= 0.5 - 0.5 * math.cos(math.pi * remaining / fade_out)
        samples[i] = (samples[i] - dc) * envelope

    # Leave 3 dB of headroom; normalization happens only once, during generation.
    peak = max(abs(value) for value in samples)
    scale = 10 ** (-3.0 / 20.0) / peak
    return [round(value * scale * 32767) for value in samples]


def main():
    samples = synthesize()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(SAMPLE_RATE)
        audio.writeframes(struct.pack(f"<{len(samples)}h", *samples))

    # Verify the actual saved PCM, not just the synthesis buffer.
    with wave.open(str(OUTPUT), "rb") as audio:
        assert audio.getparams()[:4] == (1, 2, SAMPLE_RATE, len(samples))
        decoded = struct.unpack(f"<{len(samples)}h", audio.readframes(audio.getnframes()))
    assert decoded[0] == decoded[-1] == 0
    assert all(abs(value) < 32767 for value in decoded)
    peak_db = 20 * math.log10(max(abs(value) for value in decoded) / 32767)
    rms_db = 20 * math.log10(math.sqrt(sum(value * value for value in decoded) / len(decoded)) / 32767)
    print(f"Created: {OUTPUT}")
    print(f"PCM16 mono, {SAMPLE_RATE} Hz, {len(decoded) / SAMPLE_RATE:.2f} s")
    print(f"Peak: {peak_db:.2f} dBFS; RMS: {rms_db:.2f} dBFS; bytes: {OUTPUT.stat().st_size}")


if __name__ == "__main__":
    main()
