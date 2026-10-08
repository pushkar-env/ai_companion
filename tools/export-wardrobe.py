"""Run as a short stage in the connected Blender live workflow after garment fitting.
Exports authored mesh normals/UVs and named skin weights, without re-exporting Alita.
"""
import bpy
import json
from pathlib import Path

folder = Path('D:/Unity/ai_companion/models/wardrobe')
folder.mkdir(parents=True, exist_ok=True)
for name in ['Dress', 'Crop_T_shirts', 'Denim_shorts', 'Sleeveless_shell', 'Midi_skirt']:
    obj = bpy.data.objects[name]
    mesh = obj.data
    mesh.calc_loop_triangles()
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    data = dict(vertices=[], normals=[], uv=[], weights=[], triangles=[])
    for triangle in mesh.loop_triangles:
        for loop_index in triangle.loops:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index]
            point = obj.matrix_world @ vertex.co
            normal = (normal_matrix @ mesh.corner_normals[loop_index].vector).normalized()
            uv = mesh.uv_layers.active.data[loop_index].uv
            groups = sorted(vertex.groups, key=lambda item: item.weight, reverse=True)[:4]
            data['vertices'].append(dict(x=point.x, y=point.z, z=-point.y))
            data['normals'].append(dict(x=normal.x, y=normal.z, z=-normal.y))
            data['uv'].append(dict(x=uv.x, y=uv.y))
            data['weights'].append(dict(names=[obj.vertex_groups[g.group].name for g in groups], values=[g.weight for g in groups]))
            data['triangles'].append(len(data['triangles']))
    (folder / (name+'.json')).write_text(json.dumps(data, separators=(',', ':')), encoding='utf-8')
bpy.ops.wm.save_as_mainfile()
