# 병종 16 — 궁기병(HorseArcher, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_horse_archer.py
#
# doc/spec-unit.md: [육지, 속도 3, 탐지 3, 사거리 2/1/1] 말 위에서 활을 쏘는 경기병.
# 말·안장·기수 몸은 기병(make_troop_cavalry.py)과 같고 무기만 칼 → 활로 바뀐다.
# leg_fl 규약으로 갤럽이, bow_grip·arrow 규약으로 기마 사격 분기(_horseArcher)가 걸린다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_COAT = ic.make_mat("coat", (0.36, 0.22, 0.13))
M_MANE = ic.make_mat("mane", (0.12, 0.09, 0.07))
M_HOOF = ic.make_mat("hoof", (0.15, 0.13, 0.12))
M_LEATHER = ic.make_mat("leather", (0.30, 0.19, 0.11))
M_STRING = ic.make_mat("string", (0.85, 0.82, 0.72), roughness=0.6)
M_FLETCH = ic.make_mat("fletch", (0.88, 0.88, 0.86))

HIP_Z = 0.166
BARREL_Z = 0.178
SHOULDER_Z = 0.291
HAND_Z = 0.233

# ── 말: 기병과 동일 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=1.0,
                                     location=(0, 0.01, BARREL_Z))
body = bpy.context.object
body.name = "body"
body.scale = (0.065, 0.128, 0.055)
body.data.materials.append(M_COAT)
ic.shade_smooth(body)
ic.bake_scale(body)

for tag, lx, ly in (("fl", -0.030, -0.055), ("fr", 0.030, -0.055),
                    ("bl", -0.030, 0.070), ("br", 0.030, 0.070)):
    leg = ic.box(f"leg_{tag}", 0.022, 0.024, HIP_Z, lx, ly, HIP_Z, M_COAT,
                 origin_shift=(0, 0, -HIP_Z / 2))
    hoof = ic.box(f"hoof_{tag}", 0.028, 0.030, 0.018, lx, ly, 0.009, M_HOOF)
    ic.parent_to(hoof, leg)
    ic.parent_to(leg, body)

neck = ic.cone("neck", 0.040, 0.028, 0.115, 0, -0.098, 0.216, M_COAT,
               rot_x=math.radians(38), smooth=True)
head = ic.box("head", 0.042, 0.088, 0.042, 0, -0.163, 0.263, M_COAT,
              rot_x=math.radians(20))
for i, ex in enumerate((-0.014, 0.014)):
    ear = ic.box(f"ear_{i}", 0.010, 0.012, 0.022, ex, -0.132, 0.289, M_COAT)
    ic.parent_to(ear, head)
mane = ic.box("mane", 0.018, 0.052, 0.100, 0, -0.088, 0.233, M_MANE,
              rot_x=math.radians(38))
tail = ic.box("tail", 0.024, 0.028, 0.105, 0, 0.128, 0.169, M_MANE,
              rot_x=math.radians(-32))
for part in (neck, head, mane, tail):
    ic.parent_to(part, body)

cloth = ic.box("saddle_cloth", 0.145, 0.135, 0.012, 0, 0.012, 0.218, m.red)
saddle = ic.box("saddle", 0.074, 0.082, 0.014, 0, 0.012, 0.233, M_LEATHER)
pommel = ic.box("saddle_pommel", 0.042, 0.014, 0.024, 0, -0.028, 0.245, M_LEATHER)
cantle = ic.box("saddle_cantle", 0.050, 0.016, 0.028, 0, 0.050, 0.247, M_LEATHER)
for i, fx in enumerate((-0.058, 0.058)):
    flap = ic.box(f"saddle_flap_{i}", 0.012, 0.072, 0.050, fx, 0.000, 0.203, M_LEATHER)
    ic.parent_to(flap, body)
for part in (cloth, saddle, pommel, cantle):
    ic.parent_to(part, body)

# ── 기수: 기병과 같은 몸, 무기만 활 ──
rider = ic.cone("rider", 0.034, 0.042, 0.070, 0, 0.012, 0.269, m.armor, smooth=True)
rhead = ic.box("rider_head", 0.044, 0.042, 0.040, 0, 0.012, 0.323, m.skin)
helmet = ic.cone("helmet", 0.031, 0.012, 0.026, 0, 0.012, 0.353, m.armor, verts=6)
plume = ic.box("plume", 0.010, 0.010, 0.026, 0, 0.012, 0.377, m.red)
for part in (rhead, helmet, plume):
    ic.parent_to(part, rider)

