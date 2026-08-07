# 병종 6 — 공성탑(SiegeTower, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_siege_tower.py
#
# doc/design-unit.md: [육지, 속도 1, 탐지 1, 사거리 3/2/1]
# 모습(사용자 확정, 2026-08-06): 네모난 박스에 바퀴가 있고, 그 위에 궁병이 서 있다.
# 궁병은 박스 안에 들어가도록 작게(0.6배) 만든다.
#
# 부위 노드 규약:
#   body (부모=탑 상자) ← 바퀴·난간·궁병 전부 자식
#   wheel_fl/fr/bl/br : 모서리 바퀴 4개(회전을 메시에 구움)
#   tower_archer      : 탑 위 궁병 몸통(공성탑 판별 마커). ta_* 부위가 자식
#   ta_arm_l(활)/ta_arm_r(시위)/ta_arrow : 사격 모션 대상
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_STRING = ic.make_mat("string", (0.85, 0.82, 0.72), roughness=0.6)
M_PLANK = ic.make_mat("plank", (0.30, 0.19, 0.11))


def bake_rotation(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


# ── 탑 상자(부모). 스케일을 구워 자식 회전의 전단을 막는다 ──
BOX_W = 0.115
BOX_BOTTOM = 0.045
BOX_TOP = 0.195
body = ic.box("body", BOX_W, BOX_W, BOX_TOP - BOX_BOTTOM, 0, 0, (BOX_TOP + BOX_BOTTOM) / 2, m.wood)
ic.bake_scale(body)

# 모서리 기둥 4 + 꼭대기 난간(패러핏)
for sx in (-1, 1):
    for sy in (-1, 1):
        post = ic.box(f"post_{sx}_{sy}", 0.015, 0.015, BOX_TOP - BOX_BOTTOM + 0.012,
                      sx * (BOX_W / 2 - 0.004), sy * (BOX_W / 2 - 0.004),
                      (BOX_TOP + BOX_BOTTOM) / 2 + 0.006, M_PLANK)
        ic.parent_to(post, body)
for i, (dx, dy, sx, sy) in enumerate(((0, 1, BOX_W, 0.012), (0, -1, BOX_W, 0.012),
                                      (1, 0, 0.012, BOX_W), (-1, 0, 0.012, BOX_W))):
    rim = ic.box(f"parapet_{i}", sx, sy, 0.030,
                 dx * (BOX_W / 2 - 0.006), dy * (BOX_W / 2 - 0.006), BOX_TOP + 0.015, M_PLANK)
    ic.parent_to(rim, body)

# ── 모서리 바퀴 4 ──
WHEEL_R = 0.045
for tag, wx, wy in (("fl", -0.052, -0.045), ("fr", 0.052, -0.045),
                    ("bl", -0.052, 0.045), ("br", 0.052, 0.045)):
    wheel = ic.cylinder(f"wheel_{tag}", WHEEL_R, 0.013, wx, wy, WHEEL_R,
                        m.wood, verts=10, rot_y=math.radians(90))
    bake_rotation(wheel)
    ic.parent_to(wheel, body)

# ── 탑 위 궁병(0.6배) — 발판은 상자 윗면, 난간이 하반신을 가려준다 ──
S = 0.6
PLAT = BOX_TOP

torso = ic.cone("tower_archer", 0.030 * S, 0.038 * S, 0.058 * S, 0, 0.008, PLAT + 0.133 * S,
                m.armor, smooth=True)
head = ic.box("ta_head", 0.040 * S, 0.038 * S, 0.036 * S, 0, 0.008, PLAT + 0.184 * S, m.skin)
helmet = ic.cone("ta_helmet", 0.027 * S, 0.010 * S, 0.022 * S, 0, 0.008, PLAT + 0.210 * S,
                 m.armor, verts=6)
plume = ic.box("ta_plume", 0.008, 0.008, 0.018, 0, 0.008, PLAT + 0.228 * S, m.red)
hip = ic.HIP_Z * S
for tag, lx in (("l", -0.020 * S), ("r", 0.020 * S)):
    leg = ic.box(f"ta_leg_{tag}", 0.024 * S, 0.026 * S, hip, lx, 0.008, PLAT + hip, m.cloth,
                 origin_shift=(0, 0, -hip / 2))
    ic.parent_to(leg, torso)

HAND = PLAT + 0.104 * S + 0.030
arm_l = ic.box("ta_arm_l", 0.018 * S, 0.020 * S, 0.058 * S, -0.032 * S, -0.004, PLAT + 0.132 * S,
               m.armor, rot_x=math.radians(-42), origin_shift=(0, 0, -0.058 * S / 2))
arm_r = ic.box("ta_arm_r", 0.018 * S, 0.020 * S, 0.056 * S, 0.032 * S, 0.004, PLAT + 0.130 * S,
               m.armor, rot_x=math.radians(-8), origin_shift=(0, 0, -0.056 * S / 2))

# 활(왼팔 자식): 그립 + 위아래 림 + 시위
BX, BY, BZ = -0.032 * S - 0.006, -0.036, HAND
LIMB = 0.052
grip = ic.box("ta_bow_grip", 0.008, 0.009, 0.022, BX, BY, BZ, m.wood)
limb_u = ic.box("ta_bow_limb_u", 0.007, 0.008, LIMB, BX, BY - 0.008, BZ + 0.011 + LIMB / 2,
                m.wood, rot_x=math.radians(-16))
limb_d = ic.box("ta_bow_limb_d", 0.007, 0.008, LIMB, BX, BY - 0.008, BZ - 0.011 - LIMB / 2,
                m.wood, rot_x=math.radians(16))
string = ic.box("ta_bow_string", 0.003, 0.003, 0.122, BX, BY + 0.013, BZ, M_STRING)
for part in (limb_u, limb_d, string):
    ic.parent_to(part, grip)
ic.parent_to(grip, arm_l)

# 화살(오른팔 자식)
shaft = ic.box("ta_arrow", 0.004, 0.075, 0.004, 0.032 * S + 0.002, -0.018, HAND - 0.002, m.wood)
head_a = ic.cone("ta_arrow_head", 0.005, 0.001, 0.012, 0.032 * S + 0.002, -0.060, HAND - 0.002,
                 m.steel, verts=4, rot_x=math.radians(-90))
ic.parent_to(head_a, shaft)
ic.parent_to(shaft, arm_r)

for part in (head, helmet, plume, arm_l, arm_r):
    ic.parent_to(part, torso)
ic.parent_to(torso, body)

ic.export("troop-siege-tower.glb")
