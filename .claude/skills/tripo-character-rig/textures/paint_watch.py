# Skill stage 13 (modular wardrobe), from the Arjun build 2026-10-10. Arjun-specific constants (cut lines,
# opening angles, button heights, waistband details, colours) are templates: re-measure for a new garment.
# Paint Arjun's watch atlas (512 px): dial on the left half, brushed steel on the right.
# Usage: python -I paint_watch.py <out_dir>
import sys, os
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texlib import srgb_to_lin, lin_to_srgb, save_rgb, value_noise3, height_to_normal

OUT = sys.argv[1]
R = 512
v, u = np.meshgrid((np.arange(R)[::-1] + 0.5) / R, (np.arange(R) + 0.5) / R, indexing='ij')
col = np.zeros((R, R, 3))
h = np.zeros((R, R))
# ---------------- steel (right half and the case texel at (0.75, 0.9)) ----------------
steel = u >= 0.5
base = srgb_to_lin([0.80, 0.81, 0.82])
brush = value_noise3(np.stack([u * 900, v * 12, np.zeros_like(u)], -1), seed=3)
col[steel] = base * (0.92 + 0.12 * brush[steel])[..., None]
# bracelet links along the band: dark gaps every 1/24 of the circumference
lu = (u - 0.5) / 0.5 * 24
gap = np.abs(lu - np.round(lu)) < 0.035
link_alt = (np.floor(lu) % 2 == 0)
col[steel & link_alt] *= 1.08
col[steel & gap] *= 0.35
h[steel & gap] = -1.0
# ---------------- dial (left half) ----------------
cx, cy, rd = 0.25, 0.5, 0.24
du, dv = u - cx, v - cy
r = np.hypot(du, dv) / rd
ang = np.arctan2(du, dv)                 # 0 at 12 o'clock, clockwise positive
dial = (u < 0.5) & (r <= 1.02)
navy = srgb_to_lin([0.055, 0.065, 0.085])
sun = 1 + 0.10 * np.cos(ang * 2)          # sunburst sheen
col[dial] = navy * sun[dial][..., None]
ring = dial & (r > 0.86)
col[ring] = srgb_to_lin([0.13, 0.14, 0.16])
white = srgb_to_lin([0.93, 0.93, 0.92])
# hour indices
for k in range(12):
    a = k * np.pi / 6
    along = du * np.sin(a) + dv * np.cos(a)
    across = du * np.cos(a) - dv * np.sin(a)
    w = 0.028 if k % 3 == 0 else 0.018
    ind = dial & (along / rd > 0.62) & (along / rd < 0.84) & (np.abs(across) / rd < w)
    col[ind] = white
    h[ind] = 0.6
# minute ticks
for k in range(60):
    a = k * np.pi / 30
    along = du * np.sin(a) + dv * np.cos(a)
    across = du * np.cos(a) - dv * np.sin(a)
    tick = dial & (along / rd > 0.88) & (along / rd < 0.95) & (np.abs(across) / rd < 0.006)
    col[tick] = white * 0.8


def hand(a, length, width):
    along = du * np.sin(a) + dv * np.cos(a)
    across = du * np.cos(a) - dv * np.sin(a)
    return dial & (along / rd > -0.12) & (along / rd < length) & (np.abs(across) / rd < width * (1 - 0.5 * np.clip(along / rd / length, 0, 1)))


hh = hand(np.radians(305), 0.50, 0.045)   # 10:10
mh = hand(np.radians(60), 0.80, 0.035)
col[hh | mh] = white
h[hh | mh] = 0.9
cap = dial & (r < 0.06)
col[cap] = srgb_to_lin([0.75, 0.76, 0.78])
# outside the dial on the left half: steel bezel colour
rest = (u < 0.5) & ~dial
col[rest] = base
s = lin_to_srgb(col)
save_rgb(os.path.join(OUT, 'Arjun_Watch_BaseColor.png'), s)
nx, ny = height_to_normal(h, 1.5)
n = np.stack([nx, ny, np.ones_like(nx)], -1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
save_rgb(os.path.join(OUT, 'Arjun_Watch_Normal.png'), n * 0.5 + 0.5)
Image.fromarray((np.clip(s, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, 'preview_watch.png'))
print('watch done')
