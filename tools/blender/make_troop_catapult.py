# 병종 5 — 투석기(Catapult, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_catapult.py
#
# doc/design-unit.md: [육지, 속도 1, 탐지 1, 사거리 3/2/1] 투석기를 끌고 있다.
# 벽력거와 같은 수레·바퀴·crew 규약을 재사용하고, 말뚝 대신 던지는 팔을 얹는다.
#   arm       : 던지는 팔(원점=굴대). 공격 때 뒤로 젖혔다 튕겨 오른다
#   arm_spoon : 팔 끝 바구니(팔 자식)
#   stone     : 바구니에 담긴 돌(팔 자식). 발사 순간 숨기고 발사체로 잇는다
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic
from mathutils import Matrix

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_STONE = ic.make_mat("stone", (0.52, 0.52, 0.50))


def bake_rotation(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


# ── 수레 바닥(부모) — 벽력거와 동일. 스케일을 구워 자식 회전의 전단을 막는다 ──
body = ic.box("body", 0.100, 0.130, 0.016, 0, 0.010, 0.062, m.wood)
ic.bake_scale(body)
for sx in (-1, 1):
    rail = ic.box(f"rail_{'l' if sx < 0 else 'r'}", 0.012, 0.165, 0.012,
                  sx * 0.062, -0.005, 0.104, m.wood)
    ic.parent_to(rail, body)

WHEEL_R = 0.055
for sx, tag in ((-1, "l"), (1, "r")):
    wheel = ic.cylinder(f"wheel_{tag}", WHEEL_R, 0.014, sx * 0.066, 0.030, WHEEL_R,
                        m.wood, verts=10, rot_y=math.radians(90))
    bake_rotation(wheel)
    ic.parent_to(wheel, body)
axle = ic.cylinder("axle", 0.010, 0.150, 0, 0.030, WHEEL_R, m.wood,
                   verts=6, rot_y=math.radians(90))
bake_rotation(axle)
ic.parent_to(axle, body)

# ── A자 지지대 2 + 굴대 ──
PIVOT_Y = 0.030
PIVOT_Z = 0.135
for sx in (-1, 1):
    frame = ic.box(f"frame_{'l' if sx < 0 else 'r'}", 0.014, 0.020, 0.075,
                   sx * 0.042, PIVOT_Y, 0.070 + 0.0375, m.wood, rot_x=math.radians(8))
    ic.parent_to(frame, body)
crossbar = ic.cylinder("crossbar", 0.009, 0.100, 0, PIVOT_Y, PIVOT_Z, m.wood,
                       verts=6, rot_y=math.radians(90))
bake_rotation(crossbar)
ic.parent_to(crossbar, body)

# ── 던지는 팔: 원점을 굴대에 두고 뒤로 눕혀 둔다(대기 자세 55도).
#    메시를 원점에서 위로 뻗게 만들어 rotation.x만으로 젖힘·발사가 된다 ──
ARM_LEN = 0.150
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, PIVOT_Y, PIVOT_Z))
arm = bpy.context.object
arm.name = "arm"
arm.scale = (0.016, 0.020, ARM_LEN)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
arm.data.transform(Matrix.Translation((0, 0, ARM_LEN / 2)))
arm.data.materials.append(m.wood)
arm.rotation_euler = (math.radians(55), 0, 0)
ic.parent_to(arm, body)

# 팔 끝 바구니 + 돌 — 팔 로컬 좌표(원점=굴대, +Z가 팔 끝 방향)
spoon = ic.box("arm_spoon", 0.046, 0.040, 0.016, 0, PIVOT_Y, PIVOT_Z, m.wood)
spoon.data.transform(Matrix.Translation((0, 0.012, ARM_LEN - 0.008)))
ic.parent_to(spoon, arm)
bpy.ops.mesh.primitive_uv_sphere_add(segments=7, ring_count=5, radius=0.020,
                                     location=(0, PIVOT_Y, PIVOT_Z))
stone = bpy.context.object
stone.name = "stone"
stone.data.transform(Matrix.Translation((0, 0.030, ARM_LEN - 0.005)))
stone.data.materials.append(M_STONE)
ic.parent_to(stone, arm)

# 평형추(팔 반대쪽 짧은 끝)
weight = ic.box("arm_weight", 0.052, 0.046, 0.040, 0, PIVOT_Y, PIVOT_Z, m.steel)
weight.data.transform(Matrix.Translation((0, 0, -0.045)))
ic.parent_to(weight, arm)

# ── 끄는 병사 2 — 벽력거와 동일 ──
for i, sx in enumerate((-1, 1)):
    cx = sx * 0.085
    cy = -0.040
    torso = ic.cone(f"crew{i}_torso", 0.030, 0.038, 0.058, cx, cy, 0.133, m.armor, smooth=True)
    head = ic.box(f"crew{i}_head", 0.040, 0.038, 0.036, cx, cy, 0.184, m.skin)
    helmet = ic.cone(f"crew{i}_helmet", 0.027, 0.010, 0.022, cx, cy, 0.210, m.armor, verts=6)
    for tag, lx in (("l", -0.020), ("r", 0.020)):
        leg = ic.box(f"crew{i}_leg_{tag}", 0.024, 0.026, ic.HIP_Z, cx + lx, cy, ic.HIP_Z,
                     m.cloth, origin_shift=(0, 0, -ic.HIP_Z / 2))
        foot = ic.box(f"crew{i}_foot_{tag}", 0.026, 0.044, 0.014, cx + lx, cy - 0.008, 0.007, m.armor)
        ic.parent_to(foot, leg)
        ic.parent_to(leg, torso)
    arm_c = ic.box(f"crew{i}_arm", 0.020, 0.022, 0.055, cx - sx * 0.022, cy + 0.018, 0.135,
                   m.armor, rot_x=math.radians(-55))
    for part in (head, helmet, arm_c):
        ic.parent_to(part, torso)
    ic.parent_to(torso, body)

ic.export("troop-catapult.glb")
