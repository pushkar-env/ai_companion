# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Per-face colour features from a 1024 copy of the atlas (Meera call 19) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
img = bpy.data.images['review_basecolor_1024']
W,H = img.size
px = np.empty(W*H*4, np.float32); img.pixels.foreach_get(px); px = px.reshape(H,W,4)
nf = len(me.polygons)
loop_start = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', loop_start)
loop_total = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', loop_total)
uv = np.empty(len(me.loops)*2, np.float32); me.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
# triangles only after decimate? check
tri = bool((loop_total==3).all())
face_of_loop = np.repeat(np.arange(nf), loop_total)
uvc = np.zeros((nf,2), np.float64); np.add.at(uvc, face_of_loop, uv); uvc /= loop_total[:,None]
def sample(uvs):
    x = np.clip((uvs[:,0]%1.0)*W, 0, W-1).astype(int); y = np.clip((uvs[:,1]%1.0)*H, 0, H-1).astype(int)
    return px[y,x,:3]
# average of center + 3 corner-shrunk samples
cols = sample(uvc)
for k in range(3):
    idx = loop_start + np.minimum(k, loop_total-1)
    cols = cols + sample(0.6*uvc + 0.4*uv[idx])
cols /= 4.0
# linear -> approx sRGB for intuitive thresholds
srgb = np.where(cols <= 0.0031308, cols*12.92, 1.055*np.power(np.clip(cols,0,None), 1/2.4) - 0.055)
r,g,b = srgb[:,0], srgb[:,1], srgb[:,2]
mx = srgb.max(1); mn = srgb.min(1); v = mx; s = np.where(mx>1e-6, (mx-mn)/np.maximum(mx,1e-6), 0)
hue = np.zeros(nf)
d = np.maximum(mx-mn, 1e-6)
hr = ((g-b)/d) % 6; hg = (b-r)/d + 2; hb = (r-g)/d + 4
hue = np.where(mx==r, hr, np.where(mx==g, hg, hb))*60.0
fc = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1,3)
fn = np.empty(nf*3, np.float32); me.polygons.foreach_get('normal', fn); fn = fn.reshape(-1,3)
bpy.app.driver_namespace['seg_feat'] = dict(hue=hue, s=s, v=v, srgb=srgb, fc=fc, fn=fn)
# quick histograms to set thresholds
def stats(mask):
    return {"n": int(mask.sum()), "h": [round(float(np.percentile(hue[mask],q)),1) for q in (10,50,90)], "s": [round(float(np.percentile(s[mask],q)),2) for q in (10,50,90)], "v": [round(float(np.percentile(v[mask],q)),2) for q in (10,50,90)]}
z = fc[:,2]; x = fc[:,0]; y = fc[:,1]
result = {"tri": tri,
  "shoes": stats(z<0.06),
  "pants_mid": stats((z>0.2)&(z<0.5)),
  "kurti_belly": stats((z>0.85)&(z<1.0)&(y<-0.05)&(np.abs(x)<0.08)),
  "hand": stats(np.abs(x)>0.58),
  "face": stats((z>1.36)&(z<1.42)&(y<-0.1)&(np.abs(x)<0.04)),
  "hair_top": stats(z>1.57),
  "hair_back": stats((z>0.95)&(z<1.2)&(y>0.12)),
}

