# 병종 7 — 상병(WarElephant, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_war_elephant.py
#
# doc/design-unit.md: [육지, 속도 2, 탐지 2, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-06): 코끼리 좌우에 아주 작은 사람.
# 이동은 걷기(다리 4개), 공격은 들이받기 — 이때 코(trunk)가 움직여야 한다.
#
# 부위 노드 규약:
#   body (부모=코끼리 몸통) ← 다리·머리·등판이 자식
#   leg_fl/fr/bl/br : 다리(원점=고관절). 코끼리 판별은 trunk 노드가 먼저라 기병과 안 섞인다
#   head ← trunk(원점=코 뿌리, 런타임 회전) ← trunk_tip(고정 굽음)
#   walker0/1_* : 바닥에서 함께 걷는 꼬마 병사(0.42배). body가 아니라 루트에 둔다
#                 — body의 흔들림·앞쏠림에 딸려가면 발이 지면에서 뜬다
#   howdah_* : 등판 위 꼬마 병사(0.38배) — 공격 때 아무것도 하지 않는다
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic
from mathutils import Matrix

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_HIDE = ic.make_mat("hide", (0.44, 0.42, 0.44))
M_HIDE2 = ic.make_mat("hide2", (0.38, 0.36, 0.38))
M_IVORY = ic.make_mat("ivory", (0.90, 0.87, 0.78), roughness=0.5)

# ── 몸통(부모): 큰 타원체. 자식이 회전하므로 스케일을 굽는다 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=7, radius=1.0,
                                     location=(0, 0.015, 0.185))
body = bpy.context.object
body.name = "body"
body.scale = (0.080, 0.115, 0.085)
body.data.materials.append(M_HIDE)
ic.shade_smooth(body)
ic.bake_scale(body)

# ── 다리 4: 원기둥(스무스) + 몸통 이음새를 덮는 어깨 근육(몸통 자식) + 마디 선 2 ──
HIP = 0.185
for tag, lx, ly in (("fl", -0.048, -0.068), ("fr", 0.048, -0.068),
                    ("bl", -0.048, 0.076), ("br", 0.048, 0.076)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=9, radius=0.024, depth=HIP,
                                        location=(lx, ly, HIP))
    leg = bpy.context.object
    leg.name = f"leg_{tag}"
    leg.data.transform(Matrix.Translation((0, 0, -HIP / 2)))
    leg.data.materials.append(M_HIDE2)
    ic.shade_smooth(leg)
    for j, rz in enumerate((0.100, 0.046)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=9, radius=0.0262, depth=0.008,
                                            location=(lx, ly, rz))
        ring = bpy.context.object
        ring.name = f"leg_{tag}_ring{j}"
        ring.data.materials.append(M_HIDE)
        ic.parent_to(ring, leg)
    ic.parent_to(leg, body)
    # 어깨 근육: 다리와 몸통이 만나는 사각 이음새를 둥근 혹으로 덮는다. 몸통 자식(다리 스윙과 무관)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=9, ring_count=6, radius=0.036,
                                         location=(lx, ly, 0.150))
    pad = bpy.context.object
    pad.name = f"hip_{tag}"
    pad.scale = (1.0, 1.1, 0.82)
    pad.data.materials.append(M_HIDE)
    ic.shade_smooth(pad)
    ic.bake_scale(pad)
    ic.parent_to(pad, body)

# ── 머리: 타원체(자식 trunk가 회전하므로 스케일 굽기) + 귀 2 + 상아 2 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=1.0,
                                     location=(0, -0.148, 0.245))
head = bpy.context.object
head.name = "head"
head.scale = (0.055, 0.050, 0.052)
head.data.materials.append(M_HIDE)
ic.shade_smooth(head)
ic.bake_scale(head)

for sx in (-1, 1):
    ear = ic.box(f"ear_{'l' if sx < 0 else 'r'}", 0.012, 0.052, 0.068,
                 sx * 0.058, -0.132, 0.245, M_HIDE2, rot_y=math.radians(sx * 14))
    ic.parent_to(ear, head)
for sx in (-1, 1):
    tusk = ic.cone(f"tusk_{'l' if sx < 0 else 'r'}", 0.009, 0.001, 0.058,
                   sx * 0.026, -0.196, 0.216, M_IVORY, verts=6, rot_x=math.radians(-62))
    ic.parent_to(tusk, head)

