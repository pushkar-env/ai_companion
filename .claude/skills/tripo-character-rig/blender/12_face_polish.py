# Face polish stage (2026-10-09, ADR-073). Needs the stage-09 face rig (shapes, lip walls, mouth parts);
# run it after stage 09/10 and export with stage 11 afterwards (Meera and Tara were re-exported).
# Verbatim working code from the Meera/Tara face-polish session in the live Blender 5.2.2 process.
# REFERENCE, NOT A ONE-SHOT SCRIPT: re-measure lip heights, interior placement and teeth arches per
# character (probe renders + numbers below) and run each write as a staged live.submit job.
#
# Why: the stage-09 shapes baked a jaw rotation into V_Open/V_Tight_O/... and the jaw skin weights
# jumped from ~0.2 to ~0.8 one vertex from each mouth corner, so every viseme opened as the same
# wide box/slit. This stage
#   1. re-solves the jaw weights (harmonic, CG) so the two lip halves meet at 0.5 at the corners and
#      blend smoothly toward the centre (upper 0, lower 1): the app's jaw bone now opens an oval;
#   2. rebuilds the mouth shapes as LIP-ONLY postures (no jaw inside): rounded/protruded oo/w,
#      spread ee, sealed p/b/m, lip-to-teeth f/v, flared sh, plus the ARKit mouth set
#      (jawOpen/mouthClose keep the 22 degree reference so the app can seal lips against the jaw gap);
#   3. fixes the mouth interior: tongue behind the lower incisors, lower incisors just behind the
#      uppers, teeth arches rebuilt wider with a deeper curve, tongue-only interior shapes;
#   4. paints a 256 px mouth atlas (same 4x4 colour-block UV centres + teeth strips) and maps the
#      teeth front/incisal faces onto it.
# Everything is derived from saved mesh data (companion_region attribute, region-7 lip walls,
# CC_Base_Head/JawRoot groups, mouth_part attribute), so it works after reopening/appending a file.
#
# How it was run: exec this module into a dict inside the live session
#   FP = {}; exec(open(path).read(), FP); bpy.app.driver_namespace['FP'] = FP
# then compute (fast, read-only) and submit the STEP_* strings below with live.submit; DRIVER at the
# end is the exact driver that did this per character (STEP_CHECKPOINT first, as a separate job).
#
# Recipes used (metres):
#   Tara : mouth_frame(D, hU0=0.0075, hL0=0.0095)            fields(D, RU=0.013, RL=0.020)
#          adjust_interior(tongue_len=.88, tongue_back=.0015, tongue_h=.72, tongue_drop=.0005, lt_up=.001, lt_back=.0008)
#          rebuild_teeth upper (half .024, depth .014), lower (half .0215, depth .0125)
#          teeth_texture(upper_bounds=(.31,.56,.78,.95), lower_bounds=(.24,.48,.70,.90), cavity=(.40,.12,.13))
#   Meera: mouth_frame(D, hU0=0.006, hL0=0.009, eU=0.8, eL=0.6) fields(D, RU=0.013, RL=0.020)
#          adjust_interior pass 1 (tongue_len=.94, tongue_back=0, tongue_h=1, tongue_drop=0, lt_up=.003, lt_back=.0008)
#          adjust_interior pass 2 (tongue_len=1, tongue_back=0, tongue_h=.8, tongue_drop=.0045, lt_up=0, lt_back=0)
#          rebuild_teeth upper (half .0205, depth .013), lower (half .0185, depth .012)
#          teeth_texture(upper_bounds=(.37,.66,.90), lower_bounds=(.27,.55,.80), cavity=(.40,.12,.13))
#          fix_seam_uvs(D, 'lo_chain', .45) (did NOT remove the white V marks: they are painted in the
#          Tripo texture and soften at Unity's 2K texture size)
#   Measure lip heights from the base colour along vertical lines through the mouth (lip vs skin
#   texel); measure interior placement with a midline (|x|<4 mm) cross-section plot of body, teeth,
#   tongue and cavity points; keep the tongue top below the lower incisal edge and behind them.
#
# Review: Workbench with BACKFACE CULLING ON (the inward-facing cavity front wall hides the teeth
# otherwise, unlike Unity), poses as the app drives them: shape weight + jawOpen = deg/22 (linear
# stand-in for the bone) + mouthClose = seal * deg/22. Then check in Unity: the app's bone jaw uses
# the vertex groups, not the jawOpen key.
#
# Export with the stage-11 FBX settings; Unity import uses blendshape normals = None (CharacterSetup):
# calculated shape normals flipped 180 degrees on the thin lip walls (orange lip "flakes").

import bpy, numpy as np, heapq

def ss(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)

def load(name):
    body = bpy.data.objects[name + '_Body']; me = body.data
    nv = len(me.vertices); nf = len(me.polygons)
    kb = me.shape_keys.key_blocks['Basis']
    P = np.empty(nv * 3); kb.data.foreach_get('co', P); P = P.reshape(-1, 3)
    ed = np.empty(len(me.edges) * 2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1, 2)
    lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
    ls = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', ls)
    fv = np.empty(len(me.loops), np.int32); me.polygons.foreach_get('vertices', fv)
    le = np.empty(len(me.loops), np.int32); me.loops.foreach_get('edge_index', le)
    reg = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
    fol = np.repeat(np.arange(nf), lt)
    rig = bpy.data.objects[name + '_Rig']
    pivot = np.array(rig.matrix_world @ rig.data.bones['CC_Base_JawRoot'].head_local)
    vg = {g.name: g.index for g in body.vertex_groups}
    J, H = vg['CC_Base_JawRoot'], vg['CC_Base_Head']
    wJ = np.zeros(nv); wH = np.zeros(nv); ninf = np.zeros(nv, np.int32)
    for v in me.vertices:
        ninf[v.index] = len(v.groups)
        for g in v.groups:
            if g.group == J: wJ[v.index] = g.weight
            elif g.group == H: wH[v.index] = g.weight
    return dict(name=name, body=body, me=me, nv=nv, nf=nf, P=P, ed=ed, lt=lt, ls=ls, fv=fv, le=le,
                reg=reg, fol=fol, pivot=pivot, J=J, H=H, wJ=wJ, wH=wH, ninf=ninf)

