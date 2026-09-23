import bpy
import math
from pathlib import Path
from mathutils import Vector

OUTPUT_FILE = "effect-lightning.glb"

FPS = 24
FRAME_START = 1
FRAME_END = 18

SCRIPT_PATH = Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[2]
MODEL_DIR = REPO_ROOT / "SanguoSLG.Game" / "assets" / "models"

# 기존 보병 높이 약 0.27 기준.
# 낙뢰는 약 3배 높이, 지면 효과는 한 부대 주변에만 보이도록 제한.
BOLT_TOP_Z = 0.82
BOLT_BOTTOM_Z = 0.035
GROUND_RADIUS = 0.18
MAIN_THICKNESS = 0.014
BRANCH_THICKNESS = 0.008

HIDDEN = 0.001
VISIBLE = 1.0

bpy.ops.wm.read_factory_settings(use_empty=True)

scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start = FRAME_START
scene.frame_end = FRAME_END


def make_emission_mat(name, color, strength):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = False

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        elif "Emission" in bsdf.inputs:
            bsdf.inputs["Emission"].default_value = (*color, 1.0)

        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = strength

        bsdf.inputs["Roughness"].default_value = 0.35

    return mat


MAT_CORE = make_emission_mat("lightning_core", (0.92, 0.97, 1.00), 8.0)
MAT_GLOW = make_emission_mat("lightning_glow", (0.42, 0.67, 1.00), 4.0)
MAT_GROUND = make_emission_mat("lightning_ground", (0.34, 0.58, 1.00), 3.5)
MAT_SPARK = make_emission_mat("lightning_spark", (0.78, 0.90, 1.00), 5.0)


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def key_scale(obj, frame, value):
    obj.scale = (value, value, value)
    obj.keyframe_insert(data_path="scale", frame=frame)


def key_loc(obj, frame, z=None):
    loc = list(obj.location)
    if z is not None:
        loc[2] = z
    obj.location = loc
    obj.keyframe_insert(data_path="location", frame=frame)


def key_rot_z(obj, frame, degrees):
    rot = list(obj.rotation_euler)
    rot[2] = math.radians(degrees)
    obj.rotation_euler = rot
    obj.keyframe_insert(data_path="rotation_euler", frame=frame)


def create_cylinder_between(name, p1, p2, radius, material, verts=6, parent=None):
    p1 = Vector(p1)
    p2 = Vector(p2)
    delta = p2 - p1
    length = delta.length
    midpoint = (p1 + p2) * 0.5

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts,
        radius=radius,
        depth=length,
        location=midpoint,
    )

    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)

    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(delta.normalized())
    obj.rotation_mode = "XYZ"

    if parent is not None:
        obj.parent = parent

    return obj


def create_bolt(name, points, radius, material, parent, verts=6):
    group = empty(name, parent)

    for i in range(len(points) - 1):
        create_cylinder_between(
            f"{name}_seg_{i:02d}",
            points[i],
            points[i + 1],
            radius,
            material,
            verts=verts,
            parent=group,
        )

    return group


def create_ground_ring(name, radius, tube_radius, material, parent):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=radius,
        minor_radius=tube_radius,
        major_segments=20,
        minor_segments=5,
        location=(0.0, 0.0, 0.018),
    )

    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    obj.parent = parent
    return obj


def create_impact_disc(name, radius, material, parent):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=radius,
        depth=0.010,
        location=(0.0, 0.0, 0.010),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    obj.parent = parent
    return obj


root = empty("effect_lightning_root")
root["asset_type"] = "effect"
root["effect_type"] = "lightning"
root["loop"] = False
root["play_once"] = True
root["frame_start"] = FRAME_START
root["frame_end"] = FRAME_END
root["fps"] = FPS
root["effect_height"] = BOLT_TOP_Z
root["ground_radius"] = GROUND_RADIUS

flash_1 = empty("flash_1", root)
flash_2 = empty("flash_2", root)
impact = empty("impact", root)
sparks = empty("sparks", root)

# 첫 번째 번개
main_points_1 = [
    (0.010, 0.005, BOLT_TOP_Z),
    (-0.030, 0.012, 0.690),
    (0.025, -0.006, 0.575),
    (-0.018, 0.016, 0.455),
    (0.030, -0.012, 0.335),
    (-0.012, 0.005, 0.205),
    (0.000, 0.000, BOLT_BOTTOM_Z),
]

create_bolt("main_glow_1", main_points_1, MAIN_THICKNESS * 1.85, MAT_GLOW, flash_1)
create_bolt("main_bolt_1", main_points_1, MAIN_THICKNESS, MAT_CORE, flash_1)

create_bolt(
    "branch_1",
    [
        (-0.030, 0.012, 0.690),
        (-0.105, 0.030, 0.610),
        (-0.140, 0.018, 0.535),
    ],
    BRANCH_THICKNESS,
    MAT_CORE,
    flash_1,
    verts=5,
)

create_bolt(
    "branch_2",
    [
        (0.025, -0.006, 0.575),
        (0.095, -0.025, 0.500),
        (0.122, -0.050, 0.425),
    ],
    BRANCH_THICKNESS * 0.85,
    MAT_CORE,
    flash_1,
    verts=5,
)

create_bolt(
    "branch_3",
    [
        (-0.018, 0.016, 0.455),
        (-0.078, 0.055, 0.370),
    ],
    BRANCH_THICKNESS * 0.75,
    MAT_GLOW,
    flash_1,
    verts=5,
)

