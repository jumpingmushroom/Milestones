"""Thunderstore icon: 256x256 PNG. An inventory slot in miniature: a dark plate with a golden
border, a potion-red disc for the item, a dark radial sweep covering the part of the cooldown
still to run, and a big "12" countdown in the middle. Written without PIL, which the build box
lacks. Run from the repo root: python3 build/make_icon.py"""
import math
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

def rect(x0, y0, x1, y1, col, alpha=1.0):
    for y in range(int(y0), int(y1)):
        for x in range(int(x0), int(x1)):
            blend(x, y, *col, alpha)

def disc(cx, cy, rad, col, alpha=1.0):
    for y in range(int(cy - rad), int(cy + rad) + 1):
        for x in range(int(cx - rad), int(cx + rad) + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= rad * rad:
                blend(x, y, *col, alpha)

def sweep(x0, y0, x1, y1, frac, col, alpha):
    """Darken the fraction of the square still to run, measured clockwise from the top."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    for y in range(int(y0), int(y1)):
        for x in range(int(x0), int(x1)):
            ang = math.atan2(x - cx, cy - y)          # 0 at top, clockwise positive
            if ang < 0:
                ang += 2 * math.pi
            if ang >= (1 - frac) * 2 * math.pi:
                blend(x, y, *col, alpha)

# 5x7 bitmap digits, drawn as filled cells
FONT = {
    "1": ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
}

def glyph(ch, x, y, cell, col, alpha=1.0, pad=0):
    rows = FONT[ch]
    for r, row in enumerate(rows):
        for c, v in enumerate(row):
            if v == "#":
                rect(x + c * cell - pad, y + r * cell - pad,
                     x + (c + 1) * cell + pad, y + (r + 1) * cell + pad, col, alpha)

WOOD_DARK = (34, 25, 18)
PLATE = (14, 11, 9)
BORDER = (158, 133, 92)
POTION = (196, 52, 48)
POTION_HI = (240, 120, 100)
GLASS = (90, 140, 170)

# ground and slot
rounded_rect(0, 0, W, W, 44 * SS, WOOD_DARK)
m = 22 * SS
rounded_rect(m, m, W - m, W - m, 10 * SS, BORDER)
rounded_rect(m + 3 * SS, m + 3 * SS, W - m - 3 * SS, W - m - 3 * SS, 8 * SS, PLATE)

# a potion: round body, glass neck, highlight
cx, cy = W / 2, W / 2 + 14 * SS
disc(cx, cy, 58 * SS, POTION)
rounded_rect(cx - 16 * SS, cy - 92 * SS, cx + 16 * SS, cy - 44 * SS, 5 * SS, GLASS)
rounded_rect(cx - 22 * SS, cy - 100 * SS, cx + 22 * SS, cy - 86 * SS, 4 * SS, BORDER)
disc(cx - 22 * SS, cy - 22 * SS, 14 * SS, POTION_HI, 0.55)

# sweep: 40% still to run
sweep(m + 3 * SS, m + 3 * SS, W - m - 3 * SS, W - m - 3 * SS, 0.40, (0, 0, 0), 0.62)

# "12" with a dark outline
cell = 14 * SS
tw = 5 * cell
gap = 8 * SS
x0 = W / 2 - (2 * tw + gap) / 2
y0 = W / 2 - 3.5 * cell
for ch, x in (("1", x0), ("2", x0 + tw + gap)):
    glyph(ch, x, y0, cell, (0, 0, 0), 1.0, pad=4 * SS)
for ch, x in (("1", x0), ("2", x0 + tw + gap)):
    glyph(ch, x, y0, cell, (255, 250, 200))

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
