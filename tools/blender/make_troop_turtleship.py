# 병종 19 — 거북선(Turtleship, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_turtleship.py
#
# doc/spec-unit.md: [대하, 속도 1, 탐지 2, 사거리 1/1/1] 편대 없이 항상 1척.
# 송곳 박힌 등딱지가 갑판 전체를 덮고 뱃머리에 용머리 — 공격 때 용머리에서
# 화염이 나간다(dragon_head 노드가 화염 분사구, _turtleShip 분기의 판별 마커).
# 선박 규약: body(선체) / sail·sail2(돛, Ship 모션 마커) / dragon_head(용머리)
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
M_SHELL = ic.make_mat("shell", (0.24, 0.17, 0.09))
M_SAIL = ic.make_mat("sail", (0.80, 0.68, 0.42), roughness=0.9)
M_BATTEN = ic.make_mat("batten", (0.35, 0.24, 0.14))
M_DRAGON = ic.make_mat("dragon", (0.30, 0.34, 0.26))
M_FIRE = ic.make_mat("fire", (0.92, 0.34, 0.08), roughness=0.5)

# ── 선체(부모): 중선보다 넓고 낮게 앉는다 ──
body = ic.box("body", 0.125, 0.360, 0.055, 0, 0, 0.038, M_HULL)
ic.bake_scale(body)

bow_panel = ic.box("bow_panel", 0.110, 0.024, 0.066, 0, -0.188, 0.052, M_HULL,
                   rot_x=math.radians(-24))
stern_panel = ic.box("stern_panel", 0.110, 0.022, 0.058, 0, 0.186, 0.052, M_HULL,
                     rot_x=math.radians(20))
rudder = ic.box("rudder", 0.010, 0.036, 0.056, 0, 0.204, 0.020, M_DECK,
                rot_x=math.radians(-14))
for part in (bow_panel, stern_panel, rudder):
    ic.parent_to(part, body)

# ── 등딱지: 눌린 타원체가 갑판 전체를 덮는다 + 중앙 등마루 판 ──
SHELL_A, SHELL_B, SHELL_C = 0.105, 0.175, 0.052
SHELL_Z = 0.098
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1.0,
                                     location=(0, 0, SHELL_Z))
shell = bpy.context.object
shell.name = "shell"
shell.scale = (SHELL_A, SHELL_B, SHELL_C)
shell.data.materials.append(M_SHELL)
ic.shade_smooth(shell)
ic.bake_scale(shell)
ic.parent_to(shell, body)

ridge = ic.box("shell_ridge", 0.020, 0.300, 0.010, 0, 0, SHELL_Z + SHELL_C - 0.002, M_BATTEN)
ic.parent_to(ridge, shell)

# 송곳: 등딱지 곡면을 따라 세운다 — 표면 높이는 타원체 식으로 구한다
spike_i = 0
for sx in (-0.052, 0.0, 0.052):
    for sy in (-0.124, -0.062, 0.0, 0.062, 0.124):
        if sx == 0.0 and abs(sy) < 0.10:
            continue
        t = 1.0 - (sx / SHELL_A) ** 2 - (sy / SHELL_B) ** 2
        sz = SHELL_Z + SHELL_C * math.sqrt(max(t, 0.0))
        spike = ic.cone(f"spike_{spike_i}", 0.0075, 0.001, 0.024, sx, sy, sz + 0.008,
                        m.steel, verts=4)
        ic.parent_to(spike, shell)
        spike_i += 1

# ── 용머리: 뱃머리에서 앞을 노려본다. 이 노드 위치에서 화염이 나간다 ──
neck = ic.box("dragon_neck", 0.036, 0.052, 0.036, 0, -0.192, 0.116, M_DRAGON,
              rot_x=math.radians(-35))
dragon = ic.box("dragon_head", 0.052, 0.064, 0.046, 0, -0.228, 0.148, M_DRAGON)
snout = ic.box("dragon_snout", 0.040, 0.042, 0.020, 0, -0.264, 0.150, M_DRAGON)
jaw = ic.box("dragon_jaw", 0.036, 0.038, 0.010, 0, -0.260, 0.128, M_DRAGON,
             rot_x=math.radians(16))
mouth = ic.box("dragon_mouth", 0.024, 0.020, 0.014, 0, -0.262, 0.138, M_FIRE)
for i, hx in enumerate((-0.018, 0.018)):
    horn = ic.box(f"dragon_horn_{i}", 0.008, 0.010, 0.026, hx, -0.206, 0.180, M_DECK,
                  rot_x=math.radians(28))
    ic.parent_to(horn, dragon)
for part in (snout, jaw, mouth):
    ic.parent_to(part, dragon)
ic.parent_to(neck, body)
ic.parent_to(dragon, body)

# 거북 꼬리: 고물에서 위로 살짝 들린다
tail = ic.box("turtle_tail", 0.022, 0.048, 0.016, 0, 0.200, 0.082, M_DRAGON,
              rot_x=math.radians(-22))
ic.parent_to(tail, body)

# ── 돛대 2 + 정크 돛 2(등딱지를 뚫고 선다) + 세력색 기 ──
main_mast = ic.cylinder("mast", 0.008, 0.260, 0, 0.070, 0.190, M_BATTEN, verts=6)
ic.parent_to(main_mast, body)
main_sail = ic.box("sail", 0.095, 0.005, 0.135, 0, 0.070, 0.228, M_SAIL)
for i in range(4):
    batten = ic.box(f"sail_batten_{i}", 0.101, 0.007, 0.007, 0, 0.070, 0.174 + i * 0.036,
                    M_BATTEN)
    ic.parent_to(batten, main_sail)
flag = ic.box("sail_flag", 0.036, 0.004, 0.021, 0.024, 0.070, 0.334, m.red)
ic.parent_to(flag, main_sail)
ic.parent_to(main_sail, main_mast)

fore_mast = ic.cylinder("fore_mast", 0.007, 0.215, 0, -0.072, 0.165, M_BATTEN, verts=6)
ic.parent_to(fore_mast, body)
fore_sail = ic.box("sail2", 0.078, 0.005, 0.105, 0, -0.072, 0.204, M_SAIL)
for i in range(3):
    batten = ic.box(f"sail2_batten_{i}", 0.084, 0.007, 0.007, 0, -0.072, 0.166 + i * 0.036,
                    M_BATTEN)
    ic.parent_to(batten, fore_sail)
ic.parent_to(fore_sail, fore_mast)

# 전체 확대는 균등 스케일로만 — 축마다 다르게 주면 자식 회전 부위가 전단으로 일그러진다
body.scale = (1.35, 1.35, 1.35)

ic.export("troop-turtleship.glb")