# ---- Face adjacency (Meera call 22) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
f = bpy.app.driver_namespace['seg_feat']
hue, s, v, srgb, fc = f['hue'], f['s'], f['v'], f['srgb'], f['fc']
nf = len(me.polygons)
x,y,z = fc[:,0], fc[:,1], fc[:,2]
# face adjacency via shared edges
loop_total = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', loop_total)
face_of_loop = np.repeat(np.arange(nf), loop_total)
le = np.empty(len(me.loops), np.int32); me.loops.foreach_get('edge_index', le)
order = np.argsort(le, kind='stable'); le_s = le[order]; fo = face_of_loop[order]
same = le_s[1:] == le_s[:-1]
adj = np.c_[fo[:-1][same], fo[1:][same]]
bpy.app.driver_namespace['face_adj'] = adj
# features: approximate Lab-ish via sRGB -> simple opponent space
R,G,B = srgb[:,0], srgb[:,1], srgb[:,2]
L = 0.299*R+0.587*G+0.114*B
A = R-G; Bb = (R+G)/2 - B
feat = np.c_[L*1.0, A*2.0, Bb*2.0]
def seed_stats(mask): return feat[mask].mean(0), int(mask.sum())
seeds = {
 'skin': (np.abs(x)>0.60) | ((z>1.36)&(z<1.42)&(y<-0.1)&(np.abs(x)<0.04)),
 'hair_dark': (z>1.45)&(y<-0.08)&(np.abs(x)>0.06)&(L<0.25),
 'hair_grey': (z>1.35)&(y>0.08),
 'kurti': ((z>0.85)&(z<1.0)&(y<-0.05)&(np.abs(x)<0.08)) | ((z>0.72)&(z<0.85)&(y>0.05)&(np.abs(x)<0.12)),
 'pants': (z>0.2)&(z<0.5),
 'shoes': (z<0.05)&(v>0.6),
}
cent = {k: seed_stats(m) for k,m in seeds.items()}
names = list(cent.keys())
C = np.array([cent[k][0] for k in names])
d = ((feat[:,None,:]-C[None,:,:])**2).sum(-1)
lab = np.argmin(d, 1)
cls_map = {'skin':0,'hair_dark':1,'hair_grey':1,'kurti':2,'pants':3,'shoes':4}
labels = np.array([cls_map[names[i]] for i in lab])
# spatial priors
labels[(labels==3)&(z>0.95)] = 1        # dark above waist is hair
labels[(labels==1)&(z<0.62)] = 3        # dark low is pants
labels[(labels==4)&(z>0.12)] = 2        # white high is kurti cream
labels[(z<0.09)&(v>0.55)] = 4
labels[(labels==0)&(z<0.62)] = 3
bpy.app.driver_namespace['seg_labels0'] = labels.copy()
result = {"centroids": {k:[[round(float(t),3) for t in cent[k][0]], cent[k][1]] for k in names}, "counts": np.bincount(labels, minlength=6).tolist(), "adj": int(len(adj))}

# ---- Rule labels (Meera call 24) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
f = bpy.app.driver_namespace['seg_feat']; adj = bpy.app.driver_namespace['face_adj']
hue, s, v, srgb, fc, fn = f['hue'], f['s'], f['v'], f['srgb'], f['fc'], f['fn']
nf = len(me.polygons); x,y,z = fc[:,0], fc[:,1], fc[:,2]
R,G,B = srgb[:,0], srgb[:,1], srgb[:,2]
L = 0.299*R+0.587*G+0.114*B; A = R-G; Bb=(R+G)/2-B
warm = (A > 0.10) & (s > 0.16) & (hue < 26) & (L > 0.45)        # skin-like
dark = L < 0.33
SKIN,HAIR,KURTI,PANTS,SHOES,EAR = 0,1,2,3,4,5
lab = np.full(nf, KURTI, np.int32)
head = z > 1.29
hands = (np.abs(x) > 0.50) & ~head
legs = (z <= 0.62) & (z > 0.09)
feet = z <= 0.09
torso = ~(head|hands|legs|feet)
# feet / legs
lab[feet] = np.where(v[feet] > 0.55, SHOES, PANTS)
lab[legs] = PANTS
# hands: skin unless cream/olive cuff
lab[hands] = np.where(warm[hands] | ((hue[hands]<24)&(s[hands]>0.12)&(L[hands]>0.55)), SKIN, KURTI)
# torso band: kurti olive/cream, skin neckline, dark -> hair or pants (decided by flood fill below)
tl = torso & warm & (z > 1.05)
lab[torso] = KURTI
lab[tl] = SKIN
tdark = torso & (dark | ((s < 0.2) & (hue < 36) & (L < 0.85) & (hue > 5) & ~((hue>36))))
# grey hair is low-sat warm-grey; kurti is olive hue>36
greyish = (s < 0.22) & (hue < 36)
lab[torso & (dark | greyish)] = HAIR
# head: front face oval is skin, otherwise hair unless warm skin (ears/neck)
face_oval = head & (y < -0.07) & (np.abs(x) < 0.085) & (z < 1.47) & (z > 1.315)
lab[head] = np.where(warm[head], SKIN, HAIR)
lab[face_oval] = SKIN
bpy.app.driver_namespace['seg_labels1'] = lab.copy()
bpy.app.driver_namespace['seg_aux'] = dict(L=L, A=A, warm=warm, dark=dark, greyish=greyish, face_oval=face_oval)
r = bpy.app.driver_namespace['seg_show'](lab, 'seg1')
result = {"counts": np.bincount(lab, minlength=6).tolist()}