def mouth_frame(D, hU0, hL0, eU=0.8, eL=1.4):
    P, nv, reg, fv, fol, ls, lt, le, ed = D['P'], D['nv'], D['reg'], D['fv'], D['fol'], D['ls'], D['lt'], D['le'], D['ed']
    wall_v = np.zeros(nv, bool); wall_v[fv[reg[fol] == 7]] = True
    other_v = np.zeros(nv, bool); other_v[fv[reg[fol] != 7]] = True
    seam = wall_v & other_v
    # Lip side by flood fill: the cut seam separates the lips everywhere except at the two
    # corner vertices, so floods from the lip centres (kept inside the corners) never cross.
    seam_ids = np.nonzero(seam)[0]
    cand = seam_ids
    corners = np.array([cand[np.argmin(P[cand, 0])], cand[np.argmax(P[cand, 0])]])
    xc0 = (P[corners[0], 0] + P[corners[1], 0]) / 2; hw0 = (P[corners[1], 0] - P[corners[0], 0]) / 2
    sord = seam_ids[np.argsort(P[seam_ids, 0])]
    zs0 = np.interp(P[:, 0], P[sord, 0], P[sord, 2]); ys0 = np.interp(P[:, 0], P[sord, 0], P[sord, 1])
    u0 = (P[:, 0] - xc0) / hw0; dz0 = P[:, 2] - zs0
    band = (np.abs(u0) < 0.985) & (np.abs(dz0) < 0.014) & (P[:, 1] < ys0.min() + 0.03)
    band[corners] = False
    skin_e = np.zeros(len(ed), bool)
    nonwall_loops = reg[fol] != 7
    skin_e[le[nonwall_loops]] = True
    E = ed[skin_e & band[ed[:, 0]] & band[ed[:, 1]]]
    def flood(seed):
        reach = seed & band
        for _ in range(500):
            a = reach[E[:, 0]] & ~reach[E[:, 1]]; b = reach[E[:, 1]] & ~reach[E[:, 0]]
            if not (a.any() or b.any()): break
            reach[E[a, 1]] = True; reach[E[b, 0]] = True
        return reach
    front = P[:, 1] < ys0.min() + 0.012
    upR = flood((np.abs(u0) < 0.2) & (dz0 > 0.002) & (dz0 < 0.006) & front)
    loR = flood((np.abs(u0) < 0.2) & (dz0 < -0.002) & (dz0 > -0.006) & front)
    up_seam = seam & upR; lo_seam = seam & loR
    up_seam[corners] = lo_seam[corners] = True
    mixed = np.nonzero(seam & upR & loR)[0]
    orphan = seam & ~upR & ~loR
    orphan[corners] = False
    # leftover seam verts (outside the band): nearest classified seam vertex decides
    cls = np.nonzero((up_seam ^ lo_seam))[0]
    for v in np.nonzero(orphan)[0]:
        k = cls[np.argmin(np.linalg.norm(P[cls] - P[v], axis=1))]
        (up_seam if up_seam[k] else lo_seam)[v] = True
    vside = np.where(up_seam & ~lo_seam, 1, np.where(lo_seam & ~up_seam, -1, 0))
    wf = np.nonzero(reg == 7)[0]
    wall_side = np.zeros(nv)
    for f in wf:
        vs = fv[ls[f]:ls[f] + lt[f]]
        s_ = np.sign(sum(vside[v] for v in vs if seam[v]))
        for v in vs:
            if not seam[v]: wall_side[v] += s_
    up_all = np.r_[np.nonzero(up_seam)[0], np.nonzero(wall_v & ~seam & (wall_side > 0))[0]]
    lo_all = np.r_[np.nonzero(lo_seam)[0], np.nonzero(wall_v & ~seam & (wall_side < 0))[0]]
    D['wall_votes'] = dict(mixed=int(len(mixed)), orphans=int(orphan.sum()), up_seam=int(up_seam.sum()), lo_seam=int(lo_seam.sum()),
                           unresolved_wall=int((wall_v & ~seam & (wall_side == 0)).sum()))
    cR, cL = (corners if P[corners[0], 0] < P[corners[1], 0] else corners[::-1])
    up_chain = up_all[seam[up_all]]; lo_chain = lo_all[seam[lo_all]]
    up_chain = up_chain[np.argsort(P[up_chain, 0])]; lo_chain = lo_chain[np.argsort(P[lo_chain, 0])]
    up_wall = up_all[~seam[up_all]]; lo_wall = lo_all[~seam[lo_all]]
    xc = (P[cR, 0] + P[cL, 0]) / 2; hw = (P[cL, 0] - P[cR, 0]) / 2
    sx = P[up_chain, 0]; sy = P[up_chain, 1]; sz = P[up_chain, 2]
    X, Y, Z = P[:, 0], P[:, 1], P[:, 2]
    u = (X - xc) / hw
    zs = np.interp(X, sx, sz); ys = np.interp(X, sx, sy)
    dz = Z - zs
    # geodesic side: the split seam keeps the lips apart, so paths cross only at the corners
    adj = {}
    near = np.linalg.norm((P - np.array([xc, sy.min(), zs[up_chain].mean()])) * np.array([1, 0.8, 1]), axis=1) < 0.075
    for a, b in ed:
        if near[a] and near[b]:
            w = float(np.linalg.norm(P[a] - P[b]))
            adj.setdefault(int(a), []).append((int(b), w)); adj.setdefault(int(b), []).append((int(a), w))
    def geo(seeds, limit=0.07):
        dist = {int(s): 0.0 for s in seeds}; pq = [(0.0, int(s)) for s in seeds]; heapq.heapify(pq)
        while pq:
            d, a = heapq.heappop(pq)
            if d > dist.get(a, 1e9) or d > limit: continue
            for b, w in adj.get(a, ()):
                nd = d + w
                if nd < dist.get(b, 1e9): dist[b] = nd; heapq.heappush(pq, (nd, b))
        out = np.full(nv, 1e9)
        for k, v in dist.items(): out[k] = v
        return out
    inner_up = np.setdiff1d(up_chain, corners); inner_lo = np.setdiff1d(lo_chain, corners)
    gU = geo(inner_up); gL = geo(inner_lo)
    side_up = np.where((gU < 1e8) | (gL < 1e8), gU < gL, dz > 0)
    side_up[up_all] = True; side_up[lo_all] = False; side_up[corners] = True
    au = np.minimum(np.abs(u), 1.0)
    hU = hU0 * (1 - au ** 2) ** eU; hL = hL0 * (1 - au ** 2) ** eL
    skin = np.zeros(nv, bool); skin[fv[np.isin(reg[fol], (0, 7))]] = True
    ymin = sy.min()
    zone = skin & (Y < ymin + 0.075) & (np.abs(u) < 2.3) & (dz > -0.045) & (dz < 0.03)
    # smooth version for displacement weights: no hard depth/side cut that could tear the skin
    zoneF = skin * ss(ymin + 0.075, ymin + 0.045, Y) * ss(2.3, 1.95, np.abs(u)) * ss(-0.045, -0.035, dz) * ss(0.03, 0.022, dz)
    # parabola through the seam depth: lateral moves follow (and extrapolate) the face curvature
    qc = np.polyfit(sx, sy, 2)
    D.update(dict(seam=seam, up_chain=up_chain, lo_chain=lo_chain, up_wall=up_wall, lo_wall=lo_wall,
                  up_all=up_all, lo_all=lo_all, corners=corners, cR=int(cR), cL=int(cL), xc=xc, hw=hw,
                  sx=sx, sy=sy, sz=sz, u=u, zs=zs, ys=ys, dz=dz, side_up=side_up, gU=gU, gL=gL,
                  hU=hU, hL=hL, skin=skin, zone=zone, zoneF=zoneF, qc=qc, hU0=hU0, hL0=hL0))
    return D

