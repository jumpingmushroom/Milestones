"""Thunderstore icon: 256x256 PNG. A dark plate with a golden border, three stacked progress
bars (done / in progress / not started, matching the mod's own bar colours) over dark tracks,
and a small amber pin triangle in the top-right corner standing for the pinned-achievement
tracker. Written without PIL, which the build box lacks.
Run from the repo root: python3 build/make_icon.py"""
import struct
import zlib

S = 256
SS = 3                       # supersample
W = S * SS

px = bytearray(W * W * 4)

def blend(x, y, r, g, b, a):
    if x < 0 or y < 0 or x >= W or y >= W:
        return
    i = (y * W + x) * 4
    ia = 1.0 - a
    px[i] = int(r * a + px[i] * ia)
    px[i + 1] = int(g * a + px[i + 1] * ia)
    px[i + 2] = int(b * a + px[i + 2] * ia)
    px[i + 3] = int(min(255, 255 * a + px[i + 3] * ia))

def rounded_rect(x0, y0, x1, y1, rad, col, alpha=1.0):
    for y in range(int(y0), int(y1)):
        for x in range(int(x0), int(x1)):
            dx = max(x0 + rad - x, 0, x - (x1 - 1 - rad))
            dy = max(y0 + rad - y, 0, y - (y1 - 1 - rad))
            if dx * dx + dy * dy <= rad * rad:
                blend(x, y, *col, alpha)

def corner_triangle(x1, y0, size, col, alpha=1.0):
    """Right triangle with its right angle at (x1, y0) — a small corner marker standing in
    for a pin. Built from per-row rects, so it is `rounded_rect` (rad=0) called row by row."""
    for i in range(int(size)):
        w = size - i
        rounded_rect(x1 - w, y0 + i, x1, y0 + i + 1, 0, col, alpha)

PLATE = (0x1c, 0x1a, 0x17)
BORDER = (0xc8, 0xa0, 0x50)
TRACK = (0x00, 0x00, 0x00)
DONE = (0x5a, 0xc8, 0x5a)
PROGRESS = (0xe0, 0xa0, 0x30)
PIN = (0xe0, 0xa0, 0x30)

# dark plate, golden border (drawn as a larger border-coloured rect showing only at the edge)
border_w = 10 * SS
rounded_rect(0, 0, W, W, 34 * SS, BORDER)
rounded_rect(border_w, border_w, W - border_w, W - border_w, 26 * SS, PLATE)

# three stacked progress bars: done, in progress, in progress (100% / 62% / 25%)
bars = [(1.00, DONE), (0.62, PROGRESS), (0.25, PROGRESS)]
pad = 34 * SS
bar_h = 26 * SS
gap = 20 * SS
x0, x1 = pad, W - pad
total_h = len(bars) * bar_h + (len(bars) - 1) * gap
y = (W - total_h) / 2
rad = bar_h / 2
for frac, col in bars:
    rounded_rect(x0, y, x1, y + bar_h, rad, TRACK, 0.55)
    rounded_rect(x0, y, x0 + (x1 - x0) * frac, y + bar_h, rad, col)
    y += bar_h + gap

# small amber pin triangle in the top-right corner
corner_triangle(W - border_w - 4 * SS, border_w + 4 * SS, 56 * SS, PIN)

# downsample
out = bytearray()
for y in range(S):
    row = bytearray([0])
    for x in range(S):
        r = g = b = a = 0
        for sy in range(SS):
            for sx in range(SS):
                i = ((y * SS + sy) * W + (x * SS + sx)) * 4
                r += px[i]; g += px[i + 1]; b += px[i + 2]; a += px[i + 3]
        n = SS * SS
        row += bytes((r // n, g // n, b // n, a // n))
    out += row

def chunk(tag, data):
    c = tag + data
    return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xffffffff)

png = b"\x89PNG\r\n\x1a\n"
png += chunk(b"IHDR", struct.pack(">IIBBBBB", S, S, 8, 6, 0, 0, 0))
png += chunk(b"IDAT", zlib.compress(bytes(out), 9))
png += chunk(b"IEND", b"")
open("thunderstore/icon.png", "wb").write(png)
print("wrote thunderstore/icon.png", S, "x", S, len(png), "bytes")