# ---- Flood-grow regions + majority smoothing (Meera call 25) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
f = bpy.app.driver_namespace['seg_feat']; adj = bpy.app.driver_namespace['face_adj']; aux = bpy.app.driver_namespace['seg_aux']
hue, s, v, fc = f['hue'], f['s'], f['v'], f['fc']
L, A, warm, dark, greyish = aux['L'], aux['A'], aux['warm'], aux['dark'], aux['greyish']
nf = len(me.polygons); x,y,z = fc[:,0], fc[:,1], fc[:,2]
SKIN,HAIR,KURTI,PANTS,SHOES,EAR = 0,1,2,3,4,5
# CSR adjacency
a = np.r_[adj[:,0], adj[:,1]]; b = np.r_[adj[:,1], adj[:,0]]
order = np.argsort(a, kind='stable'); a = a[order]; b = b[order]
indptr = np.searchsorted(a, np.arange(nf+1))
def grow(seed, allowed, max_iter=4000):
    region = seed & allowed
    frontier = region.copy()
    for _ in range(max_iter):
        idx = np.nonzero(frontier)[0]
        if len(idx)==0: break
        # neighbours of frontier
        starts = indptr[idx]; ends = indptr[idx+1]
        nb = np.concatenate([b[s0:e0] for s0,e0 in zip(starts, ends)]) if len(idx) < 2000 else b[np.concatenate([np.arange(s0,e0) for s0,e0 in zip(starts,ends)])]
        nb = np.unique(nb)
        new = nb[allowed[nb] & ~region[nb]]
        frontier = np.zeros(nf, bool); frontier[new] = True
        region[new] = True
    return region
head = z > 1.29
face_core = (y < -0.075) & (np.abs(x) < 0.075) & (z < 1.47) & (z > 1.32)
haircol = (dark | greyish) & ~warm
hair_seed = head & (z > 1.52) & haircol
hair = grow(hair_seed, haircol & ~face_core & (z > 0.66))
legs = (z <= 0.62) & (z > 0.09)
pants_seed = legs
pants_allowed = (z < 0.80) & (dark | (L < 0.6) & (hue < 36)) & ~hair
pants = grow(pants_seed, pants_allowed | legs)
skin_seed = ((y < -0.075) & (np.abs(x) < 0.06) & (z < 1.45) & (z > 1.33)) | ((np.abs(x) > 0.62))
skin_allowed = (warm | ((L > 0.6) & (hue < 30) & (s > 0.08))) & ~hair & ~pants & ~((hue>=36)&(s>0.12))
skin = grow(skin_seed, skin_allowed | face_core)
lab = np.full(nf, KURTI, np.int32)
lab[pants] = PANTS
lab[(z <= 0.09) & (v > 0.55)] = SHOES
lab[(z <= 0.09) & ~(v > 0.55)] = PANTS
lab[skin] = SKIN
lab[hair] = HAIR
# majority smoothing (keeps classes, removes speckles)
for it in range(4):
    counts = np.zeros((nf, 6), np.int32)
    np.add.at(counts, (a, lab[b]), 1)
    counts[np.arange(nf), lab] += 1   # self vote
    maj = counts.argmax(1)
    strong = counts.max(1) >= 3
    lab = np.where(strong, maj, lab)
