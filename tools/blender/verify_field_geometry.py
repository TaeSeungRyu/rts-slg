import sys
from pathlib import Path
import bpy
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
print(f'FIELD GEOMETRY QA PASS {kind}: meshes={len(meshes)}')
