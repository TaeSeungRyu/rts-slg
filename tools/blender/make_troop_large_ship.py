# 병종 10 — 대선(LargeShip, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_large_ship.py
#
# doc/spec-unit.md: [대하, 속도 1, 탐지 2, 사거리 1/1/1] 중국식 큰 배.
# 규칙(사용자 확정, 2026-08-06): 편대 없이 항상 1척. 돛 4개. 갑판 둘레 난간.
# 갑판에는 배에 맞춰 작게(0.42배) 만든 궁병 8이 타고 있다(2열x4행) — 전원 앞을 향한다.
# 공격 때 화살은 갑판 궁병(da{i}_arrow)에게서 날아간다.
#
# 선박 규약: body(선체) / sail(주돛, 판별 마커)·sail2·sail3·sail4 / da{i}_arrow(갑판 화살)
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
M_STRING = ic.make_mat("string", (0.85, 0.82, 0.72), roughness=0.6)

# ── 선체(부모) ──
body = ic.box("body", 0.215, 0.638, 0.105, 0, 0, 0.075, M_HULL)
ic.bake_scale(body)

# 이물: 뭉툭하게 치켜든 사각 판(정크선 계열) + 덮개
bow_panel = ic.box("bow_panel", 0.198, 0.045, 0.146, 0, -0.345, 0.116, M_HULL,
                   rot_x=math.radians(-26))
bow_cap = ic.box("bow_cap", 0.210, 0.056, 0.026, 0, -0.368, 0.188, M_DECK,
                 rot_x=math.radians(-26))
for part in (bow_panel, bow_cap):
    ic.parent_to(part, body)

# 고물: 2단 선미루
poop1 = ic.box("poop1", 0.199, 0.150, 0.075, 0, 0.263, 0.165, M_HULL)
poop2 = ic.box("poop2", 0.165, 0.113, 0.060, 0, 0.274, 0.233, M_HULL)
poop_roof = ic.box("poop_roof", 0.184, 0.131, 0.023, 0, 0.274, 0.274, M_BATTEN)
for part in (poop1, poop2, poop_roof):
    ic.parent_to(part, body)

# 갑판 + 뱃전 + 방향타
deck = ic.box("deck", 0.165, 0.469, 0.023, 0, -0.053, 0.131, M_DECK)
ic.parent_to(deck, body)
for s in (-1, 1):
    rail = ic.box(f"gunwale_{'l' if s < 0 else 'r'}", 0.023, 0.573, 0.038,
                  s * 0.105, -0.019, 0.146, M_DECK)
    ic.parent_to(rail, body)
rudder = ic.box("rudder", 0.021, 0.075, 0.116, 0, 0.360, 0.045, M_DECK,
                rot_x=math.radians(-14))
ic.parent_to(rudder, body)

# ── 난간: 뱃전 위 기둥 + 가로대. 좌우 두 줄 + 이물 쪽 가로막 ──
RAIL_TOP = 0.210
for s in (-1, 1):
    for i in range(7):
        py = -0.294 + i * 0.094
        post = ic.box(f"rail_post_{'l' if s < 0 else 'r'}_{i}", 0.011, 0.011, 0.045,
                      s * 0.105, py, 0.188, M_BATTEN)
        ic.parent_to(post, body)
    bar = ic.box(f"rail_bar_{'l' if s < 0 else 'r'}", 0.013, 0.588, 0.011,
                 s * 0.105, -0.013, RAIL_TOP, M_BATTEN)
    ic.parent_to(bar, body)
front_bar = ic.box("rail_bar_front", 0.210, 0.013, 0.011, 0, -0.308, RAIL_TOP, M_BATTEN)
ic.parent_to(front_bar, body)

# ── 돛대 4 + 정크 돛 4 + 세력색 기 ──
SAILS = (
    ("sail",  0.085, 0.675, 0.240, 0.375, 0.506, 5),
    ("sail2", -0.113, 0.600, 0.203, 0.319, 0.459, 4),
    ("sail3", -0.272, 0.488, 0.161, 0.244, 0.384, 3),
    ("sail4", 0.254, 0.469, 0.146, 0.216, 0.375, 3),
)
for name, sy, mh, sw, sh, sz, nb in SAILS:
    mast = ic.cylinder(f"{name}_mast", 0.014, mh, 0, sy, mh / 2 + 0.085, M_BATTEN, verts=6)
    ic.parent_to(mast, body)
    sail = ic.box(name, sw, 0.006, sh, 0, sy, sz, M_SAIL)
    for i in range(nb):
        batten = ic.box(f"{name}_batten_{i}", sw + 0.010, 0.010, 0.010,
                        0, sy, sz - sh / 2 + 0.038 + i * (sh - 0.075) / max(nb - 1, 1), M_BATTEN)
        ic.parent_to(batten, sail)
    ic.parent_to(sail, mast)

