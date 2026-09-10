# 보급부대(Supply Unit, 저폴리) 생성 → GLB 익스포트
#
# 실행:
#   blender --background --python-exit-code 1 --python make_troop_supply.py
#
# 출력:
#   SanguoSLG.Game/assets/models/troop-supply.glb
#
# 상태:
#   IDLE    1~24   : 보병 2명 + A형 군막 천막 1개
#   MOVE   25~48  : 보병 1명 + 보급 수레 1대
#   ATTACK 49~72  : 정지 상태 보병 2명이 활 발사
#   COUNTER 73~96 : ATTACK과 동일한 화살 발사
#
# 수정사항:
# - 수레 바퀴 방향 보정
# - 바퀴 초기 90도 회전을 메시 방향으로 Apply 후 로컬 X 회전
# - -Y 전진 기준 바퀴 회전 0 -> -360도
# - 천막을 박스+원뿔 형태에서 A형 군막으로 변경
# - Blender 4.4+ Action API 호환을 위해 fcurves 직접 접근 없음

import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import infantry_common as ic

OUTPUT_FILE = "troop-supply.glb"

FPS = 24
FRAME_START = 1
FRAME_END = 96

IDLE_START, IDLE_END = 1, 24
MOVE_START, MOVE_END = 25, 48
ATTACK_START, ATTACK_END = 49, 72
COUNTER_START, COUNTER_END = 73, 96

HIDDEN = 0.001
VISIBLE = 1.0

bpy.ops.wm.read_factory_settings(use_empty=True)

scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start = FRAME_START
scene.frame_end = FRAME_END

m = ic.Mats()

M_CANVAS = ic.make_mat(
    "supply_canvas",
    (0.54, 0.45, 0.30),
    roughness=0.95,
)

M_CANVAS_DARK = ic.make_mat(
    "supply_canvas_dark",
    (0.34, 0.27, 0.18),
    roughness=0.95,
)

M_GRAIN = ic.make_mat(
    "supply_grain_bag",
    (0.50, 0.41, 0.27),
    roughness=0.95,
)

M_ROPE = ic.make_mat(
    "supply_rope",
    (0.33, 0.25, 0.16),
    roughness=0.95,
)

M_STRING = ic.make_mat(
    "supply_bow_string",
    (0.85, 0.82, 0.72),
    roughness=0.60,
)

M_FLETCH = ic.make_mat(
    "supply_fletch",
    (0.88, 0.88, 0.86),
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)

    if parent is not None:
        ic.parent_to(obj, parent)

    return obj


def prefix_hierarchy(root, prefix):
    stack = [root]

    while stack:
        obj = stack.pop()

        if not obj.name.startswith(prefix + "_"):
            obj.name = f"{prefix}_{obj.name}"

        stack.extend(list(obj.children))


def move_root(root, x, y, z=0.0, rot_z=0.0):
    root.location.x += x
    root.location.y += y
    root.location.z += z
    root.rotation_euler.z += rot_z


def key_scale(obj, frame, value):
    obj.scale = (value, value, value)
    obj.keyframe_insert(
        data_path="scale",
        frame=frame,
    )


def key_loc(obj, frame, x=None, y=None, z=None):
    loc = list(obj.location)

    if x is not None:
        loc[0] = x
    if y is not None:
        loc[1] = y
    if z is not None:
        loc[2] = z

    obj.location = loc
    obj.keyframe_insert(
        data_path="location",
        frame=frame,
    )


def key_rot(obj, frame, x=None, y=None, z=None):
    rot = list(obj.rotation_euler)

    if x is not None:
        rot[0] = x
    if y is not None:
        rot[1] = y
    if z is not None:
        rot[2] = z

    obj.rotation_euler = rot
    obj.keyframe_insert(
        data_path="rotation_euler",
        frame=frame,
    )


