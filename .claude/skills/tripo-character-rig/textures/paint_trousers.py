# Skill stage 13 (modular wardrobe), from the Arjun build 2026-10-10. Arjun-specific constants (cut lines,
# opening angles, button heights, waistband details, colours) are templates: re-measure for a new garment.
# Paint Arjun's trousers atlases: the original grey look (re-baked) and olive chinos, sharing one
# normal map. Usage: python -I paint_trousers.py <mesh_dir> <uv_dir> <arjun_texture_dir> <out_dir>
import sys, os, time
import numpy as np
from PIL import Image
from scipy import ndimage
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texlib import *

MESH, UVD, TEXD, OUT = sys.argv[1:5]
os.makedirs(OUT, exist_ok=True)
RES = 2048
t0 = time.time()
m = load_mesh(os.path.join(MESH, 'Arjun_Trousers.npz'))
newuv = np.load(os.path.join(UVD, 'Arjun_Trousers_uv.npy'))
orph = np.load(os.path.join(UVD, 'Arjun_Trousers_orphans.npy'))
tid, bw = rasterize(m, newuv, RES)
mask = tid >= 0
face = np.where(mask, m['tp'][np.maximum(tid, 0)], -1)
reg = np.where(mask, m['reg'][np.maximum(face, 0)], -1)
P = interp_vertex(m, tid, bw, m['co'].astype(np.float32))
olduv = interp_loop(m, tid, bw, m['uv'].astype(np.float32))
ext = mask & np.isin(reg, (11, 12))
orphan = mask & np.isin(face, orph)
orig = mask & ~ext & ~orphan
x, y, z = P[..., 0], P[..., 1], P[..., 2]
theta = np.arctan2(x, -y)
print('rasterized', round(time.time() - t0, 1), 's; covered', int(mask.sum()))

A = np.asarray(Image.open(os.path.join(TEXD, 'Arjun_BaseColor.jpg')).convert('RGB'), np.float32) / 255.0
NM = np.asarray(Image.open(os.path.join(TEXD, 'Arjun_Normal.png')).convert('RGB'), np.float32) / 255.0
grey = np.zeros(mask.shape + (3,), np.float64)
grey[orig] = srgb_to_lin(sample_bilinear(A, olduv[orig]))
nrm = np.zeros(mask.shape + (3,), np.float32)
nrm[..., 2] = 1
nrm[orig] = sample_bilinear(NM, olduv[orig]) * 2 - 1
# reference grey for new surfaces: the original trousers just below the waist cut, per angle
ring = orig & (z < 0.86) & (z > 0.80)
th_r = theta[ring]
col_r = grey[ring]
bins = np.linspace(-np.pi, np.pi, 73)
binned = np.zeros((72, 3))
for k in range(72):
    s = (th_r >= bins[k]) & (th_r < bins[k + 1])
    binned[k] = np.median(col_r[s], axis=0) if s.sum() > 20 else np.median(col_r, axis=0)
binned = ndimage.uniform_filter1d(binned, 5, axis=0, mode='wrap')
k_ext = np.clip(((theta + np.pi) / (2 * np.pi) * 72).astype(int), 0, 71)
fillc = binned[k_ext]
newsurf = ext | orphan
shade = 1 + 0.03 * (value_noise3(np.stack([x, y, z], -1)[newsurf] / 0.02, seed=7) - 0.5)
grey[newsurf] = fillc[newsurf] * shade[:, None]
del A, NM
print('grey base', round(time.time() - t0, 1), 's')

# ---------------- waistband and front details (analytic in angle and height) ----------------
R_ = np.hypot(x, y)
arc = theta * R_                         # metres around the waist from the front centre
height = np.zeros(mask.shape, np.float32)
dark = np.zeros(mask.shape, np.float32)  # darkening (seams, shadows)
thread = np.zeros(mask.shape, np.float32)
band = ext & (z > 0.957)


def line(d, half=0.00045):
    return np.clip(1 - np.abs(d) / half, 0, 1)


# waistband face, seam below it, top and bottom stitching
dark = np.maximum(dark, np.where(ext, 0.35 * line(z - 0.9595, 0.0012), 0))
thread = np.maximum(thread, np.where(band, line(z - 0.9625) + line(z - 0.9955), 0))
height += np.where(band, 0.6, 0).astype(np.float32)
# belt loops: 1.1 cm wide straps over the band
for a in (0.085, 0.19, 0.36):
    for s in (-1, 1):
        cx = s * a
        d = np.abs(arc - cx)
        loop = ext & (z > 0.9555) & (z < 1.0005) & (d < 0.0055)
        edge = ext & (z > 0.9545) & (z < 1.0015) & (np.abs(d - 0.0058) < 0.0009)
        dark = np.maximum(dark, np.where(edge, 0.45, 0))
        thread = np.maximum(thread, np.where(loop, line(z - 0.9585, 0.0005) + line(z - 0.9975, 0.0005), 0))
        height += np.where(loop, 0.9, 0).astype(np.float32)