flag = ic.box("sail_flag", 0.079, 0.008, 0.045, 0.056, 0.085, 0.781, m.red)
main_sail = bpy.data.objects["sail"]
ic.parent_to(flag, main_sail)

# ── 갑판 궁병 8(0.42배, 배 크기에 맞춰 축소): 2열 × 4행, 전원 앞을 향한다 ──
S = 0.42
DZ = 0.143


def deck_archer(i, wx, wy):
    torso = ic.cone(f"da{i}_torso", 0.030 * S, 0.038 * S, 0.058 * S, wx, wy, DZ + 0.133 * S,
                    m.armor, smooth=True)
    head = ic.box(f"da{i}_head", 0.040 * S, 0.038 * S, 0.036 * S, wx, wy, DZ + 0.184 * S, m.skin)
    helm = ic.cone(f"da{i}_helmet", 0.027 * S, 0.010 * S, 0.022 * S, wx, wy, DZ + 0.210 * S,
                   m.armor, verts=6)
    hip = ic.HIP_Z * S
    for ltag, lx in (("l", -0.020 * S), ("r", 0.020 * S)):
        leg = ic.box(f"da{i}_leg_{ltag}", 0.024 * S, 0.026 * S, hip, wx + lx, wy, DZ + hip,
                     m.cloth)
        ic.parent_to(leg, torso)
    arm_l = ic.box(f"da{i}_arm_l", 0.018 * S, 0.020 * S, 0.056 * S, wx - 0.034 * S,
                   wy - 0.010, DZ + 0.130 * S, m.armor, rot_x=math.radians(-46))
    arm_r = ic.box(f"da{i}_arm_r", 0.018 * S, 0.020 * S, 0.054 * S, wx + 0.034 * S,
                   wy - 0.002, DZ + 0.128 * S, m.armor, rot_x=math.radians(-12))
    HAND = DZ + 0.104 * S + 0.012
    grip = ic.box(f"da{i}_bow_grip", 0.006, 0.007, 0.016, wx - 0.020, wy - 0.024, HAND, m.wood)
    limb_u = ic.box(f"da{i}_bow_limb_u", 0.005, 0.006, 0.034, wx - 0.020, wy - 0.029,
                    HAND + 0.024, m.wood, rot_x=math.radians(-16))
    limb_d = ic.box(f"da{i}_bow_limb_d", 0.005, 0.006, 0.034, wx - 0.020, wy - 0.029,
                    HAND - 0.024, m.wood, rot_x=math.radians(16))
    string = ic.box(f"da{i}_bow_string", 0.003, 0.003, 0.080, wx - 0.020, wy - 0.014,
                    HAND, M_STRING)
    for part in (limb_u, limb_d, string):
        ic.parent_to(part, grip)
    ic.parent_to(grip, arm_l)
    arrow = ic.box(f"da{i}_arrow", 0.004, 0.050, 0.004, wx + 0.015, wy - 0.026,
                   HAND - 0.002, m.wood)
    tip = ic.cone(f"da{i}_arrow_head", 0.004, 0.001, 0.009, wx + 0.015, wy - 0.054,
                  HAND - 0.002, m.steel, verts=4, rot_x=math.radians(-90))
    ic.parent_to(tip, arrow)
    ic.parent_to(arrow, arm_r)
    for part in (head, helm, arm_l, arm_r):
        ic.parent_to(part, torso)
    ic.parent_to(torso, body)


for i, (wx, wy) in enumerate(((-0.056, -0.245), (0.056, -0.245),
                              (-0.056, -0.155), (0.056, -0.155),
                              (-0.056, -0.020), (0.056, -0.020),
                              (-0.056, 0.130), (0.056, 0.130))):
    deck_archer(i, wx, wy)

ic.export("troop-large-ship.glb")