def apply_rotation_scale(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.object.transform_apply(
        location=False,
        rotation=True,
        scale=True,
    )


def mesh_object(name, verts, faces, material, parent=None):
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    if material is not None:
        obj.data.materials.append(material)

    if parent is not None:
        ic.parent_to(obj, parent)

    return obj


# ---------------------------------------------------------------------------
# Soldiers
# ---------------------------------------------------------------------------

def build_guard(prefix, x, y):
    body, arm_l, arm_r = ic.build_body(
        m,
        arm_l_pitch=math.radians(-12),
        arm_r_pitch=math.radians(8),
    )

    shaft = ic.cylinder(
        f"{prefix}_spear",
        0.006,
        0.205,
        ic.ARM_X + 0.006,
        -0.018,
        0.112,
        m.wood,
        verts=6,
        rot_x=math.radians(-8),
    )

    tip = ic.cone(
        f"{prefix}_spear_tip",
        0.010,
        0.001,
        0.028,
        ic.ARM_X + 0.006,
        -0.046,
        0.215,
        m.steel,
        verts=4,
        rot_x=math.radians(-8),
    )

    ic.parent_to(tip, shaft)
    ic.parent_to(shaft, arm_r)

    prefix_hierarchy(body, prefix)
    move_root(body, x, y)

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
        f"{prefix}_bow_grip",
        0.012,
        0.014,
        0.034,
        bx,
        by,
        ic.HAND_Z + 0.010,
        m.wood,
    )

    limb_u = ic.box(
        f"{prefix}_bow_limb_u",
        0.010,
        0.011,
        limb,
        bx,
        by - 0.012,
        ic.HAND_Z + 0.010 + 0.017 + limb / 2,
        m.wood,
        rot_x=math.radians(-16),
    )

    limb_d = ic.box(
        f"{prefix}_bow_limb_d",
        0.010,
        0.011,
        limb,
        bx,
        by - 0.012,
        ic.HAND_Z + 0.010 - 0.017 - limb / 2,
        m.wood,
        rot_x=math.radians(16),
    )

    string = ic.box(
        f"{prefix}_bow_string",
        0.004,
        0.004,
        0.196,
        bx,
        by + 0.021,
        ic.HAND_Z + 0.010,
        M_STRING,
    )

    for part in (limb_u, limb_d, string):
        ic.parent_to(part, grip)

    ic.parent_to(grip, arm_l)

    ax = ic.ARM_X + 0.004
    ay = -0.030

    arrow = ic.box(
        f"{prefix}_held_arrow",
        0.006,
        0.120,
        0.006,
        ax,
        ay,
        ic.HAND_Z + 0.002,
        m.wood,
    )

    arrow_head = ic.cone(
        f"{prefix}_held_arrow_head",
        0.007,
        0.001,
        0.018,
        ax,
        ay - 0.069,
        ic.HAND_Z + 0.002,
        m.steel,
        verts=4,
        rot_x=math.radians(-90),
    )

    arrow_fletch = ic.box(
        f"{prefix}_held_arrow_fletch",
        0.003,
        0.024,
        0.016,
        ax,
        ay + 0.052,
        ic.HAND_Z + 0.002,
        M_FLETCH,
    )

    ic.parent_to(arrow_head, arrow)
    ic.parent_to(arrow_fletch, arrow)
    ic.parent_to(arrow, arm_r)

    quiver = ic.cylinder(
        f"{prefix}_quiver",
        0.016,
        0.095,
        0.026,
        0.052,
        0.150,
        m.wood,
        verts=6,
        rot_x=math.radians(12),
    )

    for i, (dx, dz) in enumerate(
        (
            (-0.007, 0.000),
            (0.008, 0.008),
        )
    ):
        qf = ic.box(
            f"{prefix}_quiver_fletch_{i}",
            0.004,
            0.016,
            0.020,
            0.026 + dx,
            0.066,
            0.205 + dz,
            M_FLETCH,
            rot_x=math.radians(12),
        )

        ic.parent_to(qf, quiver)

    ic.parent_to(quiver, body)

    prefix_hierarchy(body, prefix)
    move_root(body, x, y, rot_z=rot_z)

    return body, arm_l, arm_r


# ---------------------------------------------------------------------------
# Supply cart
# ---------------------------------------------------------------------------

