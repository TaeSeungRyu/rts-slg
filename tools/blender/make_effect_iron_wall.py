"""Create Iron Wall: a heavy blue-steel cuirass that protects the caster."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-iron-wall.glb")
PREVIEW = os.path.join(ROOT, ".tmp-iron-wall-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.22, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


dark = material("JoseonBlackIron", (0.035, 0.025, 0.022), 0.82, 0.20, 0.22)
blue = material("JoseonCrimsonArmor", (0.40, 0.025, 0.018), 0.58, 0.23, 0.62)
silver = material("JoseonGoldenStud", (0.88, 0.46, 0.07), 0.88, 0.13, 1.55)
cyan = material("IronWallWardGlow", (1.0, 0.20, 0.06), 0.35, 0.10, 4.8)

root = bpy.data.objects.new("IronWallRoot", None)
bpy.context.collection.objects.link(root)
# 직전 GLB보다 다시 20% 축소(최초 대비 0.512).
root.scale = (0.512, 0.512, 0.512)


def bevel(obj, width=0.018, segments=2):
    mod = obj.modifiers.new("FortifiedBevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"


def cube(name, location, scale, mat, rotation=(0.0, 0.0, 0.0), width=0.015):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel(obj, width, 3)
    obj.parent = root
    return obj


# 조선 두정갑: 붉은 직물 위에 금속 두정(둥근 못)을 박은 5단 몸통.
for row, (z, width) in enumerate(((0.22, 0.30), (0.12, 0.315), (0.02, 0.31),
                                  (-0.08, 0.29), (-0.18, 0.26))):
    cube(f"IronWall_ChestPlate_{row + 1}", (0.0, 0.0, z),
         (width, 0.055, 0.052), blue, width=0.018)
    cube(f"IronWall_BlackBorder_{row + 1}", (0.0, -0.060, z + 0.047),
         (width * 0.96, 0.010, 0.009), dark, width=0.004)
    for col in range(5):
        x = (col - 2) * width * 0.42
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=0.016,
                                             location=(x, -0.074, z))
        stud = bpy.context.object
        stud.name = f"IronWall_DujeongStud_{row + 1}_{col + 1}"
        stud.scale = (1.0, 0.45, 1.0)
        stud.data.materials.append(silver)
        stud.parent = root

# 검은 테두리의 붉은 어깨 가리개와 분할된 허리 갑상.
for side, sign in (("Left", -1), ("Right", 1)):
    for layer in range(3):
        cube(f"IronWall_ShoulderGuard{side}_{layer + 1}",
             (sign * (0.29 + layer * 0.018), -0.006, 0.20 - layer * 0.075),
             (0.105 - layer * 0.012, 0.052, 0.038), blue if layer != 1 else dark,
             rotation=(0.0, 0.0, math.radians(sign * (8 + layer * 3))), width=0.014)
for col in range(5):
    x = (col - 2) * 0.105
    cube(f"IronWall_WaistTasset_{col + 1}", (x, 0.0, -0.29),
         (0.046, 0.048, 0.085), blue if col % 2 else dark,
         rotation=(0.0, 0.0, math.radians((col - 2) * -2.5)), width=0.012)

# 중앙 호심경과 붉은 보호 고리.
bpy.ops.mesh.primitive_torus_add(major_radius=0.070, minor_radius=0.012, major_segments=24,
                                 minor_segments=6, location=(0.0, -0.075, 0.08),
                                 rotation=(math.pi / 2, 0.0, 0.0))
mirror = bpy.context.object
mirror.name = "IronWall_HeartMirror"
mirror.data.materials.append(silver)
mirror.parent = root

# A luminous protection ring belongs to the GLB and pulses with the armour.
bpy.ops.mesh.primitive_torus_add(major_radius=0.42, minor_radius=0.014, major_segments=32,
                                 minor_segments=6, location=(0.0, 0.0, 0.03), rotation=(math.pi / 2, 0.0, 0.0))
ring = bpy.context.object
ring.name = "IronWall_ProtectionRing"
ring.data.materials.append(cyan)
ring.parent = root

# Preview only.
bpy.ops.object.light_add(type="AREA", location=(2.2, -3.0, 2.8))
key = bpy.context.object
key.data.energy = 950
key.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, -1.2, 1.4))
fill = bpy.context.object
fill.data.energy = 600
fill.data.color = (0.15, 0.45, 1.0)
fill.data.size = 2.5
bpy.ops.object.camera_add(location=(0.0, -3.6, 0.25), rotation=(math.pi / 2, 0.0, 0.0))
camera = bpy.context.object
camera.rotation_euler = (math.pi / 2, 0.0, 0.0)
scene.camera = camera
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for child in root.children_recursive:
    child.select_set(True)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=False, use_selection=True)
print(f"exported {OUT}")
print(f"preview {PREVIEW}")