bpy.app.driver_namespace['seg_labels2'] = lab.copy()
bpy.app.driver_namespace['face_csr'] = (indptr, b)
r = bpy.app.driver_namespace['seg_show'](lab, 'seg2')
result = {"counts": np.bincount(lab, minlength=6).tolist(), "hair": int(hair.sum()), "skin": int(skin.sum()), "pants": int(pants.sum())}

# ---- Hair highlights / neck skin / neckline trim (Meera call 26) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
f = bpy.app.driver_namespace['seg_feat']; aux = bpy.app.driver_namespace['seg_aux']
indptr, nbr = bpy.app.driver_namespace['face_csr']
hue, s, v, fc = f['hue'], f['s'], f['v'], f['fc']
L, A, warm, dark, greyish = aux['L'], aux['A'], aux['warm'], aux['dark'], aux['greyish']
nf = len(me.polygons); x,y,z = fc[:,0], fc[:,1], fc[:,2]
SKIN,HAIR,KURTI,PANTS,SHOES,EAR = 0,1,2,3,4,5
lab = bpy.app.driver_namespace['seg_labels2'].copy()
def neighbours(idx):
    return nbr[np.concatenate([np.arange(indptr[i], indptr[i+1]) for i in idx])] if len(idx) else np.zeros(0, np.int64)
def grow(region, allowed, max_iter=3000):
    region = region.copy(); frontier = region.copy()
    for _ in range(max_iter):
        idx = np.nonzero(frontier)[0]
        if not len(idx): break
        nb = np.unique(neighbours(idx))
        new = nb[allowed[nb] & ~region[nb]]
        frontier = np.zeros(nf, bool); frontier[new] = True; region[new] = True
    return region
kurticol = (hue >= 34) & (hue <= 85) & (s >= 0.10)
head = z > 1.29
# 1) hair highlights: grow hair through non-kurti, non-warm faces above the hem
hair = grow(lab == HAIR, (~kurticol) & (~warm) & (z > 0.68) & ~((lab==SKIN)&(y<-0.075)&(np.abs(x)<0.075)&(z>1.32)&(z<1.47)))
# 2) shadowed neck/chest skin
neck_allowed = (z > 1.12) & (z < 1.40) & (hue < 32) & (s > 0.09) & (L > 0.28) & ~kurticol
skin = grow(lab == SKIN, (neck_allowed | (lab==SKIN)) & ~hair)
lab2 = lab.copy()
lab2[hair] = HAIR
lab2[skin & ~hair] = SKIN
# neckline trim: dark band right below skin on torso front -> kurti
trim = (lab2 == HAIR) & (z < 1.26) & (z > 1.08) & (y < -0.06) & (np.abs(x) < 0.12)
lab2[trim] = KURTI
# head cannot be kurti
lab2[head & (lab2 == KURTI)] = HAIR
a = np.repeat(np.arange(nf), np.diff(indptr))
for it in range(3):
    counts = np.zeros((nf, 6), np.int32); np.add.at(counts, (a, lab2[nbr]), 1); counts[np.arange(nf), lab2] += 1
    lab2 = np.where(counts.max(1) >= 3, counts.argmax(1), lab2)
bpy.app.driver_namespace['seg_labels3'] = lab2.copy()
show = bpy.app.driver_namespace['seg_show']
r = show(lab2, 'seg3')
# head close-ups
attr = me.color_attributes.get('debug_seg'); me.color_attributes.active_color = attr
sh = bpy.context.scene.display.shading; sh.color_type='VERTEX'; sh.light='FLAT'
shoot = bpy.app.driver_namespace['companion_shoot']
p1 = shoot('seg3_headF','FRONT', center=(0,0,1.36), ortho=0.42, res=(700,700))
p2 = shoot('seg3_headL','RIGHT', center=(0,0,1.36), ortho=0.42, res=(700,700))
p3 = shoot('seg3_headR','LEFT', center=(0,0,1.36), ortho=0.42, res=(700,700))
sh.color_type='TEXTURE'; sh.light='STUDIO'
result = {"counts": np.bincount(lab2, minlength=6).tolist()}

