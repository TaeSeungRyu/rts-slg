import sys
from pathlib import Path
import bpy
import bmesh
import math
from mathutils import Vector

kind = sys.argv[sys.argv.index('--') + 1]
root = Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root / 'SanguoSLG.Game/assets/models' / f'field-{kind}.glb'))
bpy.context.view_layer.update()
meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
assert meshes
points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
assert max(abs(p.x) for p in points) < 0.58
assert max(abs(p.y) for p in points) < 0.58
if kind == 'palisade':
    sections = sorted((obj for obj in bpy.data.objects if obj.name.startswith('palisade_section_')), key=lambda x: x.name)
    assert len(sections) == 3
    assert sections[0].location.y < sections[1].location.y > sections[2].location.y
    assert sections[0].location.x < sections[1].location.x < sections[2].location.x
    assert all(len(section.children) == 16 for section in sections)
    assert max(p.z for p in points) < 0.20
if kind == 'fort':
    roofs = [obj for obj in meshes if obj.name.startswith(('fort_corner_roof_', 'fort_keep_roof', 'fort_gate_roof'))]
    assert len(roofs) == 12
    for roof in roofs:
        mesh = bmesh.new()
        mesh.from_mesh(roof.data)
        bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=0.00001)
        assert all(edge.is_manifold for edge in mesh.edges), roof.name
        assert mesh.calc_volume(signed=True) > 0, roof.name
        mesh.free()
        center = sum((Vector(p) for p in roof.bound_box), Vector()) / 8
        for i in range(8):
            direction = Vector((math.cos(i*math.pi/4)*0.6, math.sin(i*math.pi/4)*0.6, 1)).normalized()
            hit, position, normal, face = roof.ray_cast(center + direction*2, -direction)
            assert hit and normal.dot(direction) > 0, (roof.name, i)
print(f'FIELD GEOMETRY QA PASS {kind}: meshes={len(meshes)}')