def cg_solve(D, free, values, iters=3000, tol=1e-10):
    """Harmonic (uniform graph Laplacian) interpolation: values fixed outside `free`."""
    ed = D['ed']; nv = D['nv']
    a, b = ed[:, 0], ed[:, 1]
    deg = np.bincount(a, minlength=nv) + np.bincount(b, minlength=nv)
    fi = np.nonzero(free)[0]; idx = np.full(nv, -1); idx[fi] = np.arange(len(fi))
    both = free[a] & free[b]
    ea, eb = idx[a[both]], idx[b[both]]
    fa = free[a] & ~free[b]; fb = free[b] & ~free[a]
    rhs = np.bincount(idx[a[fa]], weights=values[b[fa]], minlength=len(fi)) + np.bincount(idx[b[fb]], weights=values[a[fb]], minlength=len(fi))
    dg = deg[fi].astype(float)
    def A(x):
        y = dg * x
        y -= np.bincount(ea, weights=x[eb], minlength=len(fi))
        y -= np.bincount(eb, weights=x[ea], minlength=len(fi))
        return y
    x = values[fi].copy(); r = rhs - A(x); p = r.copy(); rs = r @ r
    for it in range(iters):
        Ap = A(p); al = rs / (p @ Ap); x += al * p; r -= al * Ap; rn = r @ r
        if rn < tol: break
        p = r + (rn / rs) * p; rs = rn
    out = values.copy(); out[fi] = x
    return out, it, float(rn)

def jaw_field(D, e=0.6, zone_up=0.018, zone_dn=0.024, zone_u=1.75):
    """Corner-aware jaw weights: seam halves meet at 0.5 so the lips open as a lens."""
    nv, u, dz = D['nv'], D['u'], D['dz']
    tot = D['wJ'] + D['wH']
    old = np.where(tot > 1e-6, D['wJ'] / np.maximum(tot, 1e-6), 0.0)
    au = np.minimum(np.abs(u), 1.0)
    c = 1 - (1 - au ** 2) ** e
    vals = old.copy()
    fixed_seam = np.zeros(nv, bool)
    for chain, upper in ((D['up_chain'], True), (D['lo_chain'], False)):
        vals[chain] = 0.5 * c[chain] if upper else 1 - 0.5 * c[chain]
        fixed_seam[chain] = True
    vals[D['corners']] = 0.5
    # inner wall verts copy the nearest seam vertex of their own wall
    P = D['P']
    for wall, chain in ((D['up_wall'], D['up_chain']), (D['lo_wall'], D['lo_chain'])):
        for v in wall:
            k = chain[np.argmin(np.linalg.norm(P[chain] - P[v], axis=1))]
            vals[v] = vals[k]; fixed_seam[v] = True
    # lip centres stay rigid: lower lip and chin ride the jaw fully (teeth/cavity never poke
    # through), the upper lip stays on the head; only the corner thirds blend.
    up = D['side_up']; aur = np.abs(u)
    core1 = D['zone'] & ~up & (aur < 0.38) & (dz < -0.0012) & (dz > -zone_dn)
    core0 = D['zone'] & up & (aur < 0.38) & (dz > 0.0012) & (dz < zone_up)
    vals[core1] = 1.0; vals[core0] = 0.0
    free = D['zone'] & (np.abs(u) < zone_u) & (dz < zone_up) & (dz > -zone_dn) & ~fixed_seam & ~core1 & ~core0 & (tot > 0.5)
    w, it, res = cg_solve(D, free, vals)
    w = np.clip(w, 0, 1)
    D.update(dict(wj_old=old, wj=w, jaw_free=free, jaw_c=c))
    return dict(free=int(free.sum()), iters=it, residual=res)

def rot_about(P, pivot, deg):
    a = np.radians(deg); c, s = np.cos(a), np.sin(a)
    R = np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    return (P - pivot) @ R.T + pivot

def fields(D, RU, RL):
    """Soft region weights used by the lip primitives."""
    u, dz, up = D['u'], D['dz'], D['side_up']
    lo = ~up
    au = np.abs(u); auc = np.minimum(au, 1.0)
    hU, hL = D['hU'], D['hL']
    zone = D['zoneF']
    G = ss(1.8, 1.0, au)
    lens = (1 - auc ** 2) ** 0.6
    lat = ss(1.08, 0.95, au)          # vermilion exists only between the corners
    vU = up * ss(hU + 0.0025, np.maximum(hU - 0.0005, 0.0003), dz) * ss(-0.003, -0.001, dz) * lat * zone
    vL = lo * ss(hL + 0.0025, np.maximum(hL - 0.0005, 0.0003), -dz) * ss(0.003, 0.001, dz) * lat * zone
    sU = up * ss(RU, 0.0, np.abs(dz)) * zone
    sL = lo * ss(RL, 0.0, np.abs(dz)) * zone
    D.update(dict(G=G, lens=lens, vU=vU, vL=vL, sU=sU, sL=sL, au=au, auc=auc, lo=lo))

# ---- lip primitives (metres); forward is -Y ----
def _zero(D): return np.zeros((D['nv'], 3))

