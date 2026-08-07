# 병종 10 — 대선(LargeShip, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_large_ship.py
#
# doc/design-unit.md: [대하, 속도 1, 탐지 2, 사거리 1/1/1] 중국식 큰 배.
# 규칙(사용자 확정, 2026-08-06): 편대 없이 항상 1척으로 표현한다.
# 돛 4개, 갑판 위에 작은 사람이 움직인다(런타임 VillagerAmbience).
#
# 선박 규약: body(선체) / sail(주돛, 판별 마커)·sail2·sail3·sail4
# 1척뿐이라 편대 간격 제약이 없다 — 배 6종 중 가장 크게 만든다.
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

# ── 선체(부모) ──
body = ic.box("body", 0.115, 0.340, 0.056, 0, 0, 0.040, M_HULL)
ic.bake_scale(body)

# 이물: 뭉툭하게 치켜든 사각 판(정크선 계열) + 덮개
bow_panel = ic.box("bow_panel", 0.105, 0.024, 0.078, 0, -0.184, 0.062, M_HULL,
                   rot_x=math.radians(-26))
bow_cap = ic.box("bow_cap", 0.112, 0.030, 0.014, 0, -0.196, 0.100, M_DECK,
                 rot_x=math.radians(-26))
for part in (bow_panel, bow_cap):
    ic.parent_to(part, body)

# 고물: 2단 선미루
poop1 = ic.box("poop1", 0.106, 0.080, 0.040, 0, 0.140, 0.088, M_HULL)
poop2 = ic.box("poop2", 0.088, 0.060, 0.032, 0, 0.146, 0.124, M_HULL)
poop_roof = ic.box("poop_roof", 0.098, 0.070, 0.012, 0, 0.146, 0.146, M_BATTEN)
for part in (poop1, poop2, poop_roof):
    ic.parent_to(part, body)

# 갑판(주민이 걸을 면) + 난간 + 방향타
deck = ic.box("deck", 0.088, 0.250, 0.012, 0, -0.028, 0.070, M_DECK)
ic.parent_to(deck, body)
for s in (-1, 1):
    rail = ic.box(f"gunwale_{'l' if s < 0 else 'r'}", 0.012, 0.305, 0.020,
                  s * 0.056, -0.010, 0.078, M_DECK)
    ic.parent_to(rail, body)
rudder = ic.box("rudder", 0.011, 0.040, 0.062, 0, 0.192, 0.024, M_DECK,
                rot_x=math.radians(-14))
ic.parent_to(rudder, body)

# ── 돛대 4 + 정크 돛 4(가운데가 크고 앞뒤로 갈수록 작다) + 세력색 기 ──
# (이름, y, 돛대높이, 돛폭, 돛높이, 돛중심z, 배튼수)
SAILS = (
    ("sail",  0.045, 0.360, 0.128, 0.200, 0.270, 5),
    ("sail2", -0.060, 0.320, 0.108, 0.170, 0.245, 4),
    ("sail3", -0.145, 0.260, 0.086, 0.130, 0.205, 3),
    ("sail4", 0.135, 0.250, 0.078, 0.115, 0.200, 3),
)
for name, sy, mh, sw, sh, sz, nb in SAILS:
    mast = ic.cylinder(f"{name}_mast", 0.008, mh, 0, sy, mh / 2 + 0.045, M_BATTEN, verts=6)
    ic.parent_to(mast, body)
    sail = ic.box(name, sw, 0.005, sh, 0, sy, sz, M_SAIL)
    for i in range(nb):
        batten = ic.box(f"{name}_batten_{i}", sw + 0.006, 0.007, 0.007,
                        0, sy, sz - sh / 2 + 0.02 + i * (sh - 0.04) / max(nb - 1, 1), M_BATTEN)
        ic.parent_to(batten, sail)
    ic.parent_to(sail, mast)

flag = ic.box("sail_flag", 0.042, 0.004, 0.024, 0.030, 0.045, 0.415, m.red)
# 주돛 꼭대기 기 — sail의 자식이라 함께 흔들린다
main_sail = bpy.data.objects["sail"]
ic.parent_to(flag, main_sail)

ic.export("troop-large-ship.glb")