# 두 번째 섬광은 살짝 다른 모양
main_points_2 = [
    (-0.006, -0.004, BOLT_TOP_Z * 0.96),
    (0.026, 0.008, 0.690),
    (-0.020, -0.015, 0.570),
    (0.022, 0.008, 0.445),
    (-0.025, -0.008, 0.310),
    (0.010, 0.004, 0.175),
    (0.000, 0.000, BOLT_BOTTOM_Z),
]

create_bolt("main_bolt_2", main_points_2, MAIN_THICKNESS * 0.88, MAT_CORE, flash_2)

create_bolt(
    "branch_2a",
    [
        (0.026, 0.008, 0.690),
        (0.090, 0.045, 0.605),
    ],
    BRANCH_THICKNESS * 0.8,
    MAT_GLOW,
    flash_2,
    verts=5,
)

create_bolt(
    "branch_2b",
    [
        (-0.020, -0.015, 0.570),
        (-0.085, -0.035, 0.490),
        (-0.110, -0.018, 0.430),
    ],
    BRANCH_THICKNESS * 0.7,
    MAT_CORE,
    flash_2,
    verts=5,
)

# 지면 충격
create_ground_ring("impact_ring_outer", GROUND_RADIUS, 0.010, MAT_GROUND, impact)
create_ground_ring("impact_ring_inner", GROUND_RADIUS * 0.52, 0.007, MAT_CORE, impact)
create_impact_disc("impact_disc", GROUND_RADIUS * 0.32, MAT_GLOW, impact)

ground_cracks = [
    [(0.00, 0.00, 0.025), (0.08, 0.01, 0.025), (0.15, 0.04, 0.022)],
    [(0.00, 0.00, 0.025), (-0.07, 0.03, 0.024), (-0.14, 0.01, 0.020)],
    [(0.00, 0.00, 0.025), (0.04, -0.07, 0.024), (0.06, -0.14, 0.020)],
    [(0.00, 0.00, 0.025), (-0.04, -0.06, 0.024), (-0.11, -0.12, 0.020)],
]

for i, pts in enumerate(ground_cracks):
    create_bolt(
        f"ground_crack_{i}",
        pts,
        0.005,
        MAT_GROUND,
        impact,
        verts=5,
    )

# 스파크
spark_specs = [
    ((0.00, 0.00, 0.030), (0.13, 0.05, 0.200)),
    ((0.00, 0.00, 0.030), (-0.12, 0.08, 0.170)),
    ((0.00, 0.00, 0.030), (0.08, -0.12, 0.150)),
    ((0.00, 0.00, 0.030), (-0.10, -0.09, 0.185)),
    ((0.00, 0.00, 0.030), (0.03, 0.12, 0.225)),
]

for i, (start, end) in enumerate(spark_specs):
    create_cylinder_between(
        f"spark_{i}",
        start,
        end,
        0.004,
        MAT_SPARK,
        verts=5,
        parent=sparks,
    )

# 애니메이션
for group in (flash_1, flash_2, impact, sparks):
    key_scale(group, 1, HIDDEN)

key_scale(flash_1, 2, VISIBLE)
key_scale(flash_1, 3, VISIBLE)
key_scale(flash_1, 4, HIDDEN)

key_scale(flash_2, 4, HIDDEN)
key_scale(flash_2, 5, VISIBLE)
key_scale(flash_2, 6, VISIBLE)
key_scale(flash_2, 7, HIDDEN)

key_scale(impact, 4, HIDDEN)
key_scale(impact, 5, 0.35)
key_scale(impact, 7, 1.00)
key_scale(impact, 10, 1.18)
key_scale(impact, 14, HIDDEN)

key_scale(sparks, 5, HIDDEN)
key_scale(sparks, 6, 0.45)
key_scale(sparks, 8, VISIBLE)
key_scale(sparks, 12, 0.65)
key_scale(sparks, 14, HIDDEN)

key_loc(sparks, 6, z=0.0)
key_loc(sparks, 9, z=0.045)
key_loc(sparks, 14, z=0.075)

key_rot_z(sparks, 6, 0)
key_rot_z(sparks, 10, 18)
key_rot_z(sparks, 14, 28)

for group in (flash_1, flash_2, impact, sparks):
    key_scale(group, FRAME_END, HIDDEN)

scene.timeline_markers.new("LIGHTNING", frame=1)
scene.timeline_markers.new("IMPACT", frame=5)
scene.timeline_markers.new("END", frame=FRAME_END)

MODEL_DIR.mkdir(parents=True, exist_ok=True)
output_path = str(MODEL_DIR / OUTPUT_FILE)

export_kwargs = dict(
    filepath=output_path,
    export_format="GLB",
    use_selection=False,
    export_animations=True,
    export_frame_range=True,
    export_extras=True,
    export_yup=True,
    export_apply=False,
)

try:
    bpy.ops.export_scene.gltf(
        **export_kwargs,
        export_animation_mode="SCENE",
        export_nla_strips_merged_animation_name="Lightning",
        export_force_sampling=True,
    )
except TypeError:
    bpy.ops.export_scene.gltf(**export_kwargs)

print("")
print("==============================================")
print(" Lightning Effect Export Complete")
print("==============================================")
print("SCRIPT :", str(SCRIPT_PATH))
print("ROOT   :", str(REPO_ROOT))
print("MODEL  :", str(MODEL_DIR))
print("OUTPUT :", output_path)
print("HEIGHT :", BOLT_TOP_Z)
print("RADIUS :", GROUND_RADIUS)
print("FRAMES :", FRAME_START, "-", FRAME_END)
print("FPS    :", FPS)
print("==============================================")
