# 병종 2 — 기병(Cavalry, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_cavalry.py
#
# doc/spec-unit.md: [육지, 속도 3, 탐지 3, 사거리 1/1/1] 말을 타고 칼을 들고 있다.
# 폐기한 cavalry.glb(3기 고정·전부 사각형·피벗 버그)를 대체하는 1기짜리 모델이다.
#
# 부위 노드 규약 — 보병과 다른 이름을 써서 코드가 어느 쪽인지 구분할 수 있게 한다:
#   body (부모) ← 나머지 전부가 자식
#   leg_fl / leg_fr / leg_bl / leg_br : 말 다리(원점=고관절). 대각 트롯용
#   rider ← rider_arm_l / rider_arm_r : 기수 상체와 팔(원점=어깨)
#   sword_* : 오른팔 자식
# 정면은 -Y(남쪽) → Godot에서 +Z가 정면. 보병과 같다.
#
# 크기: 총높이 약 0.41(보병 0.275의 1.5배), 코끝~꼬리 약 0.38.
# 9기 편대에서도 육각을 넘지 않도록 몸통을 짧게 잡았다.
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

# 고관절은 몸통 타원체 "속"에 있어야 한다. 다리 위끝을 몸통 표면 높이에 맞추면
# 타원체가 그 지점에서 위로 휘어 있어 사이가 벌어진다 — 다리를 몸통 안까지 밀어 넣는다.
HIP_Z = 0.166          # 말 고관절 높이 = 다리 길이(위끝이 몸통 속에 묻힌다)
BARREL_Z = 0.178       # 몸통 중심
SHOULDER_Z = 0.291     # 기수 어깨
HAND_Z = 0.233         # 기수 손

# ── 말 몸통: 눌린 타원체. 계획 2(반곡선)대로 여기만 스무스 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=1.0,
                                     location=(0, 0.01, BARREL_Z))
body = bpy.context.object
body.name = "body"
body.scale = (0.065, 0.128, 0.055)
body.data.materials.append(M_COAT)
ic.shade_smooth(body)
ic.bake_scale(body)

# ── 다리 4개 — 피벗을 고관절로 옮겨 스윙 가능. 발굽은 다리 자식 ──
for tag, lx, ly in (("fl", -0.030, -0.055), ("fr", 0.030, -0.055),
                    ("bl", -0.030, 0.070), ("br", 0.030, 0.070)):
    leg = ic.box(f"leg_{tag}", 0.022, 0.024, HIP_Z, lx, ly, HIP_Z, M_COAT,
                 origin_shift=(0, 0, -HIP_Z / 2))
    hoof = ic.box(f"hoof_{tag}", 0.028, 0.030, 0.018, lx, ly, 0.009, M_HOOF)
    ic.parent_to(hoof, leg)
    ic.parent_to(leg, body)

# ── 목·머리·귀·갈기·꼬리 ──
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

# ── 안장 + 안장천(세력색). 천은 몸통보다 넓게 잡아 옆으로 늘어뜨린다 ──
# 안장은 판 하나로 두면 상자처럼 보인다 — 앞턱·뒤턱·다리받이로 층을 만든다.
cloth = ic.box("saddle_cloth", 0.145, 0.135, 0.012, 0, 0.012, 0.218, m.red)
saddle = ic.box("saddle", 0.074, 0.082, 0.014, 0, 0.012, 0.233, M_LEATHER)
pommel = ic.box("saddle_pommel", 0.042, 0.014, 0.024, 0, -0.028, 0.245, M_LEATHER)
cantle = ic.box("saddle_cantle", 0.050, 0.016, 0.028, 0, 0.050, 0.247, M_LEATHER)
for i, fx in enumerate((-0.058, 0.058)):
    flap = ic.box(f"saddle_flap_{i}", 0.012, 0.072, 0.050, fx, 0.000, 0.203, M_LEATHER)
    ic.parent_to(flap, body)
for part in (cloth, saddle, pommel, cantle):
    ic.parent_to(part, body)

# ── 기수: 상체(스무스) + 머리 + 투구 + 투구술(세력색) ──
rider = ic.cone("rider", 0.034, 0.042, 0.070, 0, 0.012, 0.269, m.armor, smooth=True)
rhead = ic.box("rider_head", 0.044, 0.042, 0.040, 0, 0.012, 0.323, m.skin)
helmet = ic.cone("helmet", 0.031, 0.012, 0.026, 0, 0.012, 0.353, m.armor, verts=6)
plume = ic.box("plume", 0.010, 0.010, 0.026, 0, 0.012, 0.377, m.red)

for part in (rhead, helmet, plume):
    ic.parent_to(part, rider)

# ── 기수 다리: 안장 옆으로 벌려 앉는다. 허벅지는 앞아래로, 정강이는 뒤로 꺾여 등자를 밟는다.
# x는 몸통 최대 반경(0.065)보다 바깥이어야 말을 뚫지 않는다.
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

# ── 기수 팔 2개 — 피벗을 어깨로. 왼팔은 고삐를 쥐듯 앞으로, 오른팔은 칼을 세워 든다 ──
arms = {}
for tag, ax, pitch in (("l", -0.042, math.radians(-26)), ("r", 0.042, math.radians(12))):
    arm = ic.box(f"rider_arm_{tag}", 0.022, 0.024, 0.058, ax, 0.012, SHOULDER_Z, m.armor,
                 rot_x=pitch, origin_shift=(0, 0, -0.029))
    ic.parent_to(arm, rider)
    arms[tag] = arm

# ── 칼: 오른손에서 위로 세워 든다(도검병과 같은 양식) ──
SWORD_TILT = math.radians(-12)
HX, HY = 0.048, 0.000

grip = ic.cylinder("sword_grip", 0.007, 0.030, HX, HY, HAND_Z + 0.004, m.wood,
                   verts=6, rot_x=SWORD_TILT)
guard = ic.box("sword_guard", 0.030, 0.012, 0.008, HX, HY - 0.004, HAND_Z + 0.021,
               m.steel, rot_x=SWORD_TILT)
blade = ic.box("sword_blade", 0.014, 0.008, 0.094, HX, HY - 0.014, HAND_Z + 0.073,
               m.steel, rot_x=SWORD_TILT)
tip = ic.cone("sword_tip", 0.010, 0.001, 0.020, HX, HY - 0.026, HAND_Z + 0.130,
              m.steel, verts=4, rot_x=SWORD_TILT)

for part in (guard, blade, tip):
    ic.parent_to(part, grip)
ic.parent_to(grip, arms["r"])

ic.export("troop-cavalry.glb")
