"""Create the One Man Army active effect: a giant sword falling into the target formation."""
import math
import os
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-one-man-army-sword.glb")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 36


def material(name, color, emission=0.0, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = 0.24
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


silver = material("SwordSilver", (0.62, 0.70, 0.78), 1.2, 0.85)
edge = material("SwordEdge", (0.92, 0.96, 1.0), 3.8, 0.9)
gold = material("SwordGold", (0.78, 0.42, 0.06), 2.2, 0.7)
crimson = material("ImpactCrimson", (0.72, 0.015, 0.02), 5.5, 0.15)

root = bpy.data.objects.new("OneManArmySwordRoot", None)
bpy.context.collection.objects.link(root)

# 부대 편대 높이·폭을 1.0으로 볼 때 약 60% 크기의 검(전체 길이 약 0.62).
blade_vertices = [
    (-0.055, 0.0, 0.00), (0.055, 0.0, 0.00), (0.042, 0.0, 0.43),
    (0.0, 0.0, 0.56), (-0.042, 0.0, 0.43),
]
blade_faces = [(0, 1, 2, 3, 4)]
mesh = bpy.data.meshes.new("SwordBladeMesh")
mesh.from_pydata(blade_vertices, [], blade_faces)
mesh.materials.append(silver)
blade = bpy.data.objects.new("SwordBlade", mesh)
bpy.context.collection.objects.link(blade)
blade.parent = root
solid = blade.modifiers.new("BladeThickness", "SOLIDIFY")
solid.thickness = 0.018
solid.material_offset = 0

# 밝은 칼날 테두리 두 줄.
for x in (-0.052, 0.052):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.009, depth=0.43, location=(x, 0.0, 0.22))
    obj = bpy.context.object
    obj.name = "SwordEdge"
    obj.data.materials.append(edge)
    obj.parent = root

bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, -0.035), scale=(0.105, 0.026, 0.018))
guard = bpy.context.object
guard.name = "SwordGuard"
guard.data.materials.append(gold)
guard.parent = root

bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.025, depth=0.13, location=(0.0, 0.0, -0.115))
grip = bpy.context.object
grip.name = "SwordGrip"
grip.data.materials.append(crimson)
grip.parent = root

bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=0.038, location=(0.0, 0.0, -0.195))
pommel = bpy.context.object
pommel.name = "SwordPommel"
pommel.data.materials.append(gold)
pommel.parent = root

# 원본 검 형상을 두 번 복제해 세 자루가 편대 내부 서로 다른 지점에 시간차로 떨어진다.
root.name = "OneManArmySword_1"
sword_roots = [root]
template_parts = list(root.children)
for index in range(2, 4):
    clone_root = bpy.data.objects.new(f"OneManArmySword_{index}", None)
    bpy.context.collection.objects.link(clone_root)
    for part in template_parts:
        clone = part.copy()
        if part.data is not None:
            clone.data = part.data.copy()
        clone.name = f"{part.name}_{index}"
        bpy.context.collection.objects.link(clone)
        clone.parent = clone_root
    sword_roots.append(clone_root)


def animate_sword(sword, index, x, y, delay):
    sword.rotation_euler = (math.pi, 0.0, math.radians((-8, 7, -3)[index]))
    sword.location = (x, y, 1.42)
    sword.scale = (0.08, 0.08, 0.08)
    sword.keyframe_insert("location", frame=1 + delay)
    sword.keyframe_insert("scale", frame=1 + delay)
    sword.location = (x, y, 1.30)
    sword.scale = (1.0, 1.0, 1.0)
    sword.keyframe_insert("location", frame=6 + delay)
    sword.keyframe_insert("scale", frame=6 + delay)
    sword.location = (x, y, 0.16)
    sword.keyframe_insert("location", frame=15 + delay)
    sword.keyframe_insert("scale", frame=15 + delay)
    sword.location = (x, y, 0.12)
    sword.scale = (1.08, 1.08, 1.08)
    sword.keyframe_insert("location", frame=20 + delay)
    sword.keyframe_insert("scale", frame=20 + delay)
    sword.scale = (0.01, 0.01, 0.01)
    sword.keyframe_insert("scale", frame=29 + delay)

    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.10, minor_radius=0.014, major_segments=24, minor_segments=6,
        location=(x, y, 0.0),
    )
    ring = bpy.context.object
    ring.name = f"SwordImpactRing_{index + 1}"
    ring.data.materials.append(crimson)
    ring.scale = (0.01, 0.01, 0.01)
    ring.keyframe_insert("scale", frame=14 + delay)
    ring.scale = (2.2, 2.2, 0.35)
    ring.keyframe_insert("scale", frame=22 + delay)
    ring.scale = (0.01, 0.01, 0.01)
    ring.keyframe_insert("scale", frame=29 + delay)


for i, (sword, placement) in enumerate(zip(sword_roots, [(-0.15, 0.08, 0), (0.14, 0.09, 3), (0.0, -0.14, 6)])):
    animate_sword(sword, i, *placement)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=True)
print(f"exported {OUT}")
