# 병종 14 — 무당비군(Wudang, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_wudang.py
#
# doc/design-unit.md: [육지+산악, 속도 2(산악 1), 탐지 2, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-07): 활과 방패, 가벼운 갑옷, 망토. 공격 모션은 궁병.
# bow_grip·arrow 등 궁병 규약 이름을 그대로 써서 사격 모션이 자동 재사용된다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_LIGHT = ic.make_mat("light_armor", (0.46, 0.44, 0.38))
M_CAPE = ic.make_mat("cape", (0.16, 0.26, 0.22), roughness=0.95)
M_STRING = ic.make_mat("string", (0.85, 0.82, 0.72), roughness=0.6)

# 왼팔은 활을 앞으로 뻗어 들고, 오른팔은 시위 쪽 — 궁병과 같은 자세
body, arm_l, arm_r = ic.build_body(m, arm_l_pitch=math.radians(-38), arm_r_pitch=math.radians(-6))

# 가벼운 갑옷 느낌: 몸통 위에 얇은 경갑 덧판
vest = ic.cone("vest", 0.040, 0.048, 0.050, 0, 0, 0.145, M_LIGHT, smooth=True)
ic.parent_to(vest, body)

# ── 망토: 등 뒤로 늘어지는 얇은 판(살짝 벌어짐) ──
cape = ic.box("cape", 0.078, 0.007, 0.118, 0, 0.042, 0.128, M_CAPE,
              rot_x=math.radians(8))
ic.parent_to(cape, body)

# ── 활(궁병과 동일 양식, 왼팔 자식) ──
BX = -(ic.ARM_X + 0.010)
BY = -0.052
LIMB = 0.085
grip = ic.box("bow_grip", 0.012, 0.014, 0.034, BX, BY, ic.HAND_Z + 0.010, m.wood)
limb_u = ic.box("bow_limb_u", 0.010, 0.011, LIMB, BX, BY - 0.012, ic.HAND_Z + 0.010 + 0.017 + LIMB / 2,
                m.wood, rot_x=math.radians(-16))
limb_d = ic.box("bow_limb_d", 0.010, 0.011, LIMB, BX, BY - 0.012, ic.HAND_Z + 0.010 - 0.017 - LIMB / 2,
                m.wood, rot_x=math.radians(16))
string = ic.box("bow_string", 0.004, 0.004, 0.196, BX, BY + 0.021, ic.HAND_Z + 0.010, M_STRING)
for part in (limb_u, limb_d, string):
    ic.parent_to(part, grip)
ic.parent_to(grip, arm_l)

# ── 화살(오른팔 자식) ──
AX, AY = ic.ARM_X + 0.004, -0.030
shaft = ic.box("arrow", 0.006, 0.120, 0.006, AX, AY, ic.HAND_Z + 0.002, m.wood)
head_a = ic.cone("arrow_head", 0.007, 0.001, 0.018, AX, AY - 0.069, ic.HAND_Z + 0.002,
                 m.steel, verts=4, rot_x=math.radians(-90))
ic.parent_to(head_a, shaft)
ic.parent_to(shaft, arm_r)

# ── 등의 둥근 방패(망토 위에 얹힘) ──
BYS = 0.056
shield = ic.cylinder("shield_round", 0.044, 0.010, 0.004, BYS, 0.152, m.wood,
                     verts=12, rot_x=math.radians(90))
rim = ic.cylinder("shield_rim", 0.047, 0.016, 0.004, BYS, 0.152, m.steel,
                  verts=12, rot_x=math.radians(90))
boss = ic.cylinder("shield_boss", 0.013, 0.018, 0.004, BYS - 0.004, 0.152, m.red,
                   verts=8, rot_x=math.radians(90))
for part in (rim, boss):
    ic.parent_to(part, shield)
ic.parent_to(shield, body)

ic.export("troop-wudang.glb")