# ---- Earrings: box + two largest components (Meera call 31) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
f = bpy.app.driver_namespace['seg_feat']; aux = bpy.app.driver_namespace['seg_aux']
indptr, nbr = bpy.app.driver_namespace['face_csr']
fc = f['fc']; s = f['s']; L = aux['L']; warm = aux['warm']
x,y,z = fc[:,0], fc[:,1], fc[:,2]
nf = len(fc)
lab = bpy.app.driver_namespace['seg_labels3'].copy()
EAR = 5; HAIR = 1
boxL = (x > 0.045) & (x < 0.108)
boxR = (x < -0.045) & (x > -0.091)
cand = (boxL | boxR) & (z > 1.305) & (z < 1.415) & (y > -0.054) & (y < -0.006) & ~warm
lab[cand] = EAR
a = np.repeat(np.arange(nf), np.diff(indptr))
near = (np.abs(x) > 0.035) & (np.abs(x) < 0.125) & (z > 1.295) & (z < 1.425)
for it in range(2):
    counts = np.zeros((nf, 6), np.int32); np.add.at(counts, (a, lab[nbr]), 1); counts[np.arange(nf), lab] += 1
    new = np.where(counts.max(1) >= 3, counts.argmax(1), lab)
    lab = np.where(near, new, lab)
# keep only the two largest earring components (one per side)
em = lab == EAR
comp = np.where(em, np.arange(nf), -1)
for it in range(200):
    src = a[em[a] & em[nbr]]; dst = nbr[em[a] & em[nbr]]
    if not len(src): break
    newc = comp.copy(); np.minimum.at(newc, src, comp[dst])
    if (newc == comp).all(): break
    comp = newc
ids, cnt = np.unique(comp[em], return_counts=True)
keep = ids[np.argsort(-cnt)[:2]]
drop = em & ~np.isin(comp, keep)
lab[drop] = HAIR
bpy.app.driver_namespace['seg_labels4'] = lab.copy()
nfl = len(me.polygons)
loop_total = np.empty(nfl, np.int32); me.polygons.foreach_get('loop_total', loop_total)
PAL = np.array([[0.95,0.55,0.4],[0.05,0.05,0.9],[0.2,0.8,0.2],[0.9,0.1,0.1],[1,1,0],[0,1,1]], np.float32)
attr = me.color_attributes.get('debug_seg')
attr.data.foreach_set('color', np.repeat(np.c_[PAL[lab], np.ones(nfl, np.float32)], loop_total, axis=0).ravel())
sh = bpy.context.scene.display.shading; sh.color_type='VERTEX'; sh.light='FLAT'
shoot = bpy.app.driver_namespace['companion_shoot']
p1 = shoot('lab_ear_front2','FRONT', center=(0.0,0,1.36), ortho=0.30, res=(800,800))
sh.color_type='TEXTURE'; sh.light='STUDIO'
result = {"ear_faces": int((lab==EAR).sum()), "components": [int(c) for c in sorted(cnt, reverse=True)[:5]]}

# ---- Write material slots + companion_region face attribute (Meera call 32) ----
import bpy
import numpy as np
submit = bpy.app.driver_namespace['companion_submit']
bpy.app.driver_namespace['seg_final'] = bpy.app.driver_namespace['seg_labels4'].copy()
steps = [
 {"label": "Create per-part materials sharing the atlas", "code": """
import numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
src = bpy.data.materials['tripo_material_17193989-6838-461c-85b8-504bff43c0a6']
names = ['Meera_Skin','Meera_Hair','Meera_Kurti','Meera_Palazzo','Meera_Shoes','Meera_Earrings']
me.materials.clear()
for n in names:
    m = bpy.data.materials.get(n)
    if m is None:
        m = src.copy(); m.name = n
    me.materials.append(m)
lab = bpy.app.driver_namespace['seg_final']
me.polygons.foreach_set('material_index', lab.astype(np.int32))
me.update()
# keep labels as a face attribute for later stages
at = me.attributes.get('companion_region') or me.attributes.new('companion_region', 'INT', 'FACE')
at.data.foreach_set('value', lab.astype(np.int32))
"""},
 {"label": "Save checkpoint", "code": "bpy.ops.wm.save_mainfile()"},
]
r = submit("Material regions", steps)
result = {"job": r["job"]["state"]}