LEG_X = 0.068
for tag, sx in (("l", -LEG_X), ("r", LEG_X)):
    thigh = ic.box(f"rider_thigh_{tag}", 0.026, 0.028, 0.060, sx, -0.010, 0.213, m.armor,
                   rot_x=math.radians(-30))
    shin = ic.box(f"rider_shin_{tag}", 0.024, 0.026, 0.058, sx, -0.014, 0.157, m.armor,
                  rot_x=math.radians(10))
    boot = ic.box(f"rider_boot_{tag}", 0.026, 0.042, 0.018, sx, -0.024, 0.128, M_LEATHER)
    strap = ic.box(f"stirrup_strap_{tag}", 0.006, 0.008, 0.070, sx - 0.004 * (1 if sx > 0 else -1),
                   0.006, 0.171, M_LEATHER)
    stirrup = ic.box(f"stirrup_{tag}", 0.022, 0.028, 0.008, sx, -0.020, 0.116, m.steel)
    for part in (thigh, shin, boot, strap, stirrup):
        ic.parent_to(part, rider)

ic.parent_to(rider, body)

# 왼팔은 활을 앞으로 뻗어 들고(-38도, 보병 궁병과 같은 각), 오른팔은 화살을 쥔다
arms = {}
for tag, ax, pitch in (("l", -0.042, math.radians(-38)), ("r", 0.042, math.radians(-6))):
    arm = ic.box(f"rider_arm_{tag}", 0.022, 0.024, 0.058, ax, 0.012, SHOULDER_Z, m.armor,
                 rot_x=pitch, origin_shift=(0, 0, -0.029))
    ic.parent_to(arm, rider)
    arms[tag] = arm

# ── 활(보병 궁병과 동일 양식, 왼팔 자식) ──
BX = -0.054
BY = -0.040
LIMB = 0.085
grip = ic.box("bow_grip", 0.012, 0.014, 0.034, BX, BY, HAND_Z + 0.010, m.wood)
limb_u = ic.box("bow_limb_u", 0.010, 0.011, LIMB, BX, BY - 0.012,
                HAND_Z + 0.010 + 0.017 + LIMB / 2, m.wood, rot_x=math.radians(-16))
limb_d = ic.box("bow_limb_d", 0.010, 0.011, LIMB, BX, BY - 0.012,
                HAND_Z + 0.010 - 0.017 - LIMB / 2, m.wood, rot_x=math.radians(16))
string = ic.box("bow_string", 0.004, 0.004, 0.196, BX, BY + 0.021, HAND_Z + 0.010, M_STRING)
for part in (limb_u, limb_d, string):
    ic.parent_to(part, grip)
ic.parent_to(grip, arms["l"])

# ── 화살(오른팔 자식) ──
AX, AY = 0.046, -0.018
shaft = ic.box("arrow", 0.006, 0.120, 0.006, AX, AY, HAND_Z + 0.002, m.wood)
head_a = ic.cone("arrow_head", 0.007, 0.001, 0.018, AX, AY - 0.069, HAND_Z + 0.002,
                 m.steel, verts=4, rot_x=math.radians(-90))
fletch = ic.box("arrow_fletch", 0.003, 0.024, 0.016, AX, AY + 0.052, HAND_Z + 0.002, M_FLETCH)
for part in (head_a, fletch):
    ic.parent_to(part, shaft)
ic.parent_to(shaft, arms["r"])

# ── 화살통: 기수 등에 비스듬히 ──
QX, QY = 0.024, 0.050
quiver = ic.cylinder("quiver", 0.015, 0.090, QX, QY, 0.298, m.wood,
                     verts=6, rot_x=math.radians(12))
for i, (dx, dz) in enumerate(((-0.007, 0.0), (0.008, 0.008))):
    qf = ic.box(f"quiver_fletch_{i}", 0.004, 0.016, 0.020,
                QX + dx, QY + 0.012, 0.350 + dz, M_FLETCH, rot_x=math.radians(12))
    ic.parent_to(qf, quiver)
ic.parent_to(quiver, rider)

ic.export("troop-horse-archer.glb")