def build_supply_cart(parent):
    cart = empty("supply_cart", parent)

    bed = ic.box(
        "supply_cart_bed",
        0.115,
        0.155,
        0.022,
        0.050,
        0.030,
        0.078,
        m.wood,
    )
    ic.parent_to(bed, cart)

    for side in (-1, 1):
        rail = ic.box(
            f"supply_cart_rail_{'L' if side < 0 else 'R'}",
            0.015,
            0.160,
            0.060,
            0.050 + side * 0.105,
            0.030,
            0.116,
            m.wood,
        )
        ic.parent_to(rail, cart)

    for y, tag in (
        (-0.125, "front"),
        (0.185, "rear"),
    ):
        panel = ic.box(
            f"supply_cart_{tag}_panel",
            0.105,
            0.018,
            0.065,
            0.050,
            y,
            0.115,
            m.wood,
        )
        ic.parent_to(panel, cart)

    wheels = []

    for side in (-1, 1):
        wheel = ic.cylinder(
            f"supply_cart_wheel_{'L' if side < 0 else 'R'}",
            0.075,
            0.022,
            0.050 + side * 0.128,
            0.035,
            0.075,
            m.wood,
            verts=10,
            rot_y=math.radians(90),
        )

        # 중요:
        # 최초 90도 회전을 메시 자체에 적용해서
        # 애니메이션 중에는 X축 회전만 남긴다.
        apply_rotation_scale(wheel)

        ic.parent_to(wheel, cart)
        wheels.append(wheel)

    axle = ic.cylinder(
        "supply_cart_axle",
        0.010,
        0.285,
        0.050,
        0.035,
        0.075,
        m.wood,
        verts=6,
        rot_y=math.radians(90),
    )

    apply_rotation_scale(axle)
    ic.parent_to(axle, cart)

    for side in (-1, 1):
        pole = ic.box(
            f"supply_cart_pull_{'L' if side < 0 else 'R'}",
            0.012,
            0.205,
            0.012,
            0.050 + side * 0.075,
            -0.195,
            0.075,
            m.wood,
            rot_x=math.radians(6),
        )
        ic.parent_to(pole, cart)

    # 보급상자
    for i, (x, y, z, sx, sy, sz) in enumerate(
        (
            (0.020, 0.000, 0.120, 0.060, 0.052, 0.040),
            (0.095, 0.055, 0.118, 0.050, 0.045, 0.038),
            (0.030, 0.095, 0.165, 0.052, 0.040, 0.032),
        )
    ):
        crate = ic.box(
            f"supply_crate_{i}",
            sx,
            sy,
            sz,
            x,
            y,
            z,
            m.wood,
        )
        ic.parent_to(crate, cart)

        band = ic.box(
            f"supply_crate_band_{i}",
            sx * 1.05,
            0.007,
            sz * 1.10,
            x,
            y - sy * 0.55,
            z,
            m.red,
        )
        ic.parent_to(band, crate)

    # 보급 자루
    for i, (x, y, z) in enumerate(
        (
            (-0.025, 0.085, 0.120),
            (0.100, -0.025, 0.170),
        )
    ):
        sack = ic.cone(
            f"supply_sack_{i}",
            0.040,
            0.032,
            0.075,
            x,
            y,
            z,
            M_GRAIN,
            verts=7,
        )
        ic.parent_to(sack, cart)

    # 군기
    flag_pole = ic.cylinder(
        "supply_cart_flag_pole",
        0.005,
        0.210,
        -0.045,
        0.130,
        0.195,
        m.wood,
        verts=5,
    )
    ic.parent_to(flag_pole, cart)

    flag = ic.box(
        "supply_cart_flag",
        0.060,
        0.006,
        0.035,
        -0.012,
        0.130,
        0.252,
        m.red,
    )
    ic.parent_to(flag, flag_pole)

    return cart, wheels


# ---------------------------------------------------------------------------
# A-frame military tent
# ---------------------------------------------------------------------------

