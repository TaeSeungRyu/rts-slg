# 집단군(Army Group, 저폴리) 생성 → GLB 익스포트
# 실행:
#   blender --background --python-exit-code 1 --python make_troop_army_group.py
#
# rts-slg 집단군 기획 기준:
# - 보병 + 궁병 + 공성병기 혼성 부대
# - 최대 병력 40,000
# - 이동속도 1 / 모든 사거리 1 / 액티브 스킬 미발동
#
# 시각 구성:
# - 전열 보병 3명
# - 중열 궁병 2명
# - 후열 공성병기 1기
# - 중앙 대형 세력기 1개
#
# 공성 병기는 아래 SIEGE_VARIANT를 "catapult" 또는 "thunder_cart"로 변경한다.
# 출력: SanguoSLG.Game/assets/models/troop-army-group.glb
#
# 기존 tools/blender/infantry_common.py 와 같은 폴더에 두고 실행한다.

import bpy
import math
import os
import sys
from mathutils import Matrix

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import infantry_common as ic

SIEGE_VARIANT = "catapult"  # "catapult" | "thunder_cart"
OUTPUT_FILE = "troop-army-group.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_STRING = ic.make_mat("army_group_string", (0.85, 0.82, 0.72), roughness=0.6)
M_FLETCH = ic.make_mat("army_group_fletch", (0.88, 0.88, 0.86))
M_STONE = ic.make_mat("army_group_stone", (0.52, 0.52, 0.50))
M_PENCIL = ic.make_mat("army_group_pencil", (0.72, 0.52, 0.22))


