# Skill stage 13 (modular wardrobe), from the Arjun build 2026-10-10. Arjun-specific constants (cut lines,
# opening angles, button heights, waistband details, colours) are templates: re-measure for a new garment.
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(__file__))
MESH, UV, name = sys.argv[1], sys.argv[2], sys.argv[3]
d = np.load(os.path.join(MESH, name + '.npz')); m = {k: d[k] for k in d.files}
nuv = np.load(os.path.join(UV, name + '_uv.npy'))
tris = m['tl']
q = nuv[tris]; a = q[:,1]-q[:,0]; b = q[:,2]-q[:,0]
area = 0.5*np.abs(a[:,0]*b[:,1]-a[:,1]*b[:,0])
print(name, 'uv coverage', round(float(area.sum()),3), 'bbox', nuv.min(0).round(3), nuv.max(0).round(3))
# per material coverage
for mi in np.unique(m['fm']):
    sel = m['fm'][m['tp']] == mi
    print(' material', mi, 'coverage', round(float(area[sel].sum()),4))
