# Skill stage 13 (modular wardrobe), from the Arjun build 2026-10-10. Arjun-specific constants (cut lines,
# opening angles, button heights, waistband details, colours) are templates: re-measure for a new garment.
# Paint Arjun's chambray-shirt + white-tee + button atlas (2048 px, base colour and normal).
# Usage: python -I paint_top.py <mesh_dir> <uv_dir> <arjun_texture_dir> <out_dir>
import sys, os, time
import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texlib import *

MESH, UVD, TEXD, OUT = sys.argv[1:5]
os.makedirs(OUT, exist_ok=True)
RES = 2048
t0 = time.time()
m = load_mesh(os.path.join(MESH, 'Arjun_Shirt_Chambray.npz'))
newuv = np.load(os.path.join(UVD, 'Arjun_Shirt_Chambray_uv.npy'))
orph = np.load(os.path.join(UVD, 'Arjun_Shirt_Chambray_orphans.npy'))
off = np.load(os.path.join(MESH, 'Arjun_Shirt_Chambray_offset.npy'))
tee_first, tee_n = np.load(os.path.join(MESH, 'Arjun_Shirt_Chambray_tee_first.npy'))

tid, bw = rasterize(m, newuv, RES)
mask = tid >= 0
face = np.where(mask, m['tp'][np.maximum(tid, 0)], -1)
mat = np.where(mask, m['fm'][np.maximum(face, 0)], -1)
reg = np.where(mask, m['reg'][np.maximum(face, 0)], -1)
P = interp_vertex(m, tid, bw, m['co'].astype(np.float32))
olduv = interp_loop(m, tid, bw, m['uv'].astype(np.float32))
offv = interp_vertex(m, tid, bw, off.astype(np.float32))
print('rasterized', round(time.time() - t0, 1), 's; covered', int(mask.sum()))

shirt = mask & (mat == 0)
tee = mask & (mat == 1)
btn = mask & (mat == 2)
fold = shirt & (reg == 13)
orphan = shirt & np.isin(face, orph)
x, y, z = P[..., 0], P[..., 1], P[..., 2]
theta = np.arctan2(x, -y)
rad = np.hypot(x, y)

# ---------------- original shirt atlas: fold shading, seam lines, normals ----------------
A = np.asarray(Image.open(os.path.join(TEXD, 'Arjun_BaseColor.jpg')).convert('RGB'), np.float32) / 255.0
L = A @ np.array([0.299, 0.587, 0.114], np.float32)
del A
Ls = ndimage.gaussian_filter(L, 1.5)
Lb = ndimage.gaussian_filter(L, 24)
ratio_img = np.clip(Ls / np.maximum(Lb, 1e-3), 0.72, 1.3)[..., None]
seam_img = np.clip((ndimage.gaussian_filter(L, 4.0) - Ls) / np.maximum(Lb, 1e-3), 0, 0.6)[..., None]
del L, Ls, Lb
use_old = shirt & ~fold & ~orphan
ratio = np.ones(mask.shape, np.float32)
seam = np.zeros(mask.shape, np.float32)
ratio[use_old] = sample_bilinear(ratio_img, olduv[use_old])[..., 0]
seam[use_old] = sample_bilinear(seam_img, olduv[use_old])[..., 0]
del ratio_img, seam_img
NM = np.asarray(Image.open(os.path.join(TEXD, 'Arjun_Normal.png')).convert('RGB'), np.float32) / 255.0
nold = np.zeros(mask.shape + (3,), np.float32)
nold[..., 2] = 1
nold[use_old] = sample_bilinear(NM, olduv[use_old]) * 2 - 1
del NM
print('old atlas sampled', round(time.time() - t0, 1), 's')

# ---------------- distances to edges, pocket outline ----------------
fm, fr = m['fm'], m['reg']
fabric_faces = (fm == 0) & (fr != 13)
pts_edge = edge_points(m, fabric_faces)
d_edge = np.full(mask.shape, 1.0, np.float32)
d_edge[shirt] = cKDTree(pts_edge).query(P[shirt], workers=-1)[0]
# pocket: raised patch on the left chest (wearer's left, +X)
co = m['co']
vpocket = (off > 0.0025) & (co[:, 0] > 0.04) & (co[:, 0] < 0.175) & (co[:, 2] > 1.17) & (co[:, 2] < 1.375) & (co[:, 1] < -0.07)
ls, lt = m['ls'], m['lt']
pocket_face = np.zeros(len(ls), bool)
for f in np.nonzero(fabric_faces)[0]:
    v = m['lv'][ls[f]:ls[f] + lt[f]]
    if vpocket[v].sum() >= len(v) - 0:
        pocket_face[f] = True
