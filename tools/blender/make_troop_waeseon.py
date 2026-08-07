# 병종 20 — 왜선(Waeseon, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_waeseon.py
#
# doc/design-unit.md: [대하, 속도 2, 탐지 2, 사거리 1/1/1] 왜선 모양의 배.
# 모습(사용자 확정, 2026-08-07): 길쭉한 느낌 — 중선보다 좁고 긴 선체, 낮게 뻗은
# 뾰족한 이물, 세로 이음매가 보이는 일본식 사각 돛 1, 고물의 망루 선실,
# 세력색 세로 깃발(노보리). 중국 배(정크 돛·뭉툭 이물)와 실루엣이 갈린다.
# 선박 규약: body(선체) / sail(돛, Ship 모션 마커)
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_HULL = ic.make_mat("hull", (0.42, 0.26, 0.12))
M_DECK = ic.make_mat("deck", (0.58, 0.38, 0.18))
M_SAIL = ic.make_mat("sail", (0.90, 0.87, 0.78), roughness=0.9)
M_SEAM = ic.make_mat("seam", (0.40, 0.30, 0.18))

# ── 선체(부모): 중선(0.091×0.270)보다 좁고 길다 ──
body = ic.box("body", 0.060, 0.330, 0.040, 0, 0, 0.030, M_HULL)
ic.bake_scale(body)

# 이물: 낮게 앞으로 뻗는 뾰족한 뱃머리 — 치켜든 판이 아니라 물을 가르는 쐐기
prow = ic.box("prow", 0.034, 0.085, 0.028, 0, -0.190, 0.044, M_HULL,
              rot_x=math.radians(-10))
prow_tip = ic.cone("prow_tip", 0.017, 0.001, 0.040, 0, -0.240, 0.052, M_HULL,
                   verts=4, rot_x=math.radians(-96))
for part in (prow, prow_tip):
    ic.parent_to(part, body)

# 갑판 + 낮은 뱃전 + 방향타
deck = ic.box("deck", 0.046, 0.280, 0.008, 0, 0.008, 0.052, M_DECK)
ic.parent_to(deck, body)
for s in (-1, 1):
    rail = ic.box(f"gunwale_{'l' if s < 0 else 'r'}", 0.008, 0.300, 0.020,
                  s * 0.030, 0.000, 0.058, M_DECK)
    ic.parent_to(rail, body)
rudder = ic.box("rudder", 0.008, 0.030, 0.048, 0, 0.180, 0.016, M_DECK,
                rot_x=math.radians(-14))
ic.parent_to(rudder, body)

# 고물: 망루 선실(야구라) — 기둥 위에 얹힌 작은 상자 + 지붕
for i, (px, py) in enumerate(((-0.020, 0.104), (0.020, 0.104), (-0.020, 0.148), (0.020, 0.148))):
    post = ic.box(f"yagura_post_{i}", 0.008, 0.008, 0.036, px, py, 0.076, M_DECK)
    ic.parent_to(post, body)
yagura = ic.box("yagura", 0.054, 0.058, 0.030, 0, 0.126, 0.108, M_HULL)
yagura_roof = ic.box("yagura_roof", 0.064, 0.068, 0.008, 0, 0.126, 0.128, M_SEAM)
for part in (yagura, yagura_roof):
    ic.parent_to(part, body)

# ── 돛대 1 + 일본식 사각 돛(세로 이음매) ──
mast = ic.cylinder("mast", 0.007, 0.250, 0, -0.030, 0.175, M_SEAM, verts=6)
ic.parent_to(mast, body)
sail = ic.box("sail", 0.096, 0.005, 0.130, 0, -0.030, 0.212, M_SAIL)
yard = ic.box("sail_yard", 0.104, 0.008, 0.008, 0, -0.030, 0.280, M_SEAM)
ic.parent_to(yard, sail)
for i in range(3):
    seam = ic.box(f"sail_seam_{i}", 0.005, 0.007, 0.126, -0.036 + i * 0.036, -0.030, 0.210,
                  M_SEAM)
    ic.parent_to(seam, sail)
ic.parent_to(sail, mast)

# ── 세력색 노보리(세로 깃발): 고물 망루 옆에 꽂힌다 ──
pole = ic.cylinder("nobori_pole", 0.004, 0.130, 0.024, 0.164, 0.190, M_SEAM, verts=6)
ic.parent_to(pole, body)
nobori = ic.box("nobori", 0.024, 0.004, 0.082, 0.038, 0.164, 0.208, m.red)
ic.parent_to(nobori, pole)

ic.export("troop-waeseon.glb")