def part(D, up, lo, prof=0.9):
    d = _zero(D); L = D['lens'] ** prof * D['G']
    d[:, 2] += up * L * (0.7 * D['vU'] + 0.3 * D['sU'])
    d[:, 2] -= lo * L * (0.65 * D['vL'] + 0.35 * D['sL'])
    return d

def lipmass(D, skin=0.55):
    v = np.maximum(D['vU'], D['vL']); s = np.maximum(D['sU'], D['sL'])
    return v + skin * s * (1 - v)

def env(D, Ru=0.015, Rl=0.019, u0=0.95, u1=1.9):
    """Broad smooth perioral envelope: 1 on and around the lips, no step at the vermilion or corner."""
    dz = D['dz']
    e = np.where(dz >= 0, ss(Ru, 0.0, dz), ss(Rl, 0.0, -dz)) * ss(u1, u0, D['au']) * D['zoneF']
    return np.maximum(e, np.maximum(D['vU'], D['vL']))

def width(D, k):
    """k>0 narrows the mouth toward the centre, k<0 widens; points follow the seam's depth curve."""
    d = _zero(D); u = D['u']; hw = D['hw']
    W = env(D)
    a = np.abs(u); a0, sw = 0.8, 0.3
    soft = np.where(a < a0, a, a0 + sw * (1 - np.exp(-(a - a0) / sw)))
    dx = -k * hw * np.sign(u) * soft * W
    X = D['P'][:, 0]
    d[:, 0] = dx
    d[:, 1] = (np.polyval(D['qc'], X + dx) - np.polyval(D['qc'], X)) * W
    return d

def corners_z(D, dzc):
    d = _zero(D); C = ss(0.2, 1.0, D['au'])
    d[:, 2] = dzc * C * env(D, 0.016, 0.016, 1.0, 1.9)
    return d

def protrude(D, a, prof=0.35, skin=0.5):
    d = _zero(D); Pf = (1 - D['auc'] ** 2) ** prof
    v = np.maximum(D['vU'], D['vL'])
    W = v + skin * (1 - v) * env(D, 0.013, 0.016, 0.85, 1.6)
    d[:, 1] = -a * Pf * W * ss(1.6, 0.9, D['au'])
    return d

def part_center(D, up, lo, sigma=0.32):
    """Opening concentrated at the centre (rounded vowels): Gaussian across the mouth width."""
    d = _zero(D); g = np.exp(-(D['u'] / sigma) ** 2)
    d[:, 2] += up * g * (0.75 * D['vU'] + 0.25 * D['sU'])
    d[:, 2] -= lo * g * (0.7 * D['vL'] + 0.3 * D['sL'])
    return d

def roll(D, r_up, r_lo):
    """Positive rolls the vermilion in (back and toward the seam); negative everts it."""
    d = _zero(D); dz = D['dz']; L = D['lens'] ** 0.5 * D['G']
    tU = np.clip(dz / np.maximum(D['hU'], 1e-4), 0, 1); tL = np.clip(-dz / np.maximum(D['hL'], 1e-4), 0, 1)
    d[:, 1] += r_up * D['vU'] * L * (0.55 + 0.45 * (1 - tU)) + r_lo * D['vL'] * L * (0.55 + 0.45 * (1 - tL))
    d[:, 2] += -0.6 * r_up * D['vU'] * L * tU + 0.6 * r_lo * D['vL'] * L * tL
    return d

def thin(D, t):
    """Vertical lip compression toward the seam (t = fraction of lip height)."""
    d = _zero(D); dz = D['dz']; L = D['lens'] ** 0.4 * D['G']
    d[:, 2] = -t * dz * np.maximum(D['vU'], D['vL']) * L
    return d

def side(D, s):
    return ss(-0.35, 0.35, s * D['u'])

def jaw_rot(D, deg, w=None):
    w = D['wj'] if w is None else w
    return (rot_about(D['P'], D['pivot'], deg) - D['P']) * w[:, None]

def scale_mm(D):
    """Primitive amounts below are in mm for a 28 mm mouth half-width."""
    return D['hw'] / 0.028 * 0.001

def build_shapes(D):
    m = scale_mm(D); S = {}
    # CC visemes: lip posture only; the app's jaw bone supplies the opening.
    S['V_Open'] = part(D, 1.0 * m, 0.7 * m) + width(D, 0.05) + roll(D, -0.35 * m, -0.45 * m) + protrude(D, 0.4 * m)
    S['V_Explosive'] = thin(D, 0.22) + roll(D, 0.7 * m, 0.8 * m) + part(D, -0.25 * m, -0.45 * m) + protrude(D, 0.35 * m) + width(D, 0.03)
    S['V_Dental_Lip'] = (part(D, 1.9 * m, -2.0 * m, prof=0.45) + roll(D, 0.0, 1.6 * m) + width(D, -0.02)
                         + _lower_back(D, 3.4 * m))
    # rounded: narrow + protrude; the opening sits in the centre (the app seals the jaw gap with mouthClose)
    S['V_Tight_O'] = width(D, 0.36) + protrude(D, 4.4 * m, prof=0.25) + part_center(D, 1.7 * m, 1.5 * m, 0.30) + roll(D, -0.5 * m, -0.5 * m)
    S['V_Tight'] = thin(D, 0.12) + width(D, 0.08) + protrude(D, 0.9 * m) + roll(D, 0.3 * m, 0.3 * m)
    S['V_Wide'] = width(D, -0.11) + corners_z(D, 0.9 * m) + part(D, 0.8 * m, 0.6 * m, prof=0.7) + thin(D, 0.10) + _corners_back(D, 0.8 * m)
    S['V_Affricate'] = protrude(D, 3.4 * m, prof=0.2) + width(D, 0.17) + roll(D, -1.1 * m, -1.1 * m) + part(D, 1.8 * m, 1.5 * m, prof=0.4)
    S['V_Tongue_up'] = part(D, 0.6 * m, 0.4 * m) + width(D, 0.02)
    S['V_Tongue_Out'] = part(D, 0.8 * m, 0.9 * m) + width(D, 0.03)
    S['V_Tongue_Raise'] = part(D, 0.4 * m, 0.3 * m)
    # ARKit mouth set
    S['jawOpen'] = jaw_rot(D, 22)
    cU = 0.5 * D['jaw_c']
    lowerish = (~D['side_up']).astype(float)
    close_w = np.clip((D['wj'] - cU), 0, 1) * lowerish * np.maximum(D['vL'], ss(D['hL'] + 0.006, D['hL'], -D['dz']) * (~D['side_up']) * D['zoneF'] * ss(1.05, 0.9, D['au']))
    S['mouthClose'] = -(rot_about(D['P'], D['pivot'], 22) - D['P']) * close_w[:, None]
    S['jawForward'] = np.outer(D['wj'], [0, -0.0040, 0])
    S['jawLeft'] = np.outer(D['wj'], [0.0042, 0, 0])
    S['jawRight'] = np.outer(D['wj'], [-0.0042, 0, 0])
    S['mouthFunnel'] = protrude(D, 3.4 * m, prof=0.2) + width(D, 0.24) + roll(D, -1.4 * m, -1.4 * m) + part_center(D, 2.6 * m, 2.4 * m, 0.42)
    S['mouthPucker'] = width(D, 0.44) + protrude(D, 5.4 * m, prof=0.25) + roll(D, -0.3 * m, -0.3 * m) + thin(D, -0.08)
    S['mouthRollLower'] = roll(D, 0.0, 2.6 * m) + part(D, 0.0, -0.4 * m)
    S['mouthRollUpper'] = roll(D, 2.6 * m, 0.0) + part(D, -0.4 * m, 0.0)
    S['mouthShrugLower'] = part(D, 0.0, -1.8 * m) + protrude(D, 0.8 * m) + _chin_up(D, 1.4 * m)
    S['mouthShrugUpper'] = part(D, 1.4 * m, 0.0) + protrude(D, 0.6 * m)
    S['mouthLeft'] = _shift(D, 1, 4.5 * m)
    S['mouthRight'] = _shift(D, -1, 4.5 * m)
    for s, full in ((1, 'Left'), (-1, 'Right')):
        sw = side(D, s)[:, None]
        S['mouthPress' + full] = (thin(D, 0.2) + roll(D, 0.6 * m, 0.6 * m) + part(D, -0.2 * m, -0.3 * m)) * sw
        S['mouthLowerDown' + full] = part(D, 0.0, 2.8 * m, prof=0.6) * sw
        S['mouthUpperUp' + full] = part(D, 2.6 * m, 0.0, prof=0.6) * sw
        S['mouthStretch' + full] = (width(D, -0.10) + corners_z(D, -0.9 * m) + thin(D, 0.08) + _corners_back(D, 0.7 * m)) * sw
    return S