pocket_face = ndimage.binary_dilation(pocket_face, iterations=0) if False else pocket_face
pts_pocket = shared_points(m, pocket_face, fabric_faces & ~pocket_face)
in_pocket = shirt & pocket_face[np.maximum(face, 0)]
d_pocket = np.full(mask.shape, 1.0, np.float32)
if len(pts_pocket):
    tp = cKDTree(pts_pocket)
    near_pocket = shirt & (x > 0.02) & (x < 0.2) & (z > 1.15) & (z < 1.4)
    d_pocket[near_pocket] = tp.query(P[near_pocket], workers=-1)[0]
    top_z = pts_pocket[:, 2].max()
    pts_top = pts_pocket[pts_pocket[:, 2] > top_z - 0.010]
    d_top = np.full(mask.shape, 1.0, np.float32)
    d_top[near_pocket] = cKDTree(pts_top).query(P[near_pocket], workers=-1)[0]
else:
    d_top = np.full(mask.shape, 1.0, np.float32)
print('distances', round(time.time() - t0, 1), 's; pocket faces', int(pocket_face.sum()))

# ---------------- front plackets and buttonholes ----------------
XM, XP = -0.026, -0.002
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [9, 9, 11, 8, 6, 2, 0]
DP = [23, 23, 23, 21, 15, 5, 0]
te_m = np.arctan2(XM, 0.15) - np.radians(np.interp(z, ZS, DM))
te_p = np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP))
front = shirt & (y < -0.03) & (z > 0.82) & (z < 1.345)
d_m = np.where(front & (x < -0.01), (te_m - theta) * rad, 9.0)
d_p = np.where(front & (x >= -0.01), (theta - te_p) * rad, 9.0)
d_placket = np.minimum(np.where(d_m > -0.002, d_m, 9.0), np.where(d_p > -0.002, d_p, 9.0))
in_placket = front & (d_placket < 0.028)


def line(d, at, half=0.00042):
    return np.clip(1 - np.abs(d - at) / half, 0, 1)


stitch = np.zeros(mask.shape, np.float32)
stitch = np.maximum(stitch, np.where(in_placket, np.maximum(line(d_placket, 0.003), line(d_placket, 0.022)), 0))
stitch = np.maximum(stitch, np.where(shirt & ~in_placket & ~fold, line(d_edge, 0.0062), 0))
stitch = np.maximum(stitch, np.where(in_pocket, np.maximum(line(d_pocket, 0.0028), line(d_top, 0.0125)), 0))
# dashed thread: short gaps every ~3.5 mm along the dominant direction
dash = 0.55 + 0.45 * (np.sin((x * 1.7 + y * 1.3 + z * 2.1) / 0.0035 * 2 * np.pi) > -0.6)
stitch *= dash
holes = np.zeros(mask.shape, np.float32)
tack = np.zeros(mask.shape, np.float32)
for zb in (1.294, 1.199, 1.113, 1.031, 0.949):
    slit = (np.abs(d_p - 0.013) < 0.0008) & (np.abs(z - zb) < 0.0072)
    holes = np.maximum(holes, slit.astype(np.float32))
    rim = (np.abs(d_p - 0.013) < 0.0017) & (np.abs(z - zb) < 0.0084) & ~slit
    tack = np.maximum(tack, rim.astype(np.float32))

# ---------------- chambray cloth ----------------
ws = np.clip((np.abs(x) - 0.19) / 0.04, 0, 1)[..., None]
scale_torso = np.array([0.0016, 0.0016, 0.014])
scale_sleeve = np.array([0.014, 0.0016, 0.0016])
idx = np.nonzero(shirt)
p = P[idx].astype(np.float64)
w_s = ws[idx]
q = p / (scale_torso * (1 - w_s) + scale_sleeve * w_s)
slub = fbm(q, 3, seed=3)
fine = value_noise3(p / 0.0007, seed=11)
heather = value_noise3(p / 0.0042, seed=29)
dark = srgb_to_lin([112 / 255, 143 / 255, 176 / 255])
light = srgb_to_lin([171 / 255, 198 / 255, 222 / 255])
tmix = np.clip(0.52 + 0.75 * (slub - 0.5) + 0.18 * (heather - 0.5), 0, 1)[:, None]
col = dark + (light - dark) * tmix
fleck = np.clip((fine - 0.80) / 0.20, 0, 1)[:, None]
col = col + (srgb_to_lin([0.94, 0.95, 0.96]) - col) * fleck * 0.40
# fold shading from the original shirt and seam lines
r_ = ratio[idx][:, None]
col = col * (1 + 0.55 * (r_ - 1))
col = col * (1 - 0.35 * np.clip(seam[idx][:, None] * 1.6, 0, 1))
# worn, lighter edges (hem, front edges, cuffs, collar)
wear = np.clip(1 - d_edge[idx] / 0.0035, 0, 1)[:, None] * 0.12
col = col + (srgb_to_lin([0.80, 0.86, 0.91]) - col) * wear
# fold-under strips read slightly darker (inside of the fabric)
col = np.where(fold[idx][:, None], col * 0.82, col)
# stitching and buttonholes
thread = srgb_to_lin([0.86, 0.89, 0.92])
st = stitch[idx][:, None] * 0.85
col = col + (thread - col) * st
col = col + (thread - col) * tack[idx][:, None] * 0.6
col = col * (1 - 0.72 * holes[idx][:, None])
albedo = np.zeros(mask.shape + (3,), np.float64)
albedo[idx] = col
height = np.zeros(mask.shape, np.float32)
height[idx] = (0.30 * (slub - 0.5) + 0.25 * (fine - 0.5) + 0.9 * stitch[idx] - 1.2 * holes[idx] + 0.4 * tack[idx]).astype(np.float32)
print('chambray painted', round(time.time() - t0, 1), 's')

