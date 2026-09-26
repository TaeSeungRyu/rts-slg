import bpy
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "SanguoSLG.Game" / "assets" / "models" / "ruin-common.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, roughness=0.9, metallic=0.0):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    value.use_backface_culling = True
    shader = value.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return value


STONE = material("ruin_stone", (0.42, 0.43, 0.39))
STONE_LIGHT = material("ruin_edge", (0.58, 0.56, 0.48))
STONE_DARK = material("ruin_crack", (0.20, 0.22, 0.20))
MOSS = material("ruin_moss", (0.25, 0.38, 0.20))
BRONZE = material("ruin_seal", (0.55, 0.34, 0.11), roughness=0.6, metallic=0.45)


def box(name, scale, location, mat, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    return obj


def cylinder(name, radius, depth, location, mat, vertices=8, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


cylinder("ruin_base_lower", 0.46, 0.045, (0, 0, 0.0225), STONE_DARK, vertices=6)
cylinder("ruin_base_upper", 0.38, 0.055, (0, 0, 0.0725), STONE, vertices=6)

for index, angle in enumerate((0, 60, 120, 180, 240, 300)):
    radians = math.radians(angle)
    x = math.cos(radians) * 0.31
    y = math.sin(radians) * 0.31
    box(f"rim_{index}", (0.09, 0.045, 0.025), (x, y, 0.105), STONE_LIGHT,
        rotation=(0, 0, radians + math.pi / 2))

box("arch_left", (0.065, 0.07, 0.29), (-0.20, 0.12, 0.34), STONE)
box("arch_right_broken", (0.065, 0.07, 0.21), (0.20, 0.12, 0.26), STONE,
    rotation=(0, math.radians(-3), math.radians(2)))
box("arch_lintel", (0.25, 0.075, 0.055), (-0.025, 0.12, 0.60), STONE_LIGHT,
    rotation=(0, math.radians(-4), math.radians(-3)))
box("arch_missing_fragment", (0.08, 0.065, 0.045), (0.29, 0.17, 0.13), STONE,
    rotation=(math.radians(11), math.radians(18), math.radians(31)))

cylinder("altar", 0.16, 0.14, (0, -0.06, 0.17), STONE, vertices=8)
box("tablet", (0.15, 0.045, 0.19), (0, -0.035, 0.38), STONE_LIGHT,
    rotation=(math.radians(-4), 0, 0))
cylinder("tablet_seal", 0.072, 0.018, (0, -0.084, 0.40), BRONZE, vertices=12,
    rotation=(math.radians(90), 0, 0))

for index, angle in enumerate((45, 135, 225, 315)):
    radians = math.radians(angle)
    x = math.cos(radians) * 0.31
    y = math.sin(radians) * 0.31
    height = (0.16, 0.24, 0.12, 0.20)[index]
    cylinder(f"broken_pillar_{index}", 0.045, height, (x, y, 0.105 + height / 2), STONE, vertices=8,
        rotation=(math.radians((index - 1) * 2), math.radians(index * 3), 0))

box("fallen_column", (0.045, 0.045, 0.18), (0.18, -0.20, 0.14), STONE_LIGHT,
    rotation=(math.radians(66), math.radians(8), math.radians(24)))
box("moss_arch", (0.052, 0.076, 0.07), (-0.205, 0.115, 0.48), MOSS,
    rotation=(0, 0, math.radians(-4)))
box("moss_base", (0.14, 0.055, 0.012), (-0.15, -0.27, 0.115), MOSS,
    rotation=(0, 0, math.radians(18)))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=str(OUT), export_format="GLB", use_selection=True)
print(f"EXPORTED: {OUT}")
