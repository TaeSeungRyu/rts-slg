# 병종 11 — 극병(Pikeman, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_pikeman.py
#
# doc/design-unit.md: [육지, 속도 2, 탐지 2, 사거리 1/1/1]
# 이름(사용자 확정, 2026-08-06): 극병. 일반 보병에 기다란 창(극) — 모션은 찌르기.
# 몸통은 infantry_common.build_body, 여기서는 극창만 얹는다.
#   pike : 오른팔 자식 — 긴 창대 + 창날 + 곁날(극의 특징). 찌르기 판별 마커
# 창은 처음부터 수평으로 앞을 겨눈다(팔랑크스 자세) — 팔을 내리는 동작이
# 내리찍기로 읽혀서 대기 자세 자체를 겨눔으로 바꿨다(2026-08-06 사용자 확정)
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()

# 왼팔은 자연스럽게, 오른팔은 반쯤 앞으로 — 수평 창을 받쳐 쥔 자세
body, arm_l, arm_r = ic.build_body(m, arm_l_pitch=math.radians(-6), arm_r_pitch=math.radians(-38))

# ── 극창: 수평으로 앞(-Y)을 겨눈다. 손이 창대 뒤쪽 1/4을 쥔다 ──
HX = ic.ARM_X + 0.006
HZ = ic.HAND_Z + 0.012
SHAFT = 0.330
REAR = 0.075
CY = REAR - SHAFT / 2

pike = ic.cylinder("pike", 0.006, SHAFT, HX, CY, HZ, m.wood, verts=6,
                   rot_x=math.radians(90))
tip = ic.cone("pike_tip", 0.010, 0.001, 0.040, HX, REAR - SHAFT - 0.020, HZ,
              m.steel, verts=4, rot_x=math.radians(-90))
# 곁날: 창날 바로 뒤 직각으로 위로 솟는 날 — 극(戟)의 특징
side = ic.box("pike_blade", 0.006, 0.020, 0.030, HX, REAR - SHAFT + 0.020, HZ + 0.019,
              m.steel)
# 세력색 술
tassel = ic.box("pike_tassel", 0.014, 0.022, 0.014, HX, REAR - SHAFT + 0.048, HZ, m.red)
for part in (tip, side, tassel):
    ic.parent_to(part, pike)
ic.parent_to(pike, arm_r)

# ── 원형 방패: 창 반대쪽 팔에. 원판 + 가운데 세력색 돌기 ──
SX = -(ic.ARM_X + 0.014)
shield = ic.cylinder("shield_round", 0.046, 0.010, SX, -0.030, 0.128, m.wood,
                     verts=12, rot_x=math.radians(90))
rim = ic.cylinder("shield_rim", 0.049, 0.016, SX, -0.030, 0.128, m.steel,
                  verts=12, rot_x=math.radians(90))
boss = ic.cylinder("shield_boss", 0.014, 0.018, SX, -0.034, 0.128, m.red,
                   verts=8, rot_x=math.radians(90))
for part in (rim, boss):
    ic.parent_to(part, shield)
ic.parent_to(shield, arm_l)

ic.export("troop-pikeman.glb")
