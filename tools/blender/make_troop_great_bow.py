# 병종 17 — 대궁병(GreatBow, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_great_bow.py
#
# doc/design-unit.md: [육지, 속도 1, 탐지 2, 사거리 2/2/2] 정말 큰 활을 들고 있다.
# 활은 병사 키를 훌쩍 넘는 장궁 — 손 높이에 그대로 붙이면 아래 림이 땅을 뚫으므로
# 손잡이를 활 중심이 아니라 아래쪽 1/3에 두는 비대칭 활로 만든다(아래 림 끝이 지면).
# bow_grip·arrow 등 궁병 규약 이름을 그대로 써서 사격 모션이 자동 재사용된다.
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

body, arm_l, arm_r = ic.build_body(m, arm_l_pitch=math.radians(-38), arm_r_pitch=math.radians(-6))

# ── 대궁: 왼손 위치에서 세로로. 위 림이 길고 아래 림이 짧은 비대칭 ──
BX = -(ic.ARM_X + 0.010)
BY = -0.052
GRIP_Z = ic.HAND_Z + 0.010
LIMB_U = 0.195
LIMB_D = 0.090

grip = ic.box("bow_grip", 0.014, 0.016, 0.044, BX, BY, GRIP_Z, m.wood)
limb_u = ic.box("bow_limb_u", 0.013, 0.014, LIMB_U, BX, BY - 0.012,
                GRIP_Z + 0.022 + LIMB_U / 2, m.wood, rot_x=math.radians(-14))
limb_d = ic.box("bow_limb_d", 0.013, 0.014, LIMB_D, BX, BY - 0.012,
                GRIP_Z - 0.022 - LIMB_D / 2, m.wood, rot_x=math.radians(18))
nock_u = ic.box("bow_nock_u", 0.017, 0.018, 0.014, BX, BY + 0.011, 0.326, m.steel)
nock_d = ic.box("bow_nock_d", 0.017, 0.018, 0.014, BX, BY + 0.001, 0.008, m.steel)
string = ic.box("bow_string", 0.004, 0.004, 0.324, BX, BY + 0.024, 0.166, M_STRING)
for part in (limb_u, limb_d, nock_u, nock_d, string):
    ic.parent_to(part, grip)
ic.parent_to(grip, arm_l)

# ── 큰 화살(오른팔 자식): 궁병보다 길고 깃도 크다 ──
AX, AY = ic.ARM_X + 0.004, -0.030
shaft = ic.box("arrow", 0.007, 0.170, 0.007, AX, AY, ic.HAND_Z + 0.002, m.wood)
head = ic.cone("arrow_head", 0.009, 0.001, 0.024, AX, AY - 0.096, ic.HAND_Z + 0.002,
               m.steel, verts=4, rot_x=math.radians(-90))
fletch = ic.box("arrow_fletch", 0.004, 0.030, 0.020, AX, AY + 0.076, ic.HAND_Z + 0.002, M_FLETCH)
for part in (head, fletch):
    ic.parent_to(part, shaft)
ic.parent_to(shaft, arm_r)

# ── 큰 화살통: 등에 비스듬히. 궁병보다 굵고 길다 ──
QX, QY = 0.028, 0.054
quiver = ic.cylinder("quiver", 0.019, 0.115, QX, QY, 0.155, m.wood,
                     verts=6, rot_x=math.radians(12))
for i, (dx, dz) in enumerate(((-0.009, 0.0), (0.009, 0.010), (0.000, 0.018))):
    qf = ic.box(f"quiver_fletch_{i}", 0.005, 0.020, 0.024,
                QX + dx, QY + 0.016, 0.222 + dz, M_FLETCH, rot_x=math.radians(12))
    ic.parent_to(qf, quiver)
ic.parent_to(quiver, body)

ic.export("troop-great-bow.glb")
