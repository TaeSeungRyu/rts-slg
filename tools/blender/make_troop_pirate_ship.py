# Pirate junk; same node names and dimensions as medium ship.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

M_FLAG = ic.make_mat("pirate_flag_black", (0.012, 0.012, 0.012))
M_TRIM = ic.make_mat("pirate_trim", (0.19, 0.13, 0.07))
M_HULL = ic.make_mat("hull", (0.12, 0.075, 0.045))
M_DECK = ic.make_mat("deck", (0.27, 0.16, 0.08))
M_SAIL = ic.make_mat("sail", (0.12, 0.115, 0.10), roughness=0.9)
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
flag = ic.box("sail_flag", 0.038, 0.004, 0.022, 0.026, 0.040, 0.356, M_FLAG)
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

# Reinforced rails and raised prow; preserve original animated node hierarchy.
for side in (-1, 1):
    for i in range(5):
        post = ic.box(f"pirate_post_{side}_{i}", .009, .009, .028, side*.044, -.105+i*.049, .084, M_TRIM)
        ic.parent_to(post, body)
    rail = ic.box(f"pirate_upper_rail_{side}", .009, .220, .009, side*.044, -.007, .100, M_TRIM)
    ic.parent_to(rail, body)
prow = ic.box("pirate_prow", .025, .045, .020, 0, -.165, .089, M_TRIM, rot_x=math.radians(-25))
ic.parent_to(prow, body)
ic.export("troop-pirate-ship.glb")