# ---------------- white cotton tee ----------------
it = np.nonzero(tee)
pt = P[it].astype(np.float64)
ring_v = np.full(len(m['co']), -1.0, np.float32)
ring_v[tee_first:tee_first + tee_n] = (np.arange(tee_n) // 144).astype(np.float32)
ring = interp_vertex(m, tid, bw, ring_v)[it]
u_tee = olduv[it][:, 0]
circ_pos = u_tee * 0.892
rib = (ring >= 43.6).astype(np.float64)
ribs = 0.5 + 0.5 * np.sin(circ_pos / 0.0022 * 2 * np.pi)
knit = value_noise3(pt / 0.0009, seed=41)
blouse_env = np.clip(1 - np.abs(pt[:, 2] - 1.03) / 0.04, 0, 1)
blouse = 0.5 + 0.5 * np.sin(np.arctan2(pt[:, 0], -pt[:, 1]) * 13 + 3 * value_noise3(pt / 0.03, seed=5))
white = srgb_to_lin([237 / 255, 235 / 255, 231 / 255])
tcol = np.tile(white, (len(pt), 1))
tcol = tcol * (1 + 0.03 * (knit - 0.5))[:, None]
tcol = tcol * (1 - 0.07 * rib * ribs)[:, None]
tcol = tcol * (1 - 0.09 * blouse_env * blouse)[:, None]
tcol = np.where((ring >= 46.4)[:, None], tcol * 0.86, tcol)
albedo[it] = tcol
height[it] = (0.15 * (knit - 0.5) + 0.6 * rib * ribs + 0.7 * blouse_env * blouse).astype(np.float32)

# ---------------- buttons ----------------
ib = np.nonzero(btn)
buv = olduv[ib]
rb = np.linalg.norm(buv - 0.5, axis=1) / 0.4
ang = np.arctan2(buv[:, 1] - 0.5, buv[:, 0] - 0.5)
bcol = np.tile(srgb_to_lin([243 / 255, 241 / 255, 236 / 255]), (len(buv), 1))
holes_b = np.zeros(len(buv))
for k in range(4):
    a = np.pi / 4 + k * np.pi / 2
    hx, hy = 0.5 + 0.4 * 0.30 * np.cos(a), 0.5 + 0.4 * 0.30 * np.sin(a)
    holes_b = np.maximum(holes_b, np.clip(1 - np.hypot(buv[:, 0] - hx, buv[:, 1] - hy) / (0.4 * 0.11), 0, 1) > 0)
rim_b = ((rb > 0.80) & (rb <= 1.0)).astype(np.float64)
bcol = bcol * (1 - 0.10 * rim_b)[:, None]
bcol = bcol * (1 - 0.65 * holes_b)[:, None]
albedo[ib] = bcol
height[ib] = (0.8 * rim_b - 1.5 * holes_b).astype(np.float32)

# ---------------- normals ----------------
h_s = ndimage.gaussian_filter(height, 0.6)
nx, ny = height_to_normal(h_s, 0.9)
nrm = nold.copy()
nrm[tee | btn] = np.array([0, 0, 1], np.float32)
nrm[..., 0] += np.where(mask, nx, 0)
nrm[..., 1] += np.where(mask, ny, 0)
nrm /= np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-6)

# ---------------- write ----------------
albedo_s = lin_to_srgb(albedo)
albedo_s = dilate(albedo_s, mask)
nrm_rgb = dilate(nrm * 0.5 + 0.5, mask)
save_rgb(os.path.join(OUT, 'Arjun_Chambray_BaseColor.png'), albedo_s)
save_rgb(os.path.join(OUT, 'Arjun_Chambray_Normal.png'), nrm_rgb)
Image.fromarray((np.clip(albedo_s, 0, 1) * 255).astype(np.uint8)).resize((512, 512)).save(os.path.join(OUT, 'preview_chambray.png'))
print('done', round(time.time() - t0, 1), 's')
