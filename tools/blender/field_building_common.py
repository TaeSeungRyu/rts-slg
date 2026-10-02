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
CLOUD = None


def reset():
    global WOOD, DARK, ROPE, STONE, EARTH, RED, GOLD, CLOUD
    bpy.ops.wm.read_factory_settings(use_empty=True)
    WOOD = mat("wood", (0.34, 0.18, 0.07))
    DARK = mat("dark_wood", (0.16, 0.075, 0.03))
    ROPE = mat("rope", (0.58, 0.43, 0.22))
    STONE = mat("stone", (0.39, 0.40, 0.36))
    EARTH = mat("earth", (0.30, 0.20, 0.10))
    RED = mat("banner_red", (0.55, 0.06, 0.035))
    GOLD = mat("iron_gold", (0.56, 0.39, 0.12), 0.35)
    CLOUD = mat("cloud_mist", (0.72, 0.76, 0.75))


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


def stone(name, scale, location, material=STONE, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
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
    # 한 타일 안에 들어오는 작은 동양식 석성. 낮은 성벽과 네 귀퉁이 망루가
    # 중앙 성채를 둘러싸므로 기존 목책과 실루엣부터 명확히 구분된다.
    pole("fort_stone_foundation", 0.43, 0.09, (0, 0, 0.045), STONE, vertices=8)
    box("fort_wall_north", (0.28, 0.045, 0.13), (0, 0.29, 0.17), STONE)
    box("fort_wall_south_left", (0.095, 0.045, 0.13), (-0.19, -0.29, 0.17), STONE)
    box("fort_wall_south_right", (0.095, 0.045, 0.13), (0.19, -0.29, 0.17), STONE)
    box("fort_wall_west", (0.045, 0.24, 0.13), (-0.31, 0, 0.17), STONE)
    box("fort_wall_east", (0.045, 0.24, 0.13), (0.31, 0, 0.17), STONE)

    for index, (x, y) in enumerate(((-0.29, -0.27), (0.29, -0.27),
                                     (-0.29, 0.27), (0.29, 0.27))):
        pole(f"fort_corner_tower_{index}", 0.105, 0.36, (x, y, 0.22), STONE, vertices=8)
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.155, radius2=0.035, depth=0.11,
                                        location=(x, y, 0.455), rotation=(0, 0, math.radians(45)))
        roof = bpy.context.object
        roof.name = f"fort_corner_roof_{index}"
        roof.data.materials.append(RED)

    # 중앙 내성 및 중층 지붕.
    box("fort_keep", (0.19, 0.17, 0.22), (0, 0.02, 0.29), STONE)
    box("fort_keep_band", (0.205, 0.185, 0.025), (0, 0.02, 0.41), DARK)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.29, radius2=0.06, depth=0.14,
                                    location=(0, 0.02, 0.57), rotation=(0, 0, math.radians(45)))
    keep_roof = bpy.context.object
    keep_roof.name = "fort_keep_roof"
    keep_roof.data.materials.append(RED)

    # 남쪽 성문과 금색 문장.
    box("fort_gate", (0.075, 0.052, 0.105), (0, -0.305, 0.115), DARK)
    box("fort_gate_lintel", (0.105, 0.057, 0.025), (0, -0.305, 0.235), GOLD)
    box("fort_gate_emblem", (0.026, 0.059, 0.026), (0, -0.307, 0.17), GOLD,
        (0, 0, math.radians(45)))

    # 성가퀴가 성곽 실루엣을 만든다.
    for x in (-0.22, -0.08, 0.08, 0.22):
        box("fort_crenel_north", (0.035, 0.035, 0.04), (x, 0.30, 0.32), STONE)
    for x in (-0.24, 0.24):
        box("fort_crenel_south", (0.035, 0.035, 0.04), (x, -0.30, 0.32), STONE)


def formation():
    pole("formation_base", 0.41, 0.055, (0, 0, 0.028), EARTH, vertices=8)

    # 네 개의 거대한 석주. 기둥마다 받침과 머릿돌을 두어 멀리서도 진법으로 읽힌다.
    for index, (x, y) in enumerate(((-0.25, -0.22), (0.25, -0.22),
                                     (-0.25, 0.22), (0.25, 0.22))):
        box(f"formation_pillar_base_{index}", (0.09, 0.09, 0.035), (x, y, 0.075), STONE)
        pole(f"formation_pillar_{index}", 0.057, 0.70, (x, y, 0.445), STONE, vertices=8)
        box(f"formation_pillar_cap_{index}", (0.085, 0.085, 0.035), (x, y, 0.80), GOLD)
        pole(f"formation_pillar_ring_{index}", 0.068, 0.035, (x, y, 0.68), GOLD, vertices=8)

    # 중앙과 기둥 사이의 작은 돌무더기.
    rock_specs = (
        (-0.10, -0.02, 0.075, 0.075), (0.02, 0.03, 0.095, 0.085),
        (0.13, -0.04, 0.065, 0.060), (-0.02, -0.12, 0.055, 0.050),
        (-0.14, 0.12, 0.050, 0.045), (0.15, 0.13, 0.045, 0.040),
    )
    for index, (x, y, sx, sz) in enumerate(rock_specs):
        stone(f"formation_rock_{index}", (sx, sx * 0.82, sz), (x, y, 0.055), STONE,
              (0, math.radians(index * 17), math.radians(index * 29)))

    # 낮은 구름 띠가 석주 사이를 천천히 통과한다. 이 Empty의 Action은 GLB에
    # FormationCloudDrift 클립으로 수출되고 게임에서 반복 재생한다.
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(-0.30, 0, 0.36))
    cloud_root = bpy.context.object
    cloud_root.name = "formation_cloud_root"
    for index, (x, y, z, sx) in enumerate((
        (-0.18, -0.03, 0.00, 0.12), (-0.08, 0.00, 0.02, 0.15),
        (0.04, -0.02, 0.00, 0.11), (0.17, 0.02, 0.01, 0.13),
        (0.27, -0.01, 0.00, 0.09),
    )):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1,
                                             location=(x - 0.30, y, z + 0.36))
        cloud = bpy.context.object
        cloud.name = f"formation_cloud_{index}"
        cloud.scale = (sx, sx * 0.48, sx * 0.30)
        cloud.data.materials.append(CLOUD)
        cloud.parent = cloud_root
        cloud.matrix_parent_inverse = cloud_root.matrix_world.inverted()

    bpy.context.scene.render.fps = 30
    cloud_root.location = (-0.30, 0, 0.36)
    cloud_root.keyframe_insert(data_path="location", frame=1)
    cloud_root.location = (0.30, 0, 0.36)
    cloud_root.keyframe_insert(data_path="location", frame=120)
    if cloud_root.animation_data and cloud_root.animation_data.action:
        cloud_root.animation_data.action.name = "FormationCloudDrift"


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
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", use_selection=True,
                              export_animations=(kind == "formation"))
    print(f"EXPORTED: {path}")