def mouth_data(D):
    mo = bpy.data.objects[D['name'] + '_Mouth']; mm = mo.data
    mv = len(mm.vertices)
    M = np.empty(mv * 3); mm.shape_keys.key_blocks['Basis'].data.foreach_get('co', M); M = M.reshape(-1, 3)
    part = np.empty(len(mm.polygons), np.int32); mm.attributes['mouth_part'].data.foreach_get('value', part)
    mlt = np.empty(len(mm.polygons), np.int32); mm.polygons.foreach_get('loop_total', mlt)
    mfv = np.empty(len(mm.loops), np.int32); mm.polygons.foreach_get('vertices', mfv)
    vpart = np.full(mv, -1); vpart[mfv] = np.repeat(part, mlt)
    gj = mo.vertex_groups['CC_Base_JawRoot'].index
    mw = np.zeros(mv)
    for v in mm.vertices:
        for g in v.groups:
            if g.group == gj: mw[v.index] = g.weight
    D.update(dict(M=M, vpart=vpart, mouth_w=mw, mouth_obj=mo))
    return D

def teeth_arches(D):
    """Split the teeth part into upper and lower arches (connected components)."""
    mo = D['mouth_obj']; mm = mo.data; vpart = D['vpart']
    ed = np.empty(len(mm.edges) * 2, np.int32); mm.edges.foreach_get('vertices', ed); ed = ed.reshape(-1, 2)
    t = vpart == 0
    lab = np.arange(len(vpart))
    for _ in range(200):
        e = ed[t[ed[:, 0]] & t[ed[:, 1]]]
        m = np.minimum(lab[e[:, 0]], lab[e[:, 1]])
        new = lab.copy(); np.minimum.at(new, e[:, 0], m); np.minimum.at(new, e[:, 1], m)
        if (new == lab).all(): break
        lab = new
    comps = np.unique(lab[t])
    assert len(comps) == 2, 'expected two teeth arches'
    zs = [D['M'][t & (lab == c), 2].mean() for c in comps]
    lower = t & (lab == comps[int(np.argmin(zs))]); upper = t & ~lower
    D.update(dict(teeth_upper=upper, teeth_lower=lower))
    return upper, lower

def adjust_interior(D, tongue_len=0.88, tongue_back=0.0015, tongue_h=0.72, tongue_drop=0.0005, lt_up=0.001, lt_back=0.0008):
    """New interior basis: tongue rests behind the lower incisors; lower incisors sit just behind the uppers."""
    M = D['M'].copy(); vpart = D['vpart']
    up_t, lo_t = teeth_arches(D)
    tg = vpart == 2
    yb = M[tg, 1].max(); zt = M[tg, 2].max()
    M[tg, 1] = yb + (M[tg, 1] - yb) * tongue_len + tongue_back
    M[tg, 2] = zt - tongue_drop - (zt - M[tg, 2]) * tongue_h
    M[lo_t, 2] += lt_up; M[lo_t, 1] += lt_back
    D['M_old'] = D['M']; D['M'] = M
    return dict(tongue_tip_y=float(M[tg, 1].min()), tongue_bottom_z=float(M[tg, 2].min()),
                lower_teeth_top=float(M[lo_t, 2].max()), lower_teeth_front=float(M[lo_t, 1].min()),
                upper_teeth_back=float(M[up_t, 1].max()), upper_teeth_bottom=float(M[up_t, 2].min()))

def fix_seam_uvs(D, chain_key='lo_chain', amount=0.45):
    """Pull lip-seam UVs into the painted lip so they stop sampling the light mouth line."""
    me = D['me']; fv, ls, lt, reg = D['fv'], D['ls'], D['lt'], D['reg']
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
    chain = set(int(v) for v in D[chain_key]) - set(int(c) for c in D['corners'])
    seam = D['seam']; n = 0
    for f in np.nonzero(reg != 7)[0]:
        lo, cnt = ls[f], lt[f]
        vs = fv[lo:lo + cnt]
        hit = [i for i in range(cnt) if int(vs[i]) in chain]
        if not hit: continue
        inner = [i for i in range(cnt) if not seam[vs[i]]]
        if not inner: continue
        target = uv[lo + np.array(inner)].mean(0)
        for i in hit:
            uv[lo + i] += amount * (target - uv[lo + i]); n += 1
    me.uv_layers.active.data.foreach_set('uv', uv.ravel()); me.update()
    return n

