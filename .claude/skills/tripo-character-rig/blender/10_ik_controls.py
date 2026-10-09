# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- IK targets, poles, look target, bone collections (Meera call 125) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_ik = r'''
import mathutils, math
rig = bpy.data.objects['Meera_Rig']; arm = rig.data
for o in bpy.context.view_layer.objects: o.select_set(False)
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.edit_bones
V = mathutils.Vector
def signed_angle(u, v, n):
    a = u.angle(v)
    return -a if u.cross(v).dot(n) < 0 else a
def pole_angle(base, tip_bone, pole):
    pole_normal = (tip_bone.tail - base.head).cross(pole - base.head)
    proj = pole_normal.cross(base.tail - base.head)
    return signed_angle(base.x_axis, proj, base.tail - base.head)
root = eb['CC_Base_BoneRoot']
angles = {}
for side, sg in (('L',1),('R',-1)):
    foot = eb[f'CC_Base_{side}_Foot']; calf = eb[f'CC_Base_{side}_Calf']; thigh = eb[f'CC_Base_{side}_Thigh']
    ikf = eb.new(f'IK_Foot_{side}'); ikf.head = foot.head.copy(); ikf.tail = foot.tail.copy(); ikf.roll = foot.roll; ikf.parent = root; ikf.use_deform = False
    knee_pole = eb.new(f'IK_Knee_{side}'); p = calf.head + V((0, -0.38, 0)); knee_pole.head = p; knee_pole.tail = p + V((0, 0, 0.05)); knee_pole.parent = root; knee_pole.use_deform = False
    angles[f'leg_{side}'] = pole_angle(thigh, calf, p)
    hand = eb[f'CC_Base_{side}_Hand']; fore = eb[f'CC_Base_{side}_Forearm']; upper = eb[f'CC_Base_{side}_Upperarm']
    ikh = eb.new(f'IK_Hand_{side}'); ikh.head = hand.head.copy(); ikh.tail = hand.tail.copy(); ikh.roll = hand.roll; ikh.parent = root; ikh.use_deform = False
    el = eb.new(f'IK_Elbow_{side}'); p2 = fore.head + V((0, 0.32, 0)); el.head = p2; el.tail = p2 + V((0, 0, 0.05)); el.parent = root; el.use_deform = False
    angles[f'arm_{side}'] = pole_angle(upper, fore, p2)
lt = eb.new('Look_Target'); lt.head = V((0, -0.65, 1.43)); lt.tail = V((0, -0.65, 1.47)); lt.parent = root; lt.use_deform = False
bpy.ops.object.mode_set(mode='POSE')
pb = rig.pose.bones
for side in ('L','R'):
    c = pb[f'CC_Base_{side}_Calf'].constraints.new('IK'); c.name = 'Leg IK'
    c.target = rig; c.subtarget = f'IK_Foot_{side}'; c.pole_target = rig; c.pole_subtarget = f'IK_Knee_{side}'; c.pole_angle = angles[f'leg_{side}']; c.chain_count = 2
    c2 = pb[f'CC_Base_{side}_Foot'].constraints.new('COPY_ROTATION'); c2.name = 'Foot follows IK'; c2.target = rig; c2.subtarget = f'IK_Foot_{side}'
    c3 = pb[f'CC_Base_{side}_Forearm'].constraints.new('IK'); c3.name = 'Arm IK'
    c3.target = rig; c3.subtarget = f'IK_Hand_{side}'; c3.pole_target = rig; c3.pole_subtarget = f'IK_Elbow_{side}'; c3.pole_angle = angles[f'arm_{side}']; c3.chain_count = 2
    c4 = pb[f'CC_Base_{side}_Hand'].constraints.new('COPY_ROTATION'); c4.name = 'Hand follows IK'; c4.target = rig; c4.subtarget = f'IK_Hand_{side}'
    ce = pb[f'CC_Base_{side}_Eye'].constraints.new('DAMPED_TRACK'); ce.name = 'Eye look'; ce.target = rig; ce.subtarget = 'Look_Target'; ce.track_axis = 'TRACK_Y'
bpy.ops.object.mode_set(mode='OBJECT')
# bone collections for animators
cols = {}
for n in ('Deform', 'Face', 'Dynamics', 'Controls'):
    cols[n] = arm.collections.get(n) or arm.collections.new(n)
for b in arm.bones:
    n = b.name
    if n.startswith(('IK_', 'Look_')): cols['Controls'].assign(b)
    elif n.startswith(('Hair_', 'Earring_', 'Kurti_', 'Sleeve_')): cols['Dynamics'].assign(b)
    elif n in ('CC_Base_JawRoot', 'CC_Base_L_Eye', 'CC_Base_R_Eye'): cols['Face'].assign(b)
    else: cols['Deform'].assign(b)
for b in arm.bones:
    if b.name.startswith(('IK_', 'Look_')): b.color.palette = 'THEME09'
    elif b.name.startswith(('Hair_', 'Earring_', 'Kurti_', 'Sleeve_')): b.color.palette = 'THEME03'
bpy.context.view_layer.update()
dev = 0.0
for p in rig.pose.bones:
    if p.name.startswith(('IK_', 'Look_')) or p.name.endswith('_Eye'): continue
    d = (p.matrix.to_translation() - p.bone.matrix_local.to_translation()).length + (p.matrix.to_3x3() - p.bone.matrix_local.to_3x3()).median_scale if False else (p.matrix - p.bone.matrix_local).median_scale
    dev = max(dev, max(abs(v) for row in (p.matrix - p.bone.matrix_local) for v in row))
bpy.app.driver_namespace['ik_rest_dev'] = dev
bpy.app.driver_namespace['ik_angles'] = {k: math.degrees(v) for k, v in angles.items()}
'''
r = submit("IK controls", [{"label": "Add leg/arm IK, look target and bone collections", "code": code_ik}])
result = {"job": r["job"]["state"]}

# ---- Pole-angle 90-degree search (Meera call 128) ----
import bpy, math
rig = bpy.data.objects['Meera_Rig']
def best_angle(con, bones):
    best = None
    for cand in [con.pole_angle + k*math.pi/2 for k in range(4)]:
        a = math.atan2(math.sin(cand), math.cos(cand))
        con.pole_angle = a; bpy.context.view_layer.update()
        dev = 0.0
        for n in bones:
            p = rig.pose.bones[n]; M = p.matrix - p.bone.matrix_local
            dev = max(dev, max(abs(v) for row in M for v in row))
        if best is None or dev < best[0]: best = (dev, a)
    con.pole_angle = best[1]; bpy.context.view_layer.update()
    return best
res = {}
for side in ('L','R'):
    c = rig.pose.bones[f'CC_Base_{side}_Calf'].constraints['Leg IK']
    res['leg_'+side] = best_angle(c, [f'CC_Base_{side}_Thigh', f'CC_Base_{side}_Calf'])
    c = rig.pose.bones[f'CC_Base_{side}_Forearm'].constraints['Arm IK']
    res['arm_'+side] = best_angle(c, [f'CC_Base_{side}_Upperarm', f'CC_Base_{side}_Forearm'])
devs = []
for p in rig.pose.bones:
    if p.name.endswith('_Eye'): continue
    M = p.matrix - p.bone.matrix_local; d = max(abs(v) for row in M for v in row)
    if d > 1e-4: devs.append((p.name, round(d,6)))
result = {"fix": {k: [round(v[0],6), round(math.degrees(v[1]),2)] for k, v in res.items()}, "remaining": devs[:10]}
