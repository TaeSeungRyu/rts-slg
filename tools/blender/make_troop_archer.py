# 병종 3 — 궁병(Archer, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_archer.py
#
# doc/spec-unit.md: [육지, 속도 2, 탐지 2, 사거리 2/1/1] 활을 들고 있다.
# 몸통은 infantry_common.build_body가 세우고 여기서는 활·화살통만 얹는다.
#   bow    : 왼팔 자식 — 활대 위·아래 림 + 시위. 세로로 든다
#   arrow  : 오른팔 자식 — 공격 모션에서 시위에 메긴다
#   quiver : 몸통 자식 — 등의 화살통
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_STRING = ic.make_mat("string", (0.85, 0.82, 0.72), roughness=0.6)
M_FLETCH = ic.make_mat("fletch", (0.88, 0.88, 0.86))

# 왼팔은 활을 앞으로 뻗어 들고, 오른팔은 시위 쪽에 둔다
body, arm_l, arm_r = ic.build_body(m, arm_l_pitch=math.radians(-38), arm_r_pitch=math.radians(-6))

# ── 활: 왼손 위치에서 세로로. 위·아래 림을 바깥으로 살짝 젖혀 활 실루엣을 만든다 ──
BX = -(ic.ARM_X + 0.010)
BY = -0.052
LIMB = 0.085

grip = ic.box("bow_grip", 0.012, 0.014, 0.034, BX, BY, ic.HAND_Z + 0.010, m.wood)
limb_u = ic.box("bow_limb_u", 0.010, 0.011, LIMB, BX, BY - 0.012, ic.HAND_Z + 0.010 + 0.017 + LIMB / 2,
                m.wood, rot_x=math.radians(-16))
limb_d = ic.box("bow_limb_d", 0.010, 0.011, LIMB, BX, BY - 0.012, ic.HAND_Z + 0.010 - 0.017 - LIMB / 2,
                m.wood, rot_x=math.radians(16))
# 시위: 두 림 끝을 세로로 잇는다
string = ic.box("bow_string", 0.004, 0.004, 0.196, BX, BY + 0.021, ic.HAND_Z + 0.010, M_STRING)

for part in (limb_u, limb_d, string):
    ic.parent_to(part, grip)
ic.parent_to(grip, arm_l)

# ── 화살: 오른손에 쥔다. 공격 모션에서 팔이 시위로 간다 ──
AX, AY = ic.ARM_X + 0.004, -0.030
shaft = ic.box("arrow", 0.006, 0.120, 0.006, AX, AY, ic.HAND_Z + 0.002, m.wood)
head = ic.cone("arrow_head", 0.007, 0.001, 0.018, AX, AY - 0.069, ic.HAND_Z + 0.002,
               m.steel, verts=4, rot_x=math.radians(-90))
fletch = ic.box("arrow_fletch", 0.003, 0.024, 0.016, AX, AY + 0.052, ic.HAND_Z + 0.002, M_FLETCH)
for part in (head, fletch):
    ic.parent_to(part, shaft)
ic.parent_to(shaft, arm_r)

# ── 화살통: 등에 비스듬히. 화살 깃 두 개가 위로 삐져나온다 ──
QX, QY = 0.026, 0.052
quiver = ic.cylinder("quiver", 0.016, 0.095, QX, QY, 0.150, m.wood,
                     verts=6, rot_x=math.radians(12))
for i, (dx, dz) in enumerate(((-0.007, 0.0), (0.008, 0.008))):
    qf = ic.box(f"quiver_fletch_{i}", 0.004, 0.016, 0.020,
                QX + dx, QY + 0.014, 0.205 + dz, M_FLETCH, rot_x=math.radians(12))
    ic.parent_to(qf, quiver)
ic.parent_to(quiver, body)

ic.export("troop-archer.glb")