def build_tent(parent):
    tent = empty("supply_tent", parent)

    width = 0.34
    length = 0.34
    wall_z = 0.045
    ridge_z = 0.245

    x_l = -width / 2
    x_r = width / 2
    y_front = -length / 2
    y_back = length / 2

    # 좌측/우측 천막 천
    # A형 지붕의 양 경사면을 각각 하나의 quad로 만든다.
    left_verts = [
        (x_l, y_front, wall_z),
        (x_l, y_back, wall_z),
        (0.0, y_back, ridge_z),
        (0.0, y_front, ridge_z),
    ]
    left_faces = [(0, 1, 2, 3)]

    mesh_object(
        "supply_tent_canvas_left",
        left_verts,
        left_faces,
        M_CANVAS,
        tent,
    )

    right_verts = [
        (0.0, y_front, ridge_z),
        (0.0, y_back, ridge_z),
        (x_r, y_back, wall_z),
        (x_r, y_front, wall_z),
    ]
    right_faces = [(0, 1, 2, 3)]

    mesh_object(
        "supply_tent_canvas_right",
        right_verts,
        right_faces,
        M_CANVAS,
        tent,
    )

    # 뒤쪽 삼각면
    back_verts = [
        (x_l, y_back, wall_z),
        (x_r, y_back, wall_z),
        (0.0, y_back, ridge_z),
    ]
    back_faces = [(0, 1, 2)]

    mesh_object(
        "supply_tent_back",
        back_verts,
        back_faces,
        M_CANVAS_DARK,
        tent,
    )

    # 앞쪽은 완전히 막지 않고
    # 좌우 플랩을 만들어 입구가 보이게 한다.
    flap_left_verts = [
        (x_l, y_front, wall_z),
        (-0.035, y_front - 0.005, wall_z),
        (0.0, y_front, ridge_z),
    ]
    flap_left_faces = [(0, 1, 2)]

    mesh_object(
        "supply_tent_front_flap_left",
        flap_left_verts,
        flap_left_faces,
        M_CANVAS,
        tent,
    )

    flap_right_verts = [
        (0.035, y_front - 0.005, wall_z),
        (x_r, y_front, wall_z),
        (0.0, y_front, ridge_z),
    ]
    flap_right_faces = [(0, 1, 2)]

    mesh_object(
        "supply_tent_front_flap_right",
        flap_right_verts,
        flap_right_faces,
        M_CANVAS,
        tent,
    )

    # 입구 안쪽을 어둡게 만들어 천막 깊이감
    entrance = ic.box(
        "supply_tent_entrance_dark",
        0.030,
        0.010,
        0.075,
        0.0,
        y_front + 0.012,
        0.085,
        M_CANVAS_DARK,
    )
    ic.parent_to(entrance, tent)

    # 앞/뒤 중앙 지지대
    for y, tag in (
        (y_front, "front"),
        (y_back, "back"),
    ):
        pole = ic.cylinder(
            f"supply_tent_pole_{tag}",
            0.006,
            ridge_z,
            0.0,
            y,
            ridge_z / 2,
            m.wood,
            verts=6,
        )
        ic.parent_to(pole, tent)

    # 천막 능선 막대
    ridge = ic.cylinder(
        "supply_tent_ridge_pole",
        0.005,
        length + 0.06,
        0.0,
        0.0,
        ridge_z,
        m.wood,
        verts=6,
        rot_x=math.radians(90),
    )
    ic.parent_to(ridge, tent)

    # 네 모서리 말뚝
    stake_positions = [
        (x_l - 0.035, y_front - 0.035),
        (x_r + 0.035, y_front - 0.035),
        (x_l - 0.035, y_back + 0.035),
        (x_r + 0.035, y_back + 0.035),
    ]

    for i, (sx, sy) in enumerate(stake_positions):
        stake = ic.cylinder(
            f"supply_tent_stake_{i}",
            0.004,
            0.050,
            sx,
            sy,
            0.025,
            m.wood,
            verts=5,
        )
        ic.parent_to(stake, tent)

    # 앞쪽 밧줄 2개
    # 얇은 박스로 단순화
    rope_l = ic.box(
        "supply_tent_rope_L",
        0.004,
        0.100,
        0.004,
        -0.100,
        y_front - 0.040,
        0.080,
        M_ROPE,
        rot_x=math.radians(-50),
    )
    ic.parent_to(rope_l, tent)

    rope_r = ic.box(
        "supply_tent_rope_R",
        0.004,
        0.100,
        0.004,
        0.100,
        y_front - 0.040,
        0.080,
        M_ROPE,
        rot_x=math.radians(-50),
    )
    ic.parent_to(rope_r, tent)

    # 세력기
    flag_pole = ic.cylinder(
        "supply_tent_flag_pole",
        0.005,
        0.180,
        0.0,
        y_back - 0.010,
        ridge_z + 0.075,
        m.wood,
        verts=5,
    )
    ic.parent_to(flag_pole, tent)

    flag = ic.box(
        "supply_tent_flag",
        0.050,
        0.006,
        0.032,
        0.030,
        y_back - 0.010,
        ridge_z + 0.140,
        m.red,
    )
    ic.parent_to(flag, flag_pole)

    # 천막 주변 보급상자
    for i, (x, y) in enumerate(
        (
            (-0.235, 0.050),
            (0.235, 0.070),
        )
    ):
        crate = ic.box(
            f"supply_camp_crate_{i}",
            0.050,
            0.050,
            0.040,
            x,
            y,
            0.040,
            m.wood,
        )
        ic.parent_to(crate, tent)

    return tent


