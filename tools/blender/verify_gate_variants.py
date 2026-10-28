from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / "SanguoSLG.Game" / "assets" / "models"


def verify(variant):
    path = MODELS / f"gate-{variant}.glb"
    assert path.exists() and path.stat().st_size > 1000, path
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    assert len(meshes) >= (70 if variant == "medium" else 55), len(meshes)
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    lower = tuple(min(point[i] for point in points) for i in range(3))
    upper = tuple(max(point[i] for point in points) for i in range(3))
    assert lower[0] >= -0.58 and upper[0] <= 0.58, (variant, lower, upper)
    assert lower[1] >= -0.58 and upper[1] <= 0.58, (variant, lower, upper)
    assert lower[2] >= -0.01 and upper[2] <= 1.05, (variant, lower, upper)
    materials = {slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}
    assert any("roof" in name for name in materials), materials
    assert any("gate" in name for name in materials), materials
    print(f"GATE_QA={variant} meshes={len(meshes)} bounds={lower}..{upper} materials={len(materials)}")


for name in ("small", "medium"):
    verify(name)
