# 병종 14 — 무당비군(Wudang, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_wudang.py
#
# doc/design-unit.md: [육지+산악, 속도 2(산악 1), 탐지 2, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-07): 활과 방패, 가벼운 갑옷, 망토. 공격 모션은 궁병.
# 궁병과의 구분: 먹빛 경장(회색 갑옷 아님), 세력색 망토, 투구 대신 머리띠 +
# 높은 검은 깃 하나, 팔은 맨팔에 팔찌 보호대 — 비군(飛軍)다운 어둡고 민첩한 인상.
# bow_grip·arrow 등 궁병 규약 이름을 그대로 써서 사격 모션이 자동 재사용된다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_DARK = ic.make_mat("dark_garb", (0.17, 0.17, 0.22))
M_DARK2 = ic.make_mat("dark_garb2", (0.12, 0.12, 0.16))
M_FEATHER = ic.make_mat("feather_dark", (0.08, 0.08, 0.10))
M_STRING = ic.make_mat("string", (0.85, 0.82, 0.72), roughness=0.6)

# ── 먹빛 경장 몸통 + 어두운 치마 ──
body = ic.cone("body", 0.036, 0.045, 0.068, 0, 0, 0.138, M_DARK, smooth=True)
skirt = ic.cone("skirt", 0.050, 0.040, 0.036, 0, 0, 0.090, M_DARK2, smooth=True)

# 머리: 투구 없이 머리띠 + 높은 검은 깃 하나
neck = ic.cylinder("neck", 0.016, 0.020, 0, 0, 0.178, m.skin, smooth=True)
head = ic.box("head", 0.048, 0.046, 0.042, 0, 0, 0.207, m.skin)
band = ic.box("headband", 0.052, 0.050, 0.012, 0, 0, 0.226, M_DARK2)
crest = ic.box("crest", 0.008, 0.010, 0.062, 0.006, 0.012, 0.262, M_FEATHER,
               rot_x=math.radians(-12))
for part in (skirt, neck, head, band, crest):
    ic.parent_to(part, body)

# ── 어두운 다리 + 맨팔(팔찌 보호대) ──
for tag, lx in (("l", -0.024), ("r", 0.024)):
    leg = ic.box(f"leg_{tag}", 0.026, 0.028, ic.HIP_Z, lx, 0, ic.HIP_Z, M_DARK2,
                 origin_shift=(0, 0, -ic.HIP_Z / 2))
    foot = ic.box(f"foot_{tag}", 0.028, 0.046, 0.014, lx, -0.010, 0.007, M_DARK2)
    ic.parent_to(foot, leg)
    ic.parent_to(leg, body)

arms = {}
for tag, ax, pitch in (("l", -ic.ARM_X, math.radians(-38)), ("r", ic.ARM_X, math.radians(-6))):
    arm = ic.box(f"arm_{tag}", 0.022, 0.024, 0.066, ax, 0, ic.SHOULDER_Z, m.skin,
                 rot_x=pitch, origin_shift=(0, 0, -0.033))
    brace = ic.box(f"brace_{tag}", 0.026, 0.028, 0.018, ax, 0, ic.SHOULDER_Z, M_DARK2,
                   rot_x=pitch)
    brace.data.transform(__import__("mathutils").Matrix.Translation((0, 0, -0.052)))
    ic.parent_to(brace, arm)
    ic.parent_to(arm, body)
    arms[tag] = arm

# ── 세력색 망토: 등 뒤로 늘어지는 판 — 궁병과 가장 크게 갈리는 실루엣 ──
cape = ic.box("cape", 0.082, 0.007, 0.126, 0, 0.044, 0.124, m.red,
              rot_x=math.radians(9))
ic.parent_to(cape, body)

# ── 활(궁병과 동일 양식, 왼팔 자식) ──
BX = -(ic.ARM_X + 0.010)
BY = -0.052
LIMB = 0.085
grip = ic.box("bow_grip", 0.012, 0.014, 0.034, BX, BY, ic.HAND_Z + 0.010, m.wood)
limb_u = ic.box("bow_limb_u", 0.010, 0.011, LIMB, BX, BY - 0.012, ic.HAND_Z + 0.010 + 0.017 + LIMB / 2,
                m.wood, rot_x=math.radians(-16))
limb_d = ic.box("bow_limb_d", 0.010, 0.011, LIMB, BX, BY - 0.012, ic.HAND_Z + 0.010 - 0.017 - LIMB / 2,
                m.wood, rot_x=math.radians(16))
string = ic.box("bow_string", 0.004, 0.004, 0.196, BX, BY + 0.021, ic.HAND_Z + 0.010, M_STRING)
for part in (limb_u, limb_d, string):
    ic.parent_to(part, grip)
ic.parent_to(grip, arms["l"])

# ── 화살(오른팔 자식) ──
AX, AY = ic.ARM_X + 0.004, -0.030
shaft = ic.box("arrow", 0.006, 0.120, 0.006, AX, AY, ic.HAND_Z + 0.002, m.wood)
head_a = ic.cone("arrow_head", 0.007, 0.001, 0.018, AX, AY - 0.069, ic.HAND_Z + 0.002,
                 m.steel, verts=4, rot_x=math.radians(-90))
ic.parent_to(head_a, shaft)
ic.parent_to(shaft, arms["r"])

# ── 등의 둥근 방패(망토 위에 얹힘) ──
BYS = 0.058
shield = ic.cylinder("shield_round", 0.044, 0.010, 0.004, BYS, 0.152, M_DARK2,
                     verts=12, rot_x=math.radians(90))
rim = ic.cylinder("shield_rim", 0.047, 0.016, 0.004, BYS, 0.152, m.steel,
                  verts=12, rot_x=math.radians(90))
boss = ic.cylinder("shield_boss", 0.013, 0.018, 0.004, BYS - 0.004, 0.152, m.red,
                   verts=8, rot_x=math.radians(90))
for part in (rim, boss):
    ic.parent_to(part, shield)
ic.parent_to(shield, body)

ic.export("troop-wudang.glb")