def bake_rotation(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


def prefix_hierarchy(root, prefix):
    stack = [root]
    while stack:
        obj = stack.pop()
        if not obj.name.startswith(prefix):
            obj.name = f"{prefix}_{obj.name}"
        stack.extend(list(obj.children))


def move_root(root, x, y, z=0.0, rot_z=0.0):
    root.location.x += x
    root.location.y += y
    root.location.z += z
    root.rotation_euler.z += rot_z


def build_swordsman(prefix, x, y, rot_z=0.0):
    body, arm_l, arm_r = ic.build_body(
        m,
        arm_l_pitch=math.radians(-14),
        arm_r_pitch=math.radians(10),
    )

    sword_tilt = math.radians(-12)
    hx, hy = ic.ARM_X + 0.006, -0.014
    grip = ic.cylinder(
        f"{prefix}_sword_grip", 0.007, 0.032,
        hx, hy, ic.HAND_Z + 0.004, m.wood,
        verts=6, rot_x=sword_tilt,
    )
    guard = ic.box(
        f"{prefix}_sword_guard", 0.032, 0.012, 0.008,
        hx, hy - 0.004, ic.HAND_Z + 0.022, m.steel,
        rot_x=sword_tilt,
    )
    blade = ic.box(
        f"{prefix}_sword_blade", 0.015, 0.008, 0.100,
        hx, hy - 0.015, ic.HAND_Z + 0.077, m.steel,
        rot_x=sword_tilt,
    )
    tip = ic.cone(
        f"{prefix}_sword_tip", 0.011, 0.001, 0.022,
        hx, hy - 0.028, ic.HAND_Z + 0.138, m.steel,
        verts=4, rot_x=sword_tilt,
    )
    for part in (guard, blade, tip):
        ic.parent_to(part, grip)
    ic.parent_to(grip, arm_r)

    sx, sy, sz = -(ic.ARM_X + 0.014), -0.034, 0.128
    panel = ic.box(
        f"{prefix}_shield", 0.066, 0.014, 0.086,
        sx, sy, sz, m.wood,
    )
    rim_t = 0.010
    for tag, dx, dz, rsx, rsz in (
        ("t", 0.0, 0.046, 0.074, rim_t),
        ("b", 0.0, -0.046, 0.074, rim_t),
        ("l", -0.037, 0.0, rim_t, 0.098),
        ("r", 0.037, 0.0, rim_t, 0.098),
    ):
        rim = ic.box(
            f"{prefix}_shield_rim_{tag}", rsx, 0.016, rsz,
            sx + dx, sy, sz + dz, m.steel,
        )
        ic.parent_to(rim, panel)

    boss = ic.box(
        f"{prefix}_shield_boss", 0.030, 0.010, 0.030,
        sx, sy - 0.010, sz, m.red,
        rot_y=math.radians(45),
    )
    ic.parent_to(boss, panel)
    ic.parent_to(panel, arm_l)

    prefix_hierarchy(body, prefix)
    move_root(body, x, y, rot_z=rot_z)
    return body


def build_archer(prefix, x, y, rot_z=0.0):
    body, arm_l, arm_r = ic.build_body(
        m,
        arm_l_pitch=math.radians(-38),
        arm_r_pitch=math.radians(-6),
    )

    bx = -(ic.ARM_X + 0.010)
    by = -0.052
    limb = 0.085

    grip = ic.box(
        f"{prefix}_bow_grip", 0.012, 0.014, 0.034,
        bx, by, ic.HAND_Z + 0.010, m.wood,
    )
    limb_u = ic.box(
        f"{prefix}_bow_limb_u", 0.010, 0.011, limb,
        bx, by - 0.012, ic.HAND_Z + 0.010 + 0.017 + limb / 2,
        m.wood, rot_x=math.radians(-16),
    )
    limb_d = ic.box(
        f"{prefix}_bow_limb_d", 0.010, 0.011, limb,
        bx, by - 0.012, ic.HAND_Z + 0.010 - 0.017 - limb / 2,
        m.wood, rot_x=math.radians(16),
    )
    string = ic.box(
        f"{prefix}_bow_string", 0.004, 0.004, 0.196,
        bx, by + 0.021, ic.HAND_Z + 0.010, M_STRING,
    )
    for part in (limb_u, limb_d, string):
        ic.parent_to(part, grip)
    ic.parent_to(grip, arm_l)

    ax, ay = ic.ARM_X + 0.004, -0.030
    shaft = ic.box(
        f"{prefix}_arrow", 0.006, 0.120, 0.006,
        ax, ay, ic.HAND_Z + 0.002, m.wood,
    )
    head = ic.cone(
        f"{prefix}_arrow_head", 0.007, 0.001, 0.018,
        ax, ay - 0.069, ic.HAND_Z + 0.002, m.steel,
        verts=4, rot_x=math.radians(-90),
    )
    fletch = ic.box(
        f"{prefix}_arrow_fletch", 0.003, 0.024, 0.016,
        ax, ay + 0.052, ic.HAND_Z + 0.002, M_FLETCH,
    )
    ic.parent_to(head, shaft)
    ic.parent_to(fletch, shaft)
    ic.parent_to(shaft, arm_r)

    qx, qy = 0.026, 0.052
    quiver = ic.cylinder(
        f"{prefix}_quiver", 0.016, 0.095,
        qx, qy, 0.150, m.wood,
        verts=6, rot_x=math.radians(12),
    )
    for i, (dx, dz) in enumerate(((-0.007, 0.0), (0.008, 0.008))):
        qf = ic.box(
            f"{prefix}_quiver_fletch_{i}", 0.004, 0.016, 0.020,
            qx + dx, qy + 0.014, 0.205 + dz,
            M_FLETCH, rot_x=math.radians(12),
        )
        ic.parent_to(qf, quiver)
    ic.parent_to(quiver, body)

    prefix_hierarchy(body, prefix)
    move_root(body, x, y, rot_z=rot_z)
    return body


def build_cart_base(prefix):
    body = ic.box(f"{prefix}_body", 0.100, 0.130, 0.016, 0, 0.010, 0.062, m.wood)
    ic.bake_scale(body)

    for sx in (-1, 1):
        tag = "l" if sx < 0 else "r"
        rail = ic.box(
            f"{prefix}_rail_{tag}", 0.012, 0.165, 0.012,
            sx * 0.062, -0.005, 0.104, m.wood,
        )
        ic.parent_to(rail, body)

    wheel_r = 0.055
    for sx, tag in ((-1, "l"), (1, "r")):
        wheel = ic.cylinder(
            f"{prefix}_wheel_{tag}", wheel_r, 0.014,
            sx * 0.066, 0.030, wheel_r, m.wood,
            verts=10, rot_y=math.radians(90),
        )
        bake_rotation(wheel)
        ic.parent_to(wheel, body)

    axle = ic.cylinder(
        f"{prefix}_axle", 0.010, 0.150,
        0, 0.030, wheel_r, m.wood,
        verts=6, rot_y=math.radians(90),
    )
    bake_rotation(axle)
    ic.parent_to(axle, body)
    return body


def add_cart_flag(prefix, body):
    pole = ic.cylinder(
        f"{prefix}_flag_pole", 0.005, 0.115,
        -0.042, 0.058, 0.128, m.wood, verts=5,
    )
    flag = ic.box(
        f"{prefix}_flag", 0.040, 0.004, 0.024,
        -0.020, 0.058, 0.172, m.red,
    )
    ic.parent_to(flag, pole)
    ic.parent_to(pole, body)


def build_catapult(prefix, x, y):
    body = build_cart_base(prefix)

    pivot_y = 0.030
    pivot_z = 0.135
    for sx in (-1, 1):
        tag = "l" if sx < 0 else "r"
        frame = ic.box(
            f"{prefix}_frame_{tag}", 0.014, 0.020, 0.075,
            sx * 0.042, pivot_y, 0.1075, m.wood,
            rot_x=math.radians(8),
        )
        ic.parent_to(frame, body)

    crossbar = ic.cylinder(
        f"{prefix}_crossbar", 0.009, 0.100,
        0, pivot_y, pivot_z, m.wood,
        verts=6, rot_y=math.radians(90),
    )
    bake_rotation(crossbar)
    ic.parent_to(crossbar, body)

    arm_len = 0.150
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, pivot_y, pivot_z))
    arm = bpy.context.object
    arm.name = f"{prefix}_arm"
    arm.scale = (0.016, 0.020, arm_len)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    arm.data.transform(Matrix.Translation((0, 0, arm_len / 2)))
    arm.data.materials.append(m.wood)

    def arm_child_box(name, sx, sy, sz, off, mat):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, pivot_y, pivot_z))
        obj = bpy.context.object
        obj.name = f"{prefix}_{name}"
        obj.scale = (sx, sy, sz)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.transform(Matrix.Translation(off))
        obj.data.materials.append(mat)
        ic.parent_to(obj, arm)
        return obj

    tip_z = arm_len - 0.006
    arm_child_box("arm_basket_base", 0.056, 0.012, 0.052, (0, -0.016, tip_z), m.wood)
    arm_child_box("arm_basket_l", 0.010, 0.038, 0.052, (-0.028, -0.036, tip_z), m.wood)
    arm_child_box("arm_basket_r", 0.010, 0.038, 0.052, (0.028, -0.036, tip_z), m.wood)
    arm_child_box("arm_basket_end", 0.056, 0.038, 0.010, (0, -0.036, tip_z + 0.026), m.wood)

    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=7, ring_count=5, radius=0.020,
        location=(0, pivot_y, pivot_z),
    )
    stone = bpy.context.object
    stone.name = f"{prefix}_stone"
    stone.data.transform(Matrix.Translation((0, -0.040, tip_z)))
    stone.data.materials.append(M_STONE)
    ic.parent_to(stone, arm)

    arm_child_box("arm_weight", 0.052, 0.046, 0.040, (0, 0, -0.048), m.steel)
    arm.rotation_euler = (math.radians(-55), 0, 0)
    ic.parent_to(arm, body)

    add_cart_flag(prefix, body)
    move_root(body, x, y)
    return body


