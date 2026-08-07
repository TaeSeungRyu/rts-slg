# 병종 11 — 극병(Pikeman, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_pikeman.py
#
# doc/design-unit.md: [육지, 속도 2, 탐지 2, 사거리 1/1/1]
# 이름(사용자 확정, 2026-08-06): 극병. 일반 보병에 기다란 창(극) — 모션은 찌르기.
# 몸통은 infantry_common.build_body, 여기서는 극창만 얹는다.
#   pike : 오른팔 자식 — 긴 창대 + 창날 + 곁날(극의 특징). 찌르기 판별 마커
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()

# 왼팔은 자연스럽게, 오른팔은 창을 세워 쥔다
body, arm_l, arm_r = ic.build_body(m, arm_l_pitch=math.radians(-6), arm_r_pitch=math.radians(6))

# ── 극창: 병사 키(0.275)보다 긴 창대를 세워 든다 ──
HX, HY = ic.ARM_X + 0.006, -0.012
SHAFT = 0.330

pike = ic.cylinder("pike", 0.006, SHAFT, HX, HY, ic.HAND_Z - 0.030 + SHAFT / 2,
                   m.wood, verts=6)
tip = ic.cone("pike_tip", 0.010, 0.001, 0.040, HX, HY, ic.HAND_Z - 0.030 + SHAFT + 0.020,
              m.steel, verts=4)
# 곁날: 창날 바로 아래 직각으로 붙는 초승달 날 — 극(戟)의 특징
side = ic.box("pike_blade", 0.030, 0.006, 0.020, HX + 0.019, HY,
              ic.HAND_Z - 0.030 + SHAFT - 0.018, m.steel)
# 세력색 술
tassel = ic.box("pike_tassel", 0.014, 0.014, 0.022, HX, HY,
                ic.HAND_Z - 0.030 + SHAFT - 0.045, m.red)
for part in (tip, side, tassel):
    ic.parent_to(part, pike)
ic.parent_to(pike, arm_r)

ic.export("troop-pikeman.glb")