def teeth_texture(D, size=256, upper_bounds=(0.38, 0.68, 0.97), lower_bounds=(0.27, 0.55, 0.82), cavity=None):
    """256 px mouth atlas: the original 4x4 colour blocks (same UV centres) plus painted
    upper (v 0.75-1) and lower (v 0.5-0.75) teeth strips with incisors, gum line and shading."""
    img = bpy.data.images[D['name'] + '_Mouth']
    w0, h0 = img.size
    px0 = np.array(img.pixels[:], np.float32).reshape(h0, w0, 4)
    blocks = {}
    for by in range(2):
        for bx in range(4):
            cy = int((by + 0.5) / 4 * h0); cx = int((bx + 0.5) / 4 * w0)
            blocks[(bx, by)] = px0[cy, cx, :3].copy()
    S = size; B = S // 4
    tex = np.zeros((S, S, 4), np.float32); tex[..., 3] = 1
    if cavity is not None:
        blocks[(3, 0)] = np.array(cavity, np.float32)
    cav = blocks[(3, 0)]
    tex[..., :3] = cav
    for (bx, by), c in blocks.items():
        tex[by * B:(by + 1) * B, bx * B:(bx + 1) * B, :3] = c
    enamel = np.array([0.95, 0.93, 0.88]); cervical = np.array([0.86, 0.81, 0.72]); edge = np.array([0.80, 0.83, 0.86])
    gum = np.array([0.70, 0.36, 0.38]); gap = np.array([0.30, 0.16, 0.16])
    def strip(r0, bounds, upper):
        rows = np.arange(B)
        t = rows / (B - 1)                    # 0 at the strip's bottom row
        tt = t if upper else 1 - t            # 0 at the incisal edge, 1 at the gum
        cols = np.arange(S); xr = (cols + 0.5) / S * 2 - 1     # -1 .. 1 across the arch chord
        ax = np.abs(xr)
        e = np.concatenate([[0.0], bounds, [1.2]])
        k = np.clip(np.searchsorted(e, ax) - 1, 0, len(e) - 2)
        s = (ax - e[k]) / (e[k + 1] - e[k])   # 0..1 within the tooth
        T, Sx = np.meshgrid(tt, s, indexing='ij'); AX = np.meshgrid(tt, ax, indexing='ij')[1]
        col = enamel[None, None] * (1 - T[..., None] * 0.0)
        col = col + (cervical - enamel)[None, None] * np.clip((T - 0.35) / 0.5, 0, 1)[..., None]
        col = col + (edge - col) * np.clip((0.14 - T) / 0.14, 0, 1)[..., None] * 0.8
        # interproximal lines and incisal embrasures
        dist = np.minimum(Sx, 1 - Sx) * (e[k + 1] - e[k])[None, :] * S / 2   # px to the nearest tooth edge
        line = np.exp(-(dist / 0.95) ** 2) * (0.45 + 0.55 * np.clip((T - 0.45) / 0.4, 0, 1))
        notch = np.exp(-(dist / (1.0 + 7 * np.clip(0.16 - T, 0, 1))) ** 2) * (T < 0.16)
        col = col * (1 - 0.42 * line[..., None]) + gap[None, None] * 0.42 * line[..., None]
        col = col * (1 - 0.8 * notch[..., None]) + cav[None, None] * 0.8 * notch[..., None]
        # scalloped gum margin: taller crown mid-tooth, papilla between teeth
        margin = 0.80 + 0.10 * np.sin(np.pi * Sx)
        g = np.clip((T - margin) / 0.05, 0, 1)
        col = col * (1 - g[..., None]) + gum[None, None] * g[..., None]
        # teeth further round the arch sit deeper in the mouth: darker
        col *= (1 - 0.32 * AX ** 1.6)[..., None]
        tex[r0:r0 + B, :, :3] = col
    strip(3 * B, np.array(upper_bounds), True)        # upper: central, lateral, canine (+ premolars)
    strip(2 * B, np.array(lower_bounds), False)       # lower: smaller incisors
    img.scale(S, S); img.pixels.foreach_set(tex.ravel()); img.update()
    D['mouth_tex'] = tex
    return img

def teeth_uvs(D):
    """Front and incisal faces of each arch map across its painted strip."""
    mo = D['mouth_obj']; mm = mo.data; M = D['M']
    up_t, lo_t = D.get('teeth_upper'), D.get('teeth_lower')
    if up_t is None: up_t, lo_t = teeth_arches(D)
    nf = len(mm.polygons)
    lt = np.empty(nf, np.int32); mm.polygons.foreach_get('loop_total', lt)
    ls = np.empty(nf, np.int32); mm.polygons.foreach_get('loop_start', ls)
    fv = np.empty(len(mm.loops), np.int32); mm.polygons.foreach_get('vertices', fv)
    uv = np.empty(len(mm.loops) * 2, np.float32); mm.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
    nrm = np.array([p.normal[:] for p in mm.polygons])
    n_set = 0
    for arch, v0, upper in ((up_t, 0.75, True), (lo_t, 0.50, False)):
        xs = M[arch, 0]; zs = M[arch, 2]
        x0, x1, z0, z1 = xs.min(), xs.max(), zs.min(), zs.max()
        m = 2.0 / 256
        for f in range(nf):
            vs = fv[ls[f]:ls[f] + lt[f]]
            if not arch[vs].all(): continue
            ny, nz = nrm[f, 1], nrm[f, 2]
            front = ny < -0.45
            incisal = (nz < -0.6) if upper else (nz > 0.6)
            if not (front or incisal): continue
            for li in range(ls[f], ls[f] + lt[f]):
                p = M[fv[li]]
                uu = (p[0] - x0) / (x1 - x0)
                if front:
                    vv = (p[2] - z0) / (z1 - z0)
                else:
                    vv = 0.0 if upper else 1.0
                uv[li] = (m + uu * (1 - 2 * m), v0 + m + vv * (0.25 - 2 * m))
            n_set += 1
    mm.uv_layers.active.data.foreach_set('uv', uv.ravel())
    mm.update()
    return n_set

