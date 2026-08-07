# 병종 10 — 대선(LargeShip, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_large_ship.py
#
# doc/design-unit.md: [대하, 속도 1, 탐지 2, 사거리 1/1/1] 중국식 큰 배.
# 규칙(사용자 확정, 2026-08-06): 편대 없이 항상 1척. 돛 4개, 갑판 위에 작은 사람.
# 크기는 초판의 1.5배(사용자 확정) — 1척뿐이라 편대 간격 제약이 없다.
# 갑판 둘레에 난간(기둥+가로대)을 두른다. 주민 배회 반경과 맞물리는 안전 울타리.
#
# 선박 규약: body(선체) / sail(주돛, 판별 마커)·sail2·sail3·sail4
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
body = ic.box("body", 0.172, 0.510, 0.084, 0, 0, 0.060, M_HULL)
ic.bake_scale(body)

# 이물: 뭉툭하게 치켜든 사각 판(정크선 계열) + 덮개
bow_panel = ic.box("bow_panel", 0.158, 0.036, 0.117, 0, -0.276, 0.093, M_HULL,
                   rot_x=math.radians(-26))
bow_cap = ic.box("bow_cap", 0.168, 0.045, 0.021, 0, -0.294, 0.150, M_DECK,
                 rot_x=math.radians(-26))
for part in (bow_panel, bow_cap):
    ic.parent_to(part, body)

# 고물: 2단 선미루
poop1 = ic.box("poop1", 0.159, 0.120, 0.060, 0, 0.210, 0.132, M_HULL)
poop2 = ic.box("poop2", 0.132, 0.090, 0.048, 0, 0.219, 0.186, M_HULL)
poop_roof = ic.box("poop_roof", 0.147, 0.105, 0.018, 0, 0.219, 0.219, M_BATTEN)
for part in (poop1, poop2, poop_roof):
    ic.parent_to(part, body)

# 갑판(주민이 걸을 면) + 뱃전 + 방향타
deck = ic.box("deck", 0.132, 0.375, 0.018, 0, -0.042, 0.105, M_DECK)
ic.parent_to(deck, body)
for s in (-1, 1):
    rail = ic.box(f"gunwale_{'l' if s < 0 else 'r'}", 0.018, 0.458, 0.030,
                  s * 0.084, -0.015, 0.117, M_DECK)
    ic.parent_to(rail, body)
rudder = ic.box("rudder", 0.017, 0.060, 0.093, 0, 0.288, 0.036, M_DECK,
                rot_x=math.radians(-14))
ic.parent_to(rudder, body)

# ── 난간: 뱃전 위 기둥 + 가로대. 좌우 두 줄 + 이물 쪽 가로막 ──
RAIL_TOP = 0.168
for s in (-1, 1):
    for i in range(7):
        py = -0.235 + i * 0.075
        post = ic.box(f"rail_post_{'l' if s < 0 else 'r'}_{i}", 0.009, 0.009, 0.036,
                      s * 0.084, py, 0.150, M_BATTEN)
        ic.parent_to(post, body)
    bar = ic.box(f"rail_bar_{'l' if s < 0 else 'r'}", 0.010, 0.470, 0.009,
                 s * 0.084, -0.010, RAIL_TOP, M_BATTEN)
    ic.parent_to(bar, body)
front_bar = ic.box("rail_bar_front", 0.168, 0.010, 0.009, 0, -0.246, RAIL_TOP, M_BATTEN)
ic.parent_to(front_bar, body)

# ── 돛대 4 + 정크 돛 4(가운데가 크고 앞뒤로 갈수록 작다) + 세력색 기 ──
# (이름, y, 돛대높이, 돛폭, 돛높이, 돛중심z, 배튼수)
SAILS = (
    ("sail",  0.068, 0.540, 0.192, 0.300, 0.405, 5),
    ("sail2", -0.090, 0.480, 0.162, 0.255, 0.368, 4),
    ("sail3", -0.218, 0.390, 0.129, 0.195, 0.308, 3),
    ("sail4", 0.203, 0.375, 0.117, 0.173, 0.300, 3),
)
for name, sy, mh, sw, sh, sz, nb in SAILS:
    mast = ic.cylinder(f"{name}_mast", 0.011, mh, 0, sy, mh / 2 + 0.068, M_BATTEN, verts=6)
    ic.parent_to(mast, body)
    sail = ic.box(name, sw, 0.006, sh, 0, sy, sz, M_SAIL)
    for i in range(nb):
        batten = ic.box(f"{name}_batten_{i}", sw + 0.008, 0.008, 0.008,
                        0, sy, sz - sh / 2 + 0.03 + i * (sh - 0.06) / max(nb - 1, 1), M_BATTEN)
        ic.parent_to(batten, sail)
    ic.parent_to(sail, mast)

flag = ic.box("sail_flag", 0.063, 0.006, 0.036, 0.045, 0.068, 0.625, m.red)
main_sail = bpy.data.objects["sail"]
ic.parent_to(flag, main_sail)

ic.export("troop-large-ship.glb")
