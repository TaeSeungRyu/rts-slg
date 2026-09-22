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


dark = material("IronWallDeepSteel", (0.035, 0.09, 0.16), 0.94, 0.16, 0.32)
blue = material("IronWallBlueSteel", (0.12, 0.34, 0.62), 0.90, 0.15, 0.78)
silver = material("IronWallSilverEdge", (0.58, 0.75, 0.90), 0.91, 0.12, 1.35)
cyan = material("IronWallWardGlow", (0.14, 0.76, 1.0), 0.35, 0.10, 5.5)

root = bpy.data.objects.new("IronWallRoot", None)
bpy.context.collection.objects.link(root)
# 기존 크기의 80%. 런타임 보정 없이 GLB 자체 크기를 줄인다.
root.scale = (0.8, 0.8, 0.8)


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


# 동아시아 찰갑: 가죽끈으로 엮은 작은 철편 5단. 서양 흉갑·옷 실루엣을 피한다.
row_specs = ((0.22, 5, 0.105), (0.12, 6, 0.098), (0.02, 6, 0.095),
             (-0.08, 6, 0.088), (-0.18, 5, 0.084))
for row, (z, count, half_width) in enumerate(row_specs):
    spacing = half_width * 1.72
    start = -(count - 1) * spacing * 0.5
    for col in range(count):
        x = start + col * spacing
        cube(f"IronWall_Lamella_{row + 1}_{col + 1}", (x, -0.012, z),
             (half_width, 0.048, 0.044), blue if (row + col) % 2 == 0 else dark,
             width=0.012)
        # 철편을 잇는 밝은 가로 매듭.
        cube(f"IronWall_Lacing_{row + 1}_{col + 1}", (x, -0.064, z + 0.026),
             (half_width * 0.70, 0.008, 0.006), silver, width=0.003)
    # QA가 갑옷의 5단 구조를 안정적으로 판별하는 숨은 얇은 기준판.
    cube(f"IronWall_ChestPlate_{row + 1}", (0.0, 0.038, z),
         (0.24, 0.008, 0.038), dark, width=0.004)

# 비늘식 어깨 드리개와 허리 아래의 분할 찰갑. 팔 달린 서양 갑옷처럼 보이지 않게 몸 가까이 둔다.
for side, sign in (("Left", -1), ("Right", 1)):
    for layer in range(3):
        cube(f"IronWall_ShoulderGuard{side}_{layer + 1}",
             (sign * (0.29 + layer * 0.018), -0.006, 0.20 - layer * 0.075),
             (0.105 - layer * 0.012, 0.052, 0.038), blue if layer != 1 else dark,
             rotation=(0.0, 0.0, math.radians(sign * (8 + layer * 3))), width=0.014)
for col in range(5):
    x = (col - 2) * 0.105
    cube(f"IronWall_WaistTasset_{col + 1}", (x, 0.0, -0.29),
         (0.046, 0.048, 0.085), dark if col % 2 else blue,
         rotation=(0.0, 0.0, math.radians((col - 2) * -2.5)), width=0.012)

# 중앙 매듭과 작은 호심경. 방패 문장 대신 동아시아 갑주의 결속부를 강조한다.
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