def rebuild_teeth(D, arch_key, half, depth):
    """Re-place one teeth arch parametrically: its rings of 4 verts (front/back x top/bottom) along
    y = y0 + k x^2, spanning +-half, with the outer ring `depth` metres behind the centre.
    Ring heights (incisal curve) and thickness are kept; end caps follow their rings."""
    M = D['M']; arch = np.nonzero(D[arch_key])[0]
    order = arch[np.argsort(M[arch, 0], kind='stable')]
    assert len(order) % 4 == 0, 'teeth arch is not built from 4-vertex rings'
    rings = order.reshape(-1, 4); n = len(rings)
    centre = rings[np.argmin(np.abs(M[rings, 0].mean(1)))]
    y0 = np.sort(M[centre, 1])[:2].mean()
    k = depth / half ** 2
    for i, ring in enumerate(rings):
        ys = M[ring, 1]; front = ring[np.argsort(ys)[:2]]; back = ring[np.argsort(ys)[2:]]
        thick = M[back, 1].mean() - M[front, 1].mean()
        x = -half + 2 * half * i / (n - 1); yf = y0 + k * x * x
        M[ring, 0] = x; M[front, 1] = yf; M[back, 1] = yf + thick
    return dict(rings=int(n), y0_mm=round(float(y0) * 1000, 1), k=round(float(k), 1))

def build_mouth_shapes(D):
    """Tongue-only motion for the CC visemes (the jaw bone carries teeth and tongue)."""
    M, vpart = D['M'], D['vpart']; m = scale_mm(D)
    tg = vpart == 2
    yb, yf = M[tg, 1].max(), M[tg, 1].min()          # back (larger y) and tip (most forward)
    t = np.where(tg, (yb - M[:, 1]) / max(yb - yf, 1e-4), 0.0)
    tip = lambda a, b: tg * ss(a, b, t)
    def vec(w, v): return np.outer(w, np.array(v) * m)
    MS = {}
    MS['V_Open'] = vec(tg * 1.0, [0, 0.5, -1.0])
    MS['V_Dental_Lip'] = vec(tip(0.4, 1.0), [0, 0.4, -0.4])
    MS['V_Tight_O'] = vec(tg * 1.0, [0, 1.0, 0.0]) + vec(tg * ss(0.7, 0.2, t), [0, 0, 1.0])
    MS['V_Tight'] = vec(tip(0.5, 1.0), [0, -0.4, 1.2])
    MS['V_Wide'] = vec(tip(0.3, 1.0), [0, -0.8, 0.9])
    MS['V_Affricate'] = vec(tip(0.4, 1.0), [0, 0.6, 2.0])
    MS['V_Tongue_up'] = vec(tip(0.45, 1.0), [0, -2.0, 6.5])
    MS['V_Tongue_Out'] = vec(tip(0.35, 1.0), [0, -5.5, 1.8])
    MS['V_Tongue_Raise'] = vec(tg * ss(0.6, 0.15, t), [0, 0, 5.5])
    mw = D['mouth_w']
    MS['jawOpen'] = (rot_about(M, D['pivot'], 22) - M) * mw[:, None]
    MS['tongueOut'] = (rot_about(M, D['pivot'], 6) - M) * mw[:, None] + vec(tip(0.3, 1.0), [0, -12.0, -1.0])
    return MS

def _lower_back(D, b):
    d = _zero(D); L = D['lens'] ** 0.5 * D['G']
    d[:, 1] = b * L * (0.8 * D['vL'] + 0.2 * D['sL'])
    return d

def _corners_back(D, b):
    d = _zero(D); C = ss(0.3, 1.0, D['au']) * D['G']
    W = np.maximum(np.maximum(D['vU'], D['vL']), 0.7 * np.maximum(D['sU'], D['sL']))
    d[:, 1] = b * C * W
    return d

def _chin_up(D, a):
    d = _zero(D); lo = (~D['side_up']).astype(float) * D['zoneF']
    w = lo * ss(-0.002, -0.012, D['dz']) * ss(0.032, 0.010, -D['dz']) * ss(1.4, 0.5, D['au'])
    d[:, 2] = a * w; d[:, 1] = -0.4 * a * w
    return d

def _shift(D, s, a):
    d = _zero(D); W = np.maximum(lipmass(D, 0.6), 0) * ss(2.0, 1.0, D['au'])
    d[:, 0] = s * a * W
    return d


# ---- Write steps (each a live.submit step; bpy is pre-imported) ----
STEP_CHECKPOINT = r'''
import os
ck = bpy.app.driver_namespace['FP_checkpoint']
os.makedirs(os.path.dirname(ck), exist_ok=True)
if not os.path.exists(ck):
    bpy.ops.wm.save_as_mainfile(filepath=ck, copy=True)
'''

STEP_JAW_WEIGHTS = r'''
import numpy as np
D = bpy.app.driver_namespace['FPD']; body = D['body']
gJ = body.vertex_groups['CC_Base_JawRoot']; gH = body.vertex_groups['CC_Base_Head']
tot = D['wJ'] + D['wH']
changed = np.nonzero((np.abs(D['wj'] - D['wj_old']) > 1e-4) & (tot > 0.5))[0]
for i in changed:
    i = int(i); t = float(tot[i]); w = float(D['wj'][i])
    if w * t > 1e-5: gJ.add([i], w * t, 'REPLACE')
    else: gJ.remove([i])
    if (1 - w) * t > 1e-5: gH.add([i], (1 - w) * t, 'REPLACE')
    else: gH.remove([i])
bpy.app.driver_namespace['FP_jaw_changed'] = len(changed)
'''

STEP_BODY_KEYS = r'''
import numpy as np
D = bpy.app.driver_namespace['FPD']; S = bpy.app.driver_namespace['FPS']
kbs = D['me'].shape_keys.key_blocks; P = D['P']
missing = [n for n in S if n not in kbs]
assert not missing, missing
for name, d in S.items():
    kbs[name].data.foreach_set('co', (P + d).ravel().astype(np.float32))
D['me'].update()
'''

STEP_MOUTH_KEYS = r'''
import numpy as np
D = bpy.app.driver_namespace['FPD']; MS = bpy.app.driver_namespace['FPMS']
mo = bpy.data.objects[D['name'] + '_Mouth']; kbs = mo.data.shape_keys.key_blocks
M = D['M']                      # (possibly adjusted) interior basis
mo.data.vertices.foreach_set('co', M.ravel().astype(np.float32))
kbs['Basis'].data.foreach_set('co', M.ravel().astype(np.float32))
missing = [k.name for k in kbs[1:] if k.name not in MS]
assert not missing, missing
for name, d in MS.items():
    kbs[name].data.foreach_set('co', (M + d).ravel().astype(np.float32))
mo.data.update()
if bpy.app.driver_namespace.get('FP_set_interior_flag'): mo['fp_interior_v1'] = 1
'''

