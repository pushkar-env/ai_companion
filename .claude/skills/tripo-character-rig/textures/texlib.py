# Skill stage 13 (modular wardrobe), from the Arjun build 2026-10-10. Arjun-specific constants (cut lines,
# opening angles, button heights, waistband details, colours) are templates: re-measure for a new garment.
# Shared helpers for painting garment atlases from exported Blender mesh data.
import numpy as np
from PIL import Image
from scipy import ndimage


def load_mesh(path):
    d = np.load(path, allow_pickle=False)
    return {k: d[k] for k in d.files}


def srgb_to_lin(c):
    c = np.asarray(c, np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def rasterize(m, newuv, res):
    """Per-texel triangle id and barycentric weights for the new layout (row 0 = top, v up)."""
    H = W = res
    tid = np.full((H, W), -1, np.int32)
    bw = np.zeros((H, W, 3), np.float32)
    tl = m['tl']
    P = newuv[tl] * np.array([W, H])           # (T,3,2) in pixel units, y up
    P[:, :, 1] = H - P[:, :, 1]                # y down
    for t in range(len(tl)):
        p = P[t]
        x0, y0 = np.floor(p.min(0)).astype(int) - 1
        x1, y1 = np.ceil(p.max(0)).astype(int) + 1
        x0 = max(x0, 0); y0 = max(y0, 0); x1 = min(x1, W - 1); y1 = min(y1, H - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs = np.arange(x0, x1 + 1) + 0.5
        ys = np.arange(y0, y1 + 1) + 0.5
        gx, gy = np.meshgrid(xs, ys)
        (ax, ay), (bx, by), (cx, cy) = p
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
        w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
        w2 = 1 - w0 - w1
        eps = 0.02
        inside = (w0 >= -eps) & (w1 >= -eps) & (w2 >= -eps)
        if not inside.any():
            continue
        sub_t = tid[y0:y1 + 1, x0:x1 + 1]
        sub_b = bw[y0:y1 + 1, x0:x1 + 1]
        # do not let edge-tolerant samples overwrite pixels that are strictly inside another triangle
        strict = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        write = inside & ((sub_t < 0) | strict)
        sub_t[write] = t
        sub_b[write] = np.stack([w0, w1, w2], -1)[write]
    return tid, bw


def interp_vertex(m, tid, bw, attr):
    """Barycentric interpolation of a per-vertex attribute (N,k) at every covered texel."""
    mask = tid >= 0
    out = np.zeros(tid.shape + attr.shape[1:], np.float32)
    t = tid[mask]
    v = m['lv'][m['tl'][t]]                    # (n,3) vertex ids
    w = np.clip(bw[mask], 0, 1)
    w = w / np.maximum(w.sum(-1, keepdims=True), 1e-9)
    a = attr[v]                                # (n,3,k)
    if a.ndim == 2:
        out[mask] = (a * w).sum(-1)
    else:
        out[mask] = (a * w[..., None]).sum(1)
    return out


def interp_loop(m, tid, bw, attr):
    mask = tid >= 0
    out = np.zeros(tid.shape + attr.shape[1:], np.float32)
    t = tid[mask]
    L = m['tl'][t]
    w = np.clip(bw[mask], 0, 1)
    w = w / np.maximum(w.sum(-1, keepdims=True), 1e-9)
    out[mask] = (attr[L] * w[..., None]).sum(1)
    return out


def sample_bilinear(img, uv):
    """img (H,W,C) float, uv (...,2) with v up."""
    H, W = img.shape[:2]
    x = np.clip(uv[..., 0] * W - 0.5, 0, W - 1.001)
    y = np.clip((1 - uv[..., 1]) * H - 0.5, 0, H - 1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = (x - x0)[..., None]; fy = (y - y0)[..., None]
    a = img[y0, x0]; b = img[y0, x0 + 1]; c = img[y0 + 1, x0]; d = img[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def _hash3(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
    h = (h ^ (h >> 13)) * 1274126177
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF).astype(np.float64) / float(0xFFFFFF)


def value_noise3(p, seed=0):
    """Smooth value noise in [0,1] at points p (...,3) in lattice units."""
    p = np.asarray(p, np.float64)
    i = np.floor(p).astype(np.int64)
    f = p - i
    f = f * f * (3 - 2 * f)
    out = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (f[..., 0] if dx else 1 - f[..., 0]) * (f[..., 1] if dy else 1 - f[..., 1]) * (f[..., 2] if dz else 1 - f[..., 2])
                out = out + w * _hash3(i[..., 0] + dx, i[..., 1] + dy, i[..., 2] + dz, seed)
    return out


def fbm(p, octaves=3, seed=0):
    acc = 0.0; amp = 0.5; norm = 0.0
    for o in range(octaves):
        acc = acc + amp * value_noise3(p * (2 ** o), seed + 17 * o)
        norm += amp
        amp *= 0.5
    return acc / norm


def dilate(img, mask, iterations=None):
    """Fill uncovered texels with the nearest covered colour (prevents mip bleeding at seams)."""
    idx = ndimage.distance_transform_edt(~mask, return_distances=False, return_indices=True)
    return img[idx[0], idx[1]]


def height_to_normal(h, strength):
    """Tangent-space normal perturbation (OpenGL, +Y up) from a height map in texel units."""
    gy, gx = np.gradient(h)
    nx = -gx * strength
    ny = gy * strength          # image rows grow downwards, v grows upwards
    return nx, ny


def save_rgb(path, rgb):
    Image.fromarray(np.clip(np.round(rgb * 255), 0, 255).astype(np.uint8), 'RGB').save(path, optimize=True)


def edge_points(m, faces_mask_fn, step=0.0005):
    """Densified points along mesh boundary edges of the selected faces."""
    ls, lt = m['ls'], m['lt']
    count = {}
    for f in np.nonzero(faces_mask_fn)[0]:
        L = np.arange(ls[f], ls[f] + lt[f])
        v = m['lv'][L]
        for k in range(len(v)):
            a, b = v[k], v[(k + 1) % len(v)]
            key = (min(a, b), max(a, b))
            count[key] = count.get(key, 0) + 1
    pts = []
    co = m['co']
    for (a, b), c in count.items():
        if c != 1:
            continue
        pa, pb = co[a], co[b]
        n = max(1, int(np.linalg.norm(pb - pa) / step))
        t = np.linspace(0, 1, n + 1)[:, None]
        pts.append(pa + (pb - pa) * t)
    return np.concatenate(pts) if pts else np.zeros((0, 3))


def shared_points(m, mask_a, mask_b, step=0.0005):
    """Densified points along edges shared by a face in set A and a face in set B."""
    ls, lt = m['ls'], m['lt']
    owner = {}
    co = m['co']
    out = []
    for f in range(len(ls)):
        if not (mask_a[f] or mask_b[f]):
            continue
        L = np.arange(ls[f], ls[f] + lt[f])
        v = m['lv'][L]
        for k in range(len(v)):
            a, b = v[k], v[(k + 1) % len(v)]
            key = (min(a, b), max(a, b))
            if key in owner:
                g = owner[key]
                if (mask_a[g] and mask_b[f]) or (mask_b[g] and mask_a[f]):
                    pa, pb = co[key[0]], co[key[1]]
                    n = max(1, int(np.linalg.norm(pb - pa) / step))
                    t = np.linspace(0, 1, n + 1)[:, None]
                    out.append(pa + (pb - pa) * t)
            else:
                owner[key] = f
    return np.concatenate(out) if out else np.zeros((0, 3))
