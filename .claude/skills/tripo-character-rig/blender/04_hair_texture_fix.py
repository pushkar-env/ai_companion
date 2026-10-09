# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Remap grey back-of-hair texels to front tones (UV face-id raster + soft blend) (Meera call 38) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
steps = [
 {"label": "Reset 4K basecolor from source", "code": """
src = bpy.data.images['3d+character+model_basecolor.jpg']
old = bpy.data.images.get('Meera_BaseColor')
img4 = src.copy(); img4.scale(4096, 4096)
if old is not None:
    old.user_remap(img4); bpy.data.images.remove(old)
img4.name = 'Meera_BaseColor'
"""},
 {"label": "Rasterize face index map in UV space", "code": """
import numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
W = H = 4096
nf = len(me.polygons)
uv = np.empty(len(me.loops)*2, np.float32); me.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
ls = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', ls)
tri = uv[ls[:,None] + np.arange(3)[None,:]] * np.array([W, H], np.float32)
ids = np.full((H, W), -1, np.int32)
for i in range(nf):
    t = tri[i]
    x0 = max(int(np.floor(t[:,0].min())), 0); x1 = min(int(np.ceil(t[:,0].max())), W-1)
    y0 = max(int(np.floor(t[:,1].min())), 0); y1 = min(int(np.ceil(t[:,1].max())), H-1)
    if x1 < x0 or y1 < y0: continue
    X, Y = np.meshgrid(np.arange(x0, x1+1) + 0.5, np.arange(y0, y1+1) + 0.5)
    a, b, c = t
    area = (b[0]-a[0])*(c[1]-a[1]) - (b[1]-a[1])*(c[0]-a[0])
    sg = 1.0 if area >= 0 else -1.0
    ok = np.ones(X.shape, bool)
    for p, q in ((a, b), (b, c), (c, a)):
        ln = max(float(np.hypot(q[0]-p[0], q[1]-p[1])), 1e-6)
        ok &= sg * ((q[0]-p[0])*(Y-p[1]) - (q[1]-p[1])*(X-p[0])) / ln >= -0.5
    sub = ids[y0:y1+1, x0:x1+1]
    sub[ok & (sub < 0)] = i
# fill gutters from neighbours
for _ in range(6):
    hole = ids < 0
    if not hole.any(): break
    n = ids.copy()
    for sl_dst, sl_src in (((slice(1,None),slice(None)),(slice(None,-1),slice(None))), ((slice(None,-1),slice(None)),(slice(1,None),slice(None))), ((slice(None),slice(1,None)),(slice(None),slice(None,-1))), ((slice(None),slice(None,-1)),(slice(None),slice(1,None)))):
        d = n[sl_dst]; s = ids[sl_src]; m = (d < 0) & (s >= 0); d[m] = s[m]
    ids = n
bpy.app.driver_namespace['uv_face_ids'] = ids
"""},
 {"label": "Blend back hair toward front hair tones", "code": """
import numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nf = len(me.polygons)
ids = bpy.app.driver_namespace['uv_face_ids']
lab = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', lab)
fc = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1,3)
y = fc[:,1]
def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t*t*(3-2*t)
w_face = np.where(lab == 1, smooth(-0.075, 0.02, y), 0.0).astype(np.float32)
front_face = (lab == 1) & (y < -0.06)
back_face = (lab == 1) & (w_face > 0.95)
img4 = bpy.data.images['Meera_BaseColor']; W, H = img4.size
px = np.empty(W*H*4, np.float32); img4.pixels.foreach_get(px); px = px.reshape(H, W, 4)
valid = ids >= 0
fid = np.where(valid, ids, 0)
w = np.where(valid, w_face[fid], 0.0)
# soften per-face steps (3 passes of a 5px box blur on the weight map)
for _ in range(3):
    acc = w.copy()
    for k in (1, 2):
        acc[k:] += w[:-k]; acc[:-k] += w[k:]
    w = acc / 5.0
    acc = w.copy()
    for k in (1, 2):
        acc[:, k:] += w[:, :-k]; acc[:, :-k] += w[:, k:]
    w = acc / 5.0
rgb = px[..., :3]
Lw = np.array([0.299, 0.587, 0.114], np.float32)
fm = valid & front_face[fid]; bm = valid & back_face[fid]
Lsrc = rgb[bm] @ Lw; Ldst = rgb[fm] @ Lw
qs = np.linspace(0, 100, 201)
sq = np.percentile(Lsrc, qs); dq = np.percentile(Ldst, qs)
sel = w > 0.002
L = rgb[sel] @ Lw
Ln = np.interp(L, sq, dq)
fr = rgb[fm]; fl = fr @ Lw
warm = np.median(fr[(fl > 0.05) & (fl < 0.3)], 0); warm_ratio = warm / max(float(warm @ Lw), 1e-4)
t = np.clip((Ln - 0.12) / 0.45, 0, 1)[:, None]
newc = np.clip(Ln[:, None] * (warm_ratio[None, :] * (1 - t) + t), 0, 1)
ws = w[sel][:, None]
rgb_sel = rgb[sel] * (1 - ws) + newc * ws
px[..., :3][sel] = rgb_sel
img4.pixels.foreach_set(px.ravel()); img4.update()
for m in bpy.data.materials:
    if m.name.startswith('Meera_') and m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image is not None and n.image.name in ('3d+character+model_basecolor.jpg',):
                n.image = img4
bpy.app.driver_namespace['hair_blend_stats'] = dict(texels=int(sel.sum()), src=[float(v) for v in sq[::20]], dst=[float(v) for v in dq[::20]])
"""},
]
r = submit("Hair tone blend (seamless)", steps)
result = {"job": r["job"]["state"]}