# ---------------------------------------------------------------------------
# Projectile
# ---------------------------------------------------------------------------

def build_projectile_arrow(name, parent, x):
    root = empty(name, parent)

    shaft = ic.box(
        f"{name}_shaft",
        0.006,
        0.130,
        0.006,
        x,
        -0.120,
        0.150,
        m.wood,
    )

    head = ic.cone(
        f"{name}_head",
        0.008,
        0.001,
        0.022,
        x,
        -0.195,
        0.150,
        m.steel,
        verts=4,
        rot_x=math.radians(-90),
    )

    fletch = ic.box(
        f"{name}_fletch",
        0.004,
        0.025,
        0.018,
        x,
        -0.060,
        0.150,
        M_FLETCH,
    )

    for obj in (shaft, head, fletch):
        ic.parent_to(obj, root)

    return root


# ---------------------------------------------------------------------------
# Build root
# ---------------------------------------------------------------------------

root = empty("troop_supply_root")

root["asset_type"] = "troop"
root["troop_type"] = "supply"
root["front_axis"] = "-Y"

root["idle_start"] = IDLE_START
root["idle_end"] = IDLE_END
root["move_start"] = MOVE_START
root["move_end"] = MOVE_END
root["attack_start"] = ATTACK_START
root["attack_end"] = ATTACK_END
root["counter_start"] = COUNTER_START
root["counter_end"] = COUNTER_END

move_group = empty("state_move", root)
camp_group = empty("state_camp", root)
projectile_group = empty("projectiles", root)

# MOVE: 보병 + 수레
move_guard = build_guard(
    "move_guard",
    -0.175,
    -0.015,
)
ic.parent_to(move_guard, move_group)

cart, cart_wheels = build_supply_cart(move_group)

# IDLE / ATTACK / COUNTER: 보병 2 + 천막
build_tent(camp_group)

archer_l, archer_l_arm_l, archer_l_arm_r = build_archer(
    "camp_archer_l",
    -0.215,
    -0.165,
    rot_z=math.radians(-5),
)

archer_r, archer_r_arm_l, archer_r_arm_r = build_archer(
    "camp_archer_r",
    0.215,
    -0.165,
    rot_z=math.radians(5),
)

ic.parent_to(archer_l, camp_group)
ic.parent_to(archer_r, camp_group)

shot_l = build_projectile_arrow(
    "shot_arrow_l",
    projectile_group,
    -0.215,
)

shot_r = build_projectile_arrow(
    "shot_arrow_r",
    projectile_group,
    0.215,
)


# ---------------------------------------------------------------------------
# State switching
# ---------------------------------------------------------------------------

for start, end, move_visible, camp_visible in (
    (IDLE_START, IDLE_END, HIDDEN, VISIBLE),
    (MOVE_START, MOVE_END, VISIBLE, HIDDEN),
    (ATTACK_START, ATTACK_END, HIDDEN, VISIBLE),
    (COUNTER_START, COUNTER_END, HIDDEN, VISIBLE),
):
    key_scale(move_group, start, move_visible)
    key_scale(move_group, end, move_visible)

    key_scale(camp_group, start, camp_visible)
    key_scale(camp_group, end, camp_visible)


for arrow in (shot_l, shot_r):
    key_scale(
        arrow,
        FRAME_START,
        HIDDEN,
    )


# ---------------------------------------------------------------------------
# IDLE
# ---------------------------------------------------------------------------

for frame, z in (
    (IDLE_START, 0.000),
    (12, 0.005),
    (IDLE_END, 0.000),
):
    key_loc(
        camp_group,
        frame,
        z=z,
    )


# ---------------------------------------------------------------------------
# MOVE
# ---------------------------------------------------------------------------

move_leg_l = bpy.data.objects.get(
    "move_guard_leg_l"
)

move_leg_r = bpy.data.objects.get(
    "move_guard_leg_r"
)

if move_leg_l and move_leg_r:
    for frame, ldeg, rdeg in (
        (MOVE_START, 22, -22),
        (31, -22, 22),
        (37, 22, -22),
        (43, -22, 22),
        (MOVE_END, 22, -22),
    ):
        key_rot(
            move_leg_l,
            frame,
            x=math.radians(ldeg),
        )

        key_rot(
            move_leg_r,
            frame,
            x=math.radians(rdeg),
        )


