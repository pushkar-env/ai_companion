# Skill stage 13 (modular wardrobe), from the Arjun build 2026-10-10. Arjun-specific constants (cut lines,
# opening angles, button heights, waistband details, colours) are templates: re-measure for a new garment.
# Repack texture coordinates for Arjun's new garment atlases.
# Usage: python -I uvpack.py <mesh_dir> <out_dir>
# Keeps the original Tripo charts (uniform texel density) for shirt and trouser faces,
# unwraps the tee and the trouser waist extension cylindrically, gives the buttons one
# shared patch, and shelf-packs every island into the unit square without rotation.
import sys, os
import numpy as np

MESH, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
RES = 2048
MARGIN = 6.0 / RES


def load(name):
    d = np.load(os.path.join(MESH, name + '.npz'), allow_pickle=False)
    return {k: d[k] for k in d.files}


def face_loops(m):
    return [np.arange(s, s + t) for s, t in zip(m['ls'], m['lt'])]


def islands_from_uv(m, faces):
    """Union faces that share an edge whose UVs agree on both sides."""
    parent = {f: f for f in faces}

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    loops = face_loops(m)
    edge_owner = {}
    for f in faces:
        L = loops[f]
        n = len(L)
        for k in range(n):
            a, b = L[k], L[(k + 1) % n]
            va, vb = m['lv'][a], m['lv'][b]
            key = (min(va, vb), max(va, vb))
            uva, uvb = m['uv'][a], m['uv'][b]
            if va > vb:
                uva, uvb = uvb, uva
            if key in edge_owner:
                g, ua, ub = edge_owner[key]
                if np.abs(ua - uva).max() < 1e-5 and np.abs(ub - uvb).max() < 1e-5:
                    ra, rb = find(f), find(g)
                    if ra != rb:
                        parent[ra] = rb
            else:
                edge_owner[key] = (f, uva, uvb)
    groups = {}
    for f in faces:
        groups.setdefault(find(f), []).append(f)
    return list(groups.values())


def tri_area3(m, tris):
    p = m['co'][m['lv'][tris]]
    return 0.5 * np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1)


def tri_area_uv(uv, tris):
    q = uv[tris]
    a = q[:, 1] - q[:, 0]
    b = q[:, 2] - q[:, 0]
    return 0.5 * np.abs(a[:, 0] * b[:, 1] - a[:, 1] * b[:, 0])


def metric_scale(m, uv, faces):
    sel = np.isin(m['tp'], faces)
    tris = m['tl'][sel]
    a3 = tri_area3(m, tris).sum()
    au = tri_area_uv(uv, tris).sum()
    return np.sqrt(a3 / max(au, 1e-12))   # metres per UV unit


def shelf_pack(boxes, reserved=None):
    """boxes: list of (w, h) in metres. Returns (scale uv/m, positions) inside [0,1] x [0,top]."""
    order = sorted(range(len(boxes)), key=lambda i: -boxes[i][1])
    top = 1.0 - (reserved[1] + MARGIN if reserved is not None else 0.0)

    def place(g):
        pos = [None] * len(boxes)
        x = MARGIN
        y = MARGIN
        shelf_h = 0.0
        for i in order:
            w = boxes[i][0] * g
            h = boxes[i][1] * g
            if w + 2 * MARGIN > 1.0:
                return None
            if x + w + MARGIN > 1.0:
                y += shelf_h + MARGIN
                x = MARGIN
                shelf_h = 0.0
            pos[i] = (x, y)
            x += w + MARGIN
            shelf_h = max(shelf_h, h)
        if y + shelf_h + MARGIN > top:
            return None
        return pos

    lo, hi = 0.01, 1000.0
    best = None
    for _ in range(60):
        mid = (lo + hi) / 2
        p = place(mid)
        if p is None:
            hi = mid
        else:
            lo = mid
            best = (mid, p)
    return best


def pack(m, groups, out_name):
    """groups: list of dicts {faces, uv (per-loop array for those faces' loops), kind}."""
    newuv = np.zeros_like(m['uv'])
    loops = face_loops(m)
    boxes = []
    entries = []
    for g in groups:
        L = np.concatenate([loops[f] for f in g['faces']])
        uvm = g['uvm'][L]                      # metric coordinates (metres)
        lo = uvm.min(0)
        hi = uvm.max(0)
        boxes.append((hi[0] - lo[0] + 1e-6, hi[1] - lo[1] + 1e-6))
        entries.append((L, uvm, lo))
    res = shelf_pack(boxes, reserved=groups[0].get('reserve'))
    if res is None:
        raise SystemExit('packing failed for ' + out_name)
    g, pos = res
    for (L, uvm, lo), p in zip(entries, pos):
        newuv[L] = (uvm - lo) * g + np.array(p)
    np.save(os.path.join(OUT, out_name + '_uv.npy'), newuv.astype(np.float32))
    return g