# ── 코: 원점을 코 뿌리에 두고 아래로 뻗는다. 자식(끝마디)까지 붙인 뒤에 기본 각도를 준다 ──
TR_X, TR_Y, TR_Z = 0.0, -0.192, 0.232
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.021, radius2=0.015, depth=0.110,
                                location=(TR_X, TR_Y, TR_Z))
trunk = bpy.context.object
trunk.name = "trunk"
trunk.data.transform(Matrix.Translation((0, 0, -0.055)))
trunk.data.materials.append(M_HIDE2)
ic.shade_smooth(trunk)

bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=0.014, radius2=0.008, depth=0.080,
                                location=(TR_X, TR_Y, TR_Z - 0.110))
tip = bpy.context.object
tip.name = "trunk_tip"
tip.data.transform(Matrix.Translation((0, 0, -0.040)))
tip.data.materials.append(M_HIDE2)
ic.shade_smooth(tip)
tip.rotation_euler = (math.radians(-24), 0, 0)
ic.parent_to(tip, trunk)

trunk.rotation_euler = (math.radians(-10), 0, 0)
ic.parent_to(trunk, head)
ic.parent_to(head, body)

# ── 등판: 세력색 천 위에 나무 판자(축소), 그 위에 꼬마 병사 ──
cloth = ic.box("back_cloth", 0.125, 0.100, 0.008, 0, 0.018, 0.262, m.red)
plank = ic.box("back_plank", 0.095, 0.075, 0.010, 0, 0.018, 0.270, m.wood)
for part in (cloth, plank):
    ic.parent_to(part, body)

HS = 0.38
htor = ic.cone("howdah", 0.030 * HS, 0.038 * HS, 0.058 * HS, 0, 0.018, 0.287,
               m.armor, smooth=True)
hhead = ic.box("howdah_head", 0.040 * HS, 0.038 * HS, 0.036 * HS, 0, 0.018, 0.305, m.skin)
hhelm = ic.cone("howdah_helmet", 0.027 * HS, 0.010 * HS, 0.022 * HS, 0, 0.018, 0.316,
                m.armor, verts=6)
hplume = ic.box("howdah_plume", 0.006, 0.006, 0.013, 0, 0.018, 0.325, m.red)
for part in (hhead, hhelm, hplume):
    ic.parent_to(part, htor)
ic.parent_to(htor, body)
tail = ic.box("tail", 0.016, 0.018, 0.075, 0, 0.128, 0.190, M_HIDE2,
              rot_x=math.radians(18))
ic.parent_to(tail, body)

# ── 좌우 꼬마 병사(0.42배): 코끼리 옆 바닥을 함께 걷는다. body에 묶지 않는다 ──
S = 0.42
for i, sx in enumerate((-1, 1)):
    wx = sx * 0.108
    wy = -0.015
    torso = ic.cone(f"walker{i}_torso", 0.030 * S, 0.038 * S, 0.058 * S, wx, wy, 0.133 * S,
                    m.armor, smooth=True)
    whead = ic.box(f"walker{i}_head", 0.040 * S, 0.038 * S, 0.036 * S, wx, wy, 0.184 * S, m.skin)
    whelm = ic.cone(f"walker{i}_helmet", 0.027 * S, 0.010 * S, 0.022 * S, wx, wy, 0.210 * S,
                    m.armor, verts=6)
    hip = ic.HIP_Z * S
    for tag, lx in (("l", -0.020 * S), ("r", 0.020 * S)):
        leg = ic.box(f"walker{i}_leg_{tag}", 0.024 * S, 0.026 * S, hip, wx + lx, wy, hip,
                     m.cloth, origin_shift=(0, 0, -hip / 2))
        foot = ic.box(f"walker{i}_foot_{tag}", 0.026 * S, 0.044 * S, 0.014 * S,
                      wx + lx, wy - 0.008 * S, 0.007 * S, m.armor)
        ic.parent_to(foot, leg)
        ic.parent_to(leg, torso)
    for tag, ax in (("l", -0.036 * S), ("r", 0.036 * S)):
        arm = ic.box(f"walker{i}_arm_{tag}", 0.018 * S, 0.020 * S, 0.058 * S,
                     wx + ax * 2.2, wy, 0.132 * S, m.armor, rot_x=math.radians(-10))
        ic.parent_to(arm, torso)
    for part in (whead, whelm):
        ic.parent_to(part, torso)

ic.export("troop-war-elephant.glb")