STEP_TEETH = r'''
ns = bpy.app.driver_namespace
D = ns['FPD']; FP = ns['FP']
img = FP['teeth_texture'](D, **ns.get('FP_teeth_args', {}))
n = FP['teeth_uvs'](D)
if ns.get('FP_mouth_tex_path'):
    img.filepath_raw = ns['FP_mouth_tex_path']; img.file_format = 'PNG'
img.save()
ns['FP_teeth_faces'] = n
'''


# ---- Review helpers (Workbench close-ups; the session also defined these as driver_namespace fns) ----
REVIEW = r'''
import bpy, mathutils, os
NAME = bpy.app.driver_namespace.get('FP_name', 'Tara')
OUT = bpy.app.driver_namespace.get('FP_review', '//review/face-polish')
scene = bpy.context.scene
cam = bpy.data.objects['ReviewCam']; cd = cam.data
objs = [bpy.data.objects[NAME + s] for s in ('_Body', '_Mouth', '_Eyes')]
def set_shapes(vals):
    for o in objs:
        if not o.data.shape_keys: continue
        for kb in o.data.shape_keys.key_blocks[1:]: kb.value = vals.get(kb.name, 0.0)
    bpy.context.view_layer.update()
def shoot(name, center, direction=(0, -3, 0), ortho=0.09, res=(640, 640)):
    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading; sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_backface_culling = True
    scene.render.resolution_x, scene.render.resolution_y = res; scene.render.resolution_percentage = 100
    c = mathutils.Vector(center); cam.location = c + mathutils.Vector(direction)
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cd.type = 'ORTHO'; cd.ortho_scale = ortho; cd.clip_end = 20; scene.camera = cam
    path = bpy.path.abspath(OUT) + '/' + name + '.png'; os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path; bpy.ops.render.render(write_still=True); return path
J = lambda deg: deg / 22.0
POSES = [("neutral", {}), ("aa", {"V_Open": .8, "jawOpen": J(9)}), ("ee", {"V_Wide": .9, "jawOpen": J(3)}),
         ("oo", {"V_Tight_O": 1, "jawOpen": J(3.5), "mouthClose": .85 * J(3.5)}),
         ("pbm", {"V_Explosive": 1, "jawOpen": J(1.0), "mouthClose": J(1.0)}),
         ("fv", {"V_Dental_Lip": 1, "jawOpen": J(1.5), "mouthClose": .5 * J(1.5)}),
         ("sh", {"V_Affricate": 1, "jawOpen": J(2)}), ("th", {"V_Tongue_Out": 1, "jawOpen": J(3.5)}),
         ("pucker", {"mouthPucker": 1}), ("smile-talk", {"Mouth_Corner_Pull_L": .5, "Mouth_Corner_Pull_R": .5, "V_Open": .6, "jawOpen": J(7)})]
'''


# ---- Driver used per character (execute_blender_code in the live session) ----
# Set in driver_namespace first: FP_name, FP_params = dict(hU0=, hL0=, eU=, eL=, RU=, RL=, interior=dict(...) or None),
# FP_write_jaw (True on the first run only), FP_title. Run once per tuning round; look at REVIEW renders in between.
DRIVER = r"""
import bpy, numpy as np, uuid
import codex_live_workflow as live
base = r'<folder holding this file and the STEP strings>'
ns = bpy.app.driver_namespace
FP = {}; exec(open(base + r'\12_face_polish.py').read(), FP)
W = FP                                 # the STEP_* strings live in this module
name = ns['FP_name']; prm = ns['FP_params']
D = FP['load'](name)
FP['mouth_frame'](D, prm['hU0'], prm['hL0'], prm.get('eU', 0.8), prm.get('eL', 1.4))
jr = FP['jaw_field'](D)
FP['fields'](D, prm['RU'], prm['RL'])
S = FP['build_shapes'](D)
FP['mouth_data'](D)
interior = None
ns['FP_set_interior_flag'] = False
if prm.get('interior') is not None and not D['mouth_obj'].get('fp_interior_v1'):
    interior = FP['adjust_interior'](D, **prm['interior']); ns['FP_set_interior_flag'] = True
MS = FP['build_mouth_shapes'](D)
ns['FP'] = FP; ns['FPD'] = D; ns['FPS'] = S; ns['FPMS'] = MS
steps = []
if ns.get('FP_write_jaw'):
    steps.append({'label': 'Corner-aware jaw skin weights', 'code': W['STEP_JAW_WEIGHTS']})
steps += [{'label': 'Lip-only viseme and ARKit mouth shapes', 'code': W['STEP_BODY_KEYS']},
          {'label': 'Tongue-only mouth interior shapes', 'code': W['STEP_MOUTH_KEYS']}]
st = live.status()
r = live.submit(title=ns.get('FP_title', name + ' face polish'), expected_session=st['session'], job_id=str(uuid.uuid4()),
                interval=0.3, start_paused=False, steps=steps)
result = {"job": r['job']['state'], "jaw": jr, "interior": interior, "max_mm": {k: round(float(np.linalg.norm(v, axis=1).max()) * 1000, 1) for k, v in S.items()}}
"""
# Meera's second interior pass (tongue lowered 4.5 mm more) reused FPD from the run above, guarded by its own flag:
#   FP['mouth_data'](D); FP['adjust_interior'](D, tongue_len=1.0, tongue_back=0.0, tongue_h=0.8, tongue_drop=0.0045,
#   lt_up=0.0, lt_back=0.0); ns['FPMS'] = FP['build_mouth_shapes'](D); submit STEP_MOUTH_KEYS plus
#   "bpy.data.objects['Meera_Mouth']['fp_interior_v2'] = 1".
# Teeth: set ns['FP_teeth_args'] (bounds/cavity above) and ns['FP_mouth_tex_path'] (the export Textures/<Name>_Mouth.png),
#   rebuild both arches (rebuild_teeth on D['M'] after mouth_data + teeth_arches), submit STEP_MOUTH_KEYS then STEP_TEETH.
# Never call a superseded uniform teeth widening: scaling the arch in x pushed Meera's outer teeth through the cheek;
#   rebuild_teeth places a deeper curve instead. Check a top-down mouth render and a 3/4 render after every teeth change.