# 바퀴:
# -Y 방향으로 이동한다고 가정
# 0 -> -360도 회전
for wheel in cart_wheels:
    for frame, deg in (
        (MOVE_START, 0),
        (31, -90),
        (37, -180),
        (43, -270),
        (MOVE_END, -360),
    ):
        key_rot(
            wheel,
            frame,
            x=math.radians(deg),
        )


for frame, z in (
    (MOVE_START, 0.000),
    (31, 0.005),
    (37, 0.000),
    (43, 0.005),
    (MOVE_END, 0.000),
):
    key_loc(
        move_group,
        frame,
        z=z,
    )


# ---------------------------------------------------------------------------
# Attack / counter
# ---------------------------------------------------------------------------

def animate_bow_shot(
    start,
    end,
    arms,
    arrows,
):
    f0 = start
    f_draw = start + 7
    f_release = start + 12
    f_fly = start + 17

    for arm_l, arm_r in arms:
        key_rot(
            arm_l,
            f0,
            x=math.radians(-38),
            z=0.0,
        )

        key_rot(
            arm_l,
            f_draw,
            x=math.radians(-52),
            z=math.radians(-5),
        )

        key_rot(
            arm_l,
            f_release,
            x=math.radians(-44),
            z=math.radians(4),
        )

        key_rot(
            arm_l,
            end,
            x=math.radians(-38),
            z=0.0,
        )

        key_rot(
            arm_r,
            f0,
            x=math.radians(-6),
            z=0.0,
        )

        key_rot(
            arm_r,
            f_draw,
            x=math.radians(28),
            z=math.radians(18),
        )

        key_rot(
            arm_r,
            f_release,
            x=math.radians(-24),
            z=math.radians(-6),
        )

        key_rot(
            arm_r,
            end,
            x=math.radians(-6),
            z=0.0,
        )

    for arrow in arrows:
        key_scale(
            arrow,
            f0,
            HIDDEN,
        )

        key_scale(
            arrow,
            f_draw,
            HIDDEN,
        )

        key_scale(
            arrow,
            f_release,
            VISIBLE,
        )

        key_loc(
            arrow,
            f_release,
            x=0.0,
            y=0.0,
            z=0.0,
        )

        key_loc(
            arrow,
            f_fly,
            x=0.0,
            y=-0.85,
            z=0.02,
        )

        key_scale(
            arrow,
            f_fly + 1,
            HIDDEN,
        )

        key_scale(
            arrow,
            end,
            HIDDEN,
        )


animate_bow_shot(
    ATTACK_START,
    ATTACK_END,
    (
        (
            archer_l_arm_l,
            archer_l_arm_r,
        ),
        (
            archer_r_arm_l,
            archer_r_arm_r,
        ),
    ),
    (
        shot_l,
        shot_r,
    ),
)

animate_bow_shot(
    COUNTER_START,
    COUNTER_END,
    (
        (
            archer_l_arm_l,
            archer_l_arm_r,
        ),
        (
            archer_r_arm_l,
            archer_r_arm_r,
        ),
    ),
    (
        shot_l,
        shot_r,
    ),
)


# ---------------------------------------------------------------------------
# Timeline markers
# ---------------------------------------------------------------------------

for name, frame in (
    ("IDLE", IDLE_START),
    ("MOVE", MOVE_START),
    ("ATTACK", ATTACK_START),
    ("COUNTER", COUNTER_START),
):
    scene.timeline_markers.new(
        name,
        frame=frame,
    )


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

output_path = os.path.join(
    ic.MODEL_DIR,
    OUTPUT_FILE,
)

os.makedirs(
    ic.MODEL_DIR,
    exist_ok=True,
)

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
        export_nla_strips_merged_animation_name="SupplyUnit",
        export_force_sampling=True,
    )
except TypeError:
    bpy.ops.export_scene.gltf(
        **export_kwargs,
    )


print("")
print("==============================================")
print(" Supply Unit Export Complete")
print("==============================================")
print("OUTPUT  :", output_path)
print("IDLE    :", IDLE_START, "-", IDLE_END)
print("MOVE    :", MOVE_START, "-", MOVE_END)
print("ATTACK  :", ATTACK_START, "-", ATTACK_END)
print("COUNTER :", COUNTER_START, "-", COUNTER_END)
print("==============================================")
