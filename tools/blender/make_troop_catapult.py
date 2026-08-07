# 병종 5 — 투석기(Catapult, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_catapult.py
#
# doc/design-unit.md: [육지, 속도 1, 탐지 1, 사거리 3/2/1] 투석기를 끌고 있다.
# 벽력거와 같은 수레·바퀴·crew 규약을 재사용하고, 말뚝 대신 던지는 팔을 얹는다.
#   arm       : 던지는 팔(원점=굴대). rotation.x가 음수로 갈수록 뒤로 눕는다
#               — Blender Rx(+θ)는 팔을 앞(-Y)으로 기울인다. 대기 자세는 -55도(뒤)
#   arm_basket_* / stone : 팔 끝 바구니와 돌(팔 자식)
#
# 팔 자식은 팔이 회전하기 전(identity 상태)에 부모로 묶는다. 회전 후에 묶으면
# parent-inverse 보정 때문에 메시 오프셋이 팔 축이 아니라 월드 축을 따라간다.
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

# ── 던지는 팔: identity 상태로 만들고 자식까지 붙인 뒤에 눕힌다 ──
ARM_LEN = 0.150
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, PIVOT_Y, PIVOT_Z))
arm = bpy.context.object
arm.name = "arm"
arm.scale = (0.016, 0.020, ARM_LEN)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
arm.data.transform(Matrix.Translation((0, 0, ARM_LEN / 2)))
arm.data.materials.append(m.wood)


def arm_child_box(name, sx, sy, sz, off, mat):
    """팔 자식 상자. 스케일을 먼저 구워 메시 오프셋이 스케일에 곱해지지 않게 한다."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, PIVOT_Y, PIVOT_Z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.transform(Matrix.Translation(off))
    o.data.materials.append(mat)
    ic.parent_to(o, arm)
    return o


# 바구니: 바닥 + 옆벽 2 + 끝벽. 막대 윗면(-Y 로컬 = 대기 자세에서 위·앞)에 얹힌다
TIP = ARM_LEN - 0.006
arm_child_box("arm_basket_base", 0.056, 0.012, 0.052, (0, -0.016, TIP), m.wood)
arm_child_box("arm_basket_l", 0.010, 0.038, 0.052, (-0.028, -0.036, TIP), m.wood)
arm_child_box("arm_basket_r", 0.010, 0.038, 0.052, (0.028, -0.036, TIP), m.wood)
arm_child_box("arm_basket_end", 0.056, 0.038, 0.010, (0, -0.036, TIP + 0.026), m.wood)

# 돌(팔 자식, 바구니 안)
bpy.ops.mesh.primitive_uv_sphere_add(segments=7, ring_count=5, radius=0.020,
                                     location=(0, PIVOT_Y, PIVOT_Z))
stone = bpy.context.object
stone.name = "stone"
stone.data.transform(Matrix.Translation((0, -0.040, TIP)))
stone.data.materials.append(M_STONE)
ic.parent_to(stone, arm)

# 평형추(팔 반대쪽 짧은 끝)
arm_child_box("arm_weight", 0.052, 0.046, 0.040, (0, 0, -0.048), m.steel)

# 자식이 다 붙은 뒤에 눕힌다 — Rx 음수가 뒤(+Y). 대기 자세 뒤로 55도
arm.rotation_euler = (math.radians(-55), 0, 0)
ic.parent_to(arm, body)

# ── 끄는 병사 2 — 병기가 커 보이도록 20% 줄여 세운다 ──
for i, sx in enumerate((-1, 1)):
    ic.build_siege_crew(m, body, i, sx)

ic.export("troop-catapult.glb")
