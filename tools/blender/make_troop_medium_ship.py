# 병종 9 — 중선(MediumShip, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_medium_ship.py
#
# doc/design-unit.md: [대하, 속도 2, 탐지 2, 사거리 1/1/1] 중국식 중간 배.
# 소선과의 구분(사용자 확정, 2026-08-06): 돛 2개, 폭 20% 증가(0.091),
# 이물은 뾰족한 쐐기가 아니라 정크선 특유의 뭉툭하게 치켜든 사각 판.
#
# 선박 규약(소선과 동일): body(선체) / sail(주돛, 판별 마커) / sail2(앞돛)
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_HULL = ic.make_mat("hull", (0.38, 0.20, 0.08))
M_DECK = ic.make_mat("deck", (0.52, 0.30, 0.11))
M_SAIL = ic.make_mat("sail", (0.80, 0.68, 0.42), roughness=0.9)
M_BATTEN = ic.make_mat("batten", (0.35, 0.24, 0.14))

# ── 선체(부모): 소선보다 폭 20% 넓고 길다 ──
body = ic.box("body", 0.091, 0.270, 0.048, 0, 0, 0.034, M_HULL)
ic.bake_scale(body)

# 이물: 뭉툭하게 치켜든 사각 판(정크선) — 소선의 뾰족한 쐐기와 다르다
bow_panel = ic.box("bow_panel", 0.085, 0.020, 0.062, 0, -0.146, 0.052, M_HULL,
                   rot_x=math.radians(-28))
bow_cap = ic.box("bow_cap", 0.090, 0.026, 0.012, 0, -0.156, 0.082, M_DECK,
                 rot_x=math.radians(-28))
for part in (bow_panel, bow_cap):
    ic.parent_to(part, body)

# 고물: 높이 올린 선미루(작은 선실 + 지붕)
poop = ic.box("poop", 0.084, 0.062, 0.036, 0, 0.112, 0.076, M_HULL)
poop_roof = ic.box("poop_roof", 0.092, 0.070, 0.010, 0, 0.112, 0.098, M_BATTEN)
for part in (poop, poop_roof):
    ic.parent_to(part, body)

# 갑판 + 난간 + 방향타
deck = ic.box("deck", 0.068, 0.210, 0.010, 0, -0.015, 0.060, M_DECK)
ic.parent_to(deck, body)
for s in (-1, 1):
    rail = ic.box(f"gunwale_{'l' if s < 0 else 'r'}", 0.010, 0.245, 0.016,
                  s * 0.045, -0.005, 0.066, M_DECK)
    ic.parent_to(rail, body)
rudder = ic.box("rudder", 0.009, 0.034, 0.052, 0, 0.152, 0.020, M_DECK,
                rot_x=math.radians(-14))
ic.parent_to(rudder, body)

# ── 돛대 2 + 정크 돛 2(주돛이 크고 앞돛이 작다) + 세력색 기 ──
main_mast = ic.cylinder("mast", 0.008, 0.290, 0, 0.040, 0.200, M_BATTEN, verts=6)
ic.parent_to(main_mast, body)
main_sail = ic.box("sail", 0.108, 0.005, 0.165, 0, 0.040, 0.225, M_SAIL)
for i in range(4):
    batten = ic.box(f"sail_batten_{i}", 0.114, 0.007, 0.007, 0, 0.040, 0.160 + i * 0.044,
                    M_BATTEN)
    ic.parent_to(batten, main_sail)
flag = ic.box("sail_flag", 0.038, 0.004, 0.022, 0.026, 0.040, 0.356, m.red)
ic.parent_to(flag, main_sail)
ic.parent_to(main_sail, main_mast)

fore_mast = ic.cylinder("fore_mast", 0.007, 0.220, 0, -0.078, 0.165, M_BATTEN, verts=6)
ic.parent_to(fore_mast, body)
fore_sail = ic.box("sail2", 0.086, 0.005, 0.125, 0, -0.078, 0.185, M_SAIL)
for i in range(3):
    batten = ic.box(f"sail2_batten_{i}", 0.092, 0.007, 0.007, 0, -0.078, 0.140 + i * 0.042,
                    M_BATTEN)
    ic.parent_to(batten, fore_sail)
ic.parent_to(fore_sail, fore_mast)

ic.export("troop-medium-ship.glb")