def build_thunder_cart(prefix, x, y):
    body = build_cart_base(prefix)

    for dy, h, tag in ((-0.038, 0.070, "f"), (0.052, 0.046, "b")):
        post = ic.box(
            f"{prefix}_support_{tag}", 0.062, 0.016, h,
            0, dy, 0.070 + h / 2, m.wood,
        )
        ic.parent_to(post, body)

    ram_pitch = math.radians(16)
    ram = ic.cylinder(
        f"{prefix}_ram", 0.034, 0.160,
        0, 0.005, 0.128, M_PENCIL,
        verts=6, rot_x=math.radians(90) - ram_pitch,
    )
    bake_rotation(ram)

    tip = ic.cone(
        f"{prefix}_ram_tip", 0.034, 0.002, 0.055,
        0, 0.005 - 0.103, 0.128 + 0.103 * math.tan(ram_pitch),
        m.steel, verts=6,
        rot_x=-math.radians(90) - ram_pitch,
    )
    bake_rotation(tip)
    ic.parent_to(tip, ram)
    ic.parent_to(ram, body)

    add_cart_flag(prefix, body)
    move_root(body, x, y)
    return body


def build_command_flag():
    x, y = 0.0, 0.095
    pole = ic.cylinder(
        "army_group_command_flag_pole", 0.008, 0.335,
        x, y, 0.1675, m.wood, verts=6,
    )
    flag = ic.box(
        "army_group_command_flag", 0.105, 0.006, 0.060,
        x + 0.050, y, 0.285, m.red,
    )
    trim = ic.box(
        "army_group_command_flag_trim", 0.112, 0.008, 0.010,
        x + 0.050, y - 0.002, 0.255, m.steel,
    )
    ic.parent_to(flag, pole)
    ic.parent_to(trim, pole)
    return pole


# 정면은 기존 병종과 동일하게 -Y.
#
#       [보병] [보병] [보병]
#          [궁병] [궁병]
#             [대군기]
#             [공성]

roots = []

roots.append(build_swordsman("front_l", -0.155, -0.155, rot_z=math.radians(-4)))
roots.append(build_swordsman("front_c",  0.000, -0.185, rot_z=0.0))
roots.append(build_swordsman("front_r",  0.155, -0.155, rot_z=math.radians(4)))

roots.append(build_archer("archer_l", -0.095, 0.005, rot_z=math.radians(-3)))
roots.append(build_archer("archer_r",  0.095, 0.005, rot_z=math.radians(3)))

roots.append(build_command_flag())

if SIEGE_VARIANT == "catapult":
    roots.append(build_catapult("siege", 0.0, 0.205))
elif SIEGE_VARIANT == "thunder_cart":
    roots.append(build_thunder_cart("siege", 0.0, 0.205))
else:
    raise ValueError(
        f"Unknown SIEGE_VARIANT={SIEGE_VARIANT!r}; use 'catapult' or 'thunder_cart'."
    )

army_root = bpy.data.objects.new("army_group_root", None)
bpy.context.collection.objects.link(army_root)

for root in roots:
    root.parent = army_root
    root.matrix_parent_inverse = army_root.matrix_world.inverted()

army_root["asset_type"] = "army_group"
army_root["siege_variant"] = SIEGE_VARIANT
army_root["formation"] = "3_infantry_2_archer_1_siege"
army_root["max_troops"] = 40000

ic.export(OUTPUT_FILE)