# centre back loop
d = np.abs(np.abs(arc) - np.pi * 0.125)
loop = ext & (z > 0.9555) & (z < 1.0005) & (np.abs(np.abs(theta) - np.pi) * R_ < 0.0055)
height += np.where(loop, 0.9, 0).astype(np.float32)
# fly: centre seam and a J-shaped topstitch on the wearer's left
front = (y < -0.05)
dark = np.maximum(dark, np.where(front & (z > 0.855) & (z < 0.958), 0.40 * line(x - 0.0005, 0.0009), 0))
jx, jz = 0.031, 0.885
jline = np.where(z >= jz, np.abs(x - jx), np.abs(np.hypot(x - (jx - 0.031), z - jz) - 0.031))
jmask = front & (z > 0.855) & (z < 0.958) & (x > -0.002) & (x < 0.04)
thread = np.maximum(thread, np.where(jmask, line(jline, 0.0005), 0))
# slant front pockets
for s in (-1, 1):
    # from the band at |x|=0.10 down to the side seam
    p0 = np.array([s * 0.100, 0.958]); p1 = np.array([s * 0.158, 0.845])
    dvec = p1 - p0; L = np.linalg.norm(dvec); u = dvec / L
    rel_x = x - p0[0]; rel_z = z - p0[1]
    tpar = rel_x * u[0] + rel_z * u[1]
    dperp = rel_x * u[1] - rel_z * u[0]
    seg = front | (np.abs(x) > 0.09)
    on = (tpar > 0) & (tpar < L) & seg & (y < 0.02)
    dark = np.maximum(dark, np.where(on, 0.45 * line(dperp, 0.0010), 0))
    thread = np.maximum(thread, np.where(on, line(dperp + s * 0.006, 0.00045), 0))
# button on the waistband tab (wearer's right of the fly)
bx, bz = -0.028, 0.978
db = np.hypot(x - bx, z - bz)
button = front & ext & (db < 0.0062)
rim = front & ext & (db >= 0.0062) & (db < 0.0075)
tab_edge = front & band & (np.abs(x - (-0.040)) < 0.0007)
dark = np.maximum(dark, np.where(rim | tab_edge, 0.5, 0))
height += np.where(button, 1.6, 0).astype(np.float32)
print('details', round(time.time() - t0, 1), 's')


def finish(base_lin, thread_lin, button_lin, name):
    col = base_lin.copy()
    col = col * (1 - dark[..., None])
    col = col + (thread_lin - col) * np.clip(thread, 0, 1)[..., None] * 0.75
    col[button] = button_lin
    holes = button & (np.minimum.reduce([np.hypot(x - bx - ox, z - bz - oz) for ox, oz in ((0.0017, 0.0017), (-0.0017, 0.0017), (0.0017, -0.0017), (-0.0017, -0.0017))]) < 0.0007)
    col[holes] = button_lin * 0.4
    s = dilate(lin_to_srgb(col), mask)
    save_rgb(os.path.join(OUT, name + '_BaseColor.png'), s)
    Image.fromarray((np.clip(s, 0, 1) * 255).astype(np.uint8)).resize((512, 512)).save(os.path.join(OUT, 'preview_' + name + '.png'))


# grey (original) look
finish(grey, srgb_to_lin([0.86, 0.86, 0.84]), srgb_to_lin([0.20, 0.17, 0.15]), 'Arjun_Trousers_Grey')
# olive chinos: recolour the grey's luminance, keeping creases, folds and shading
Lg = grey @ np.array([0.2126, 0.7152, 0.0722])
Lmed = np.median(Lg[orig])
rel = np.clip(Lg / max(Lmed, 1e-4), 0.45, 1.6)
olive = srgb_to_lin([110 / 255, 106 / 255, 73 / 255])
tw = value_noise3(np.stack([x + z, y, z - x], -1) / 0.0011, seed=19)
olive_col = olive * (rel ** 1.1)[..., None] * (1 + 0.05 * (tw - 0.5))[..., None]
finish(olive_col, srgb_to_lin([0.58, 0.56, 0.42]), srgb_to_lin([0.20, 0.15, 0.11]), 'Arjun_Chinos_Olive')
# shared normal map: original fabric normals plus the painted relief
h = ndimage.gaussian_filter(height + 0.6 * np.clip(thread, 0, 1) - 0.8 * dark, 0.8)
nx, ny = height_to_normal(h, 0.8)
nrm[..., 0] += np.where(mask, nx, 0)
nrm[..., 1] += np.where(mask, ny, 0)
nrm /= np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-6)
save_rgb(os.path.join(OUT, 'Arjun_Trousers_Normal.png'), dilate(nrm * 0.5 + 0.5, mask))
print('done', round(time.time() - t0, 1), 's')