# ---------------- chambray top: shirt charts + tee + button patch ----------------
top = load('Arjun_Shirt_Chambray')
fm = top['fm']
shirt_faces = np.nonzero(fm == 0)[0].tolist()
tee_faces = np.nonzero(fm == 1)[0].tolist()
btn_faces = np.nonzero(fm == 2)[0].tolist()
s_shirt = metric_scale(top, top['uv'], shirt_faces)     # metres per old-UV unit
uvm = top['uv'].astype(np.float64) * s_shirt
# tee: u in [0,1] around, v in [0,1] along the profile -> metres
loops = face_loops(top)
tee_L = np.concatenate([loops[f] for f in tee_faces])
co = top['co']
tv = top['lv'][tee_L]
z = co[tv, 2]
r = np.hypot(co[tv, 0], co[tv, 1])
circ = 2 * np.pi * np.median(r[(z > 1.05) & (z < 1.25)])
# profile length: sum of ring-to-ring distances along the front column
tee_v = np.unique(tv)
N = 144
nr = len(tee_v) // N
first = tee_v.min()
front = [first + k * N + N // 2 for k in range(nr)]
prof = np.sum(np.linalg.norm(np.diff(co[front], axis=0), axis=1))
uvm[tee_L, 0] = top['uv'][tee_L, 0] * circ * 0.55
uvm[tee_L, 1] = top['uv'][tee_L, 1] * prof * 0.55
def orphans(m, faces, limit=4.0):
    """Faces whose texture triangle is far larger than their surface (hole fills that bridge charts)."""
    sel = np.isin(m['tp'], faces)
    tris = m['tl'][sel]
    tpf = m['tp'][sel]
    ratio = tri_area_uv(m['uv'], tris) / np.maximum(tri_area3(m, tris), 1e-12)
    med = np.median(ratio[ratio > 0])
    bad = np.unique(tpf[ratio > limit * med])
    return set(bad.tolist())


def planar_metric(m, f, uvm):
    """Own flat projection (metres) for one face."""
    L = face_loops(m)[f]
    p = m['co'][m['lv'][L]].astype(np.float64)
    n = np.cross(p[1] - p[0], p[2] - p[0])
    n /= max(np.linalg.norm(n), 1e-12)
    e1 = p[1] - p[0]
    e1 /= max(np.linalg.norm(e1), 1e-12)
    e2 = np.cross(n, e1)
    uvm[L, 0] = (p - p[0]) @ e1
    uvm[L, 1] = (p - p[0]) @ e2


bad = orphans(top, shirt_faces)
groups = []
for isl in islands_from_uv(top, [f for f in shirt_faces if f not in bad]):
    groups.append({'faces': isl, 'uvm': uvm})
for f in bad:
    planar_metric(top, f, uvm)
    groups.append({'faces': [f], 'uvm': uvm})
groups.append({'faces': tee_faces, 'uvm': uvm})
print('top orphan faces', len(bad))
np.save(os.path.join(OUT, 'Arjun_Shirt_Chambray_orphans.npy'), np.array(sorted(bad), dtype=np.int32))
groups[0]['reserve'] = (0.07, 0.07)
g_top = pack(top, groups, 'Arjun_Shirt_Chambray')
# buttons: one shared patch in the reserved strip along the top edge
nuv = np.load(os.path.join(OUT, 'Arjun_Shirt_Chambray_uv.npy'))
btn_L = np.concatenate([loops[f] for f in btn_faces])
bx0, by0, bw = MARGIN, 1.0 - 0.07 - MARGIN / 2, 0.07
nuv[btn_L] = np.array([bx0, by0]) + top['uv'][btn_L] * bw
np.save(os.path.join(OUT, 'Arjun_Shirt_Chambray_uv.npy'), nuv.astype(np.float32))
print('top: islands', len(groups), 'uv per metre', round(g_top, 1), 'px per mm', round(g_top * RES / 1000, 2),
      'tee circ', round(circ, 3), 'profile', round(prof, 3))

# ---------------- trousers: original charts + cylindrical waist extension ----------------
tr = load('Arjun_Trousers')
reg = tr['reg']
ext_faces = np.nonzero(np.isin(reg, (11, 12)))[0].tolist()
orig_faces = np.nonzero(~np.isin(reg, (11, 12)))[0].tolist()
s_tr = metric_scale(tr, tr['uv'], orig_faces)
uvm = tr['uv'].astype(np.float64) * s_tr
loops = face_loops(tr)
co = tr['co']
R0 = 0.14
for f in ext_faces:
    L = loops[f]
    v = tr['lv'][L]
    th = np.arctan2(co[v, 0], -co[v, 1])
    if th.max() - th.min() > np.pi:
        th = np.where(th < 0, th + 2 * np.pi, th)
    uvm[L, 0] = th * R0
    # arc length up the profile: z plus the inward fold of the waistband lip
    uvm[L, 1] = co[v, 2]
bad = orphans(tr, orig_faces)
groups = [{'faces': isl, 'uvm': uvm} for isl in islands_from_uv(tr, [f for f in orig_faces if f not in bad])]
for f in bad:
    planar_metric(tr, f, uvm)
    groups.append({'faces': [f], 'uvm': uvm})
groups.append({'faces': ext_faces, 'uvm': uvm})
print('trousers orphan faces', len(bad))
np.save(os.path.join(OUT, 'Arjun_Trousers_orphans.npy'), np.array(sorted(bad), dtype=np.int32))
g_tr = pack(tr, groups, 'Arjun_Trousers')
print('trousers: islands', len(groups), 'uv per metre', round(g_tr, 1), 'px per mm', round(g_tr * RES / 1000, 2))
