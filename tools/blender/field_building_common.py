import bpy
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "SanguoSLG.Game" / "assets" / "models"


def mat(name, color, metallic=0.0):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    shader = value.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = 0.82
    shader.inputs["Metallic"].default_value = metallic
    return value


WOOD = None
DARK = None
ROPE = None
STONE = None
EARTH = None
RED = None
GOLD = None


def reset():
    global WOOD, DARK, ROPE, STONE, EARTH, RED, GOLD
    bpy.ops.wm.read_factory_settings(use_empty=True)
    WOOD = mat("wood", (0.34, 0.18, 0.07))
    DARK = mat("dark_wood", (0.16, 0.075, 0.03))
    ROPE = mat("rope", (0.58, 0.43, 0.22))
    STONE = mat("stone", (0.39, 0.40, 0.36))
    EARTH = mat("earth", (0.30, 0.20, 0.10))
    RED = mat("banner_red", (0.55, 0.06, 0.035))
    GOLD = mat("iron_gold", (0.56, 0.39, 0.12), 0.35)


def box(name, scale, location, material, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material)
    return obj


def pole(name, radius, depth, location, material, rotation=(0, 0, 0), vertices=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    return obj


def palisade():
    for side in (-1, 1):
        y = side * 0.22
        for index, x in enumerate((-0.30, -0.18, -0.06, 0.06, 0.18, 0.30)):
            pole(f"stake_{side}_{index}", 0.035, 0.42, (x, y, 0.21), WOOD)
        box(f"rail_{side}", (0.36, 0.025, 0.035), (0, y, 0.20), DARK)
    for x in (-0.32, 0.32):
        box("side_rail", (0.025, 0.22, 0.03), (x, 0, 0.18), DARK)


def scout():
    box("camouflage_floor", (0.27, 0.22, 0.025), (0, 0, 0.025), EARTH)
    for x in (-0.22, 0.22):
        pole("scout_post", 0.022, 0.42, (x, 0, 0.23), WOOD)
    box("lean_to", (0.31, 0.23, 0.025), (0, 0, 0.43), DARK,
        (0, math.radians(12), 0))
    box("map_table", (0.12, 0.08, 0.025), (0, -0.02, 0.18), WOOD)
    pole("signal_flag", 0.014, 0.55, (0.20, 0.04, 0.33), WOOD)
    box("signal_cloth", (0.10, 0.012, 0.07), (0.11, 0.04, 0.53), RED)


def watchtower():
    for x in (-0.20, 0.20):
        for y in (-0.20, 0.20):
            pole("tower_leg", 0.035, 0.64, (x, y, 0.32), WOOD)
    box("tower_deck", (0.29, 0.29, 0.035), (0, 0, 0.54), DARK)
    for z in (0.22, 0.38):
        box("brace_x", (0.24, 0.022, 0.018), (0, -0.21, z), ROPE,
            (0, 0, math.radians(35)))
        box("brace_y", (0.022, 0.24, 0.018), (-0.21, 0, z), ROPE,
            (math.radians(35), 0, 0))
    for x, y in ((-0.25,-0.25),(-0.25,0.25),(0.25,-0.25),(0.25,0.25)):
        pole("railing", 0.018, 0.22, (x, y, 0.66), WOOD)
    box("roof", (0.34, 0.34, 0.035), (0, 0, 0.79), RED, (0, 0, math.radians(45)))


def fort():
    pole("earth_base", 0.43, 0.10, (0, 0, 0.05), EARTH, vertices=8)
    for side in (-1, 1):
        for i in range(5):
            x = -0.28 + i * 0.14
            pole("fort_stake", 0.026, 0.34, (x, side * 0.31, 0.22), WOOD)
    for x in (-0.34, 0.34):
        for i in range(4):
            y = -0.23 + i * 0.15
            pole("fort_stake", 0.026, 0.34, (x, y, 0.22), WOOD)
    box("fort_gate", (0.11, 0.025, 0.20), (0, -0.32, 0.20), DARK)
    pole("fort_flag", 0.015, 0.72, (0.20, 0.16, 0.45), WOOD)
    box("fort_banner", (0.11, 0.012, 0.09), (0.10, 0.16, 0.66), RED)


def formation():
    pole("formation_base", 0.40, 0.045, (0, 0, 0.023), EARTH, vertices=6)
    for index, angle in enumerate(range(0, 360, 60)):
        rad = math.radians(angle)
        x, y = math.cos(rad) * 0.29, math.sin(rad) * 0.29
        pole(f"formation_marker_{index}", 0.025, 0.34, (x, y, 0.20), DARK)
        box(f"formation_flag_{index}", (0.07, 0.012, 0.055),
            (x - math.sin(rad) * 0.055, y + math.cos(rad) * 0.055, 0.31), RED,
            (0, 0, rad))
    pole("command_flag", 0.018, 0.62, (0, 0, 0.34), WOOD)
    box("command_banner", (0.11, 0.014, 0.10), (-0.11, 0, 0.54), GOLD)


def tools():
    box("hammer_handle", (0.025, 0.025, 0.23), (-0.07, 0, 0.23), WOOD,
        (0, math.radians(25), math.radians(-30)))
    box("hammer_head", (0.13, 0.045, 0.045), (-0.17, 0, 0.40), GOLD,
        (0, math.radians(25), math.radians(-30)))
    box("pick_handle", (0.022, 0.022, 0.24), (0.09, 0, 0.23), WOOD,
        (0, math.radians(-25), math.radians(30)))
    box("pick_head", (0.15, 0.03, 0.03), (0.20, 0, 0.40), STONE,
        (0, math.radians(-25), math.radians(30)))


BUILDERS = {
    "palisade": (palisade, "field-palisade.glb"),
    "scout": (scout, "field-scout.glb"),
    "watchtower": (watchtower, "field-watchtower.glb"),
    "fort": (fort, "field-fort.glb"),
    "formation": (formation, "field-formation.glb"),
    "tools": (tools, "field-construction-tools.glb"),
}


def build(kind):
    reset()
    callback, filename = BUILDERS[kind]
    callback()
    bpy.ops.object.select_all(action="SELECT")
    path = OUT / filename
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", use_selection=True)
    print(f"EXPORTED: {path}")
