# 이벤트 유닛 21 — 도적(Bandit, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_bandit.py
#
# doc/spec-unit.md: [육지, 속도 2, 탐지 2, 사거리 1/1/1] 도적 무리.
# 스탯·모션은 도검병과 같고 외형만 다르다(2026-08-07 사용자 확정):
# 투구 없이 두건, 누더기 천 옷(가슴 헝겊 조각), 맨팔·맨다리, 거친 도 + 작은 나무 버클러.
# 중립(이벤트) 유닛이라 세력색 red 재질을 쓰지 않는다.
# 부위 이름은 보병 규약(body/leg_*/arm_*)이라 행군·휘두르기 모션이 자동 재사용된다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_RAG = ic.make_mat("rag", (0.35, 0.31, 0.24))
M_RAG2 = ic.make_mat("rag2", (0.27, 0.23, 0.17))
M_PATCH = ic.make_mat("patch", (0.44, 0.38, 0.26))
M_SCARF = ic.make_mat("scarf", (0.32, 0.16, 0.12))
M_HAIR = ic.make_mat("hair", (0.10, 0.08, 0.06))

# ── 몸통: 누더기 천 옷 + 가슴 헝겊 조각 + 해진 치마 ──
body = ic.cone("body", 0.036, 0.045, 0.068, 0, 0, 0.138, M_RAG, smooth=True)
patch = ic.box("chest_patch", 0.026, 0.006, 0.024, 0.010, -0.041, 0.148, M_PATCH,
               rot_x=math.radians(6))
skirt = ic.cone("skirt", 0.050, 0.040, 0.036, 0, 0, 0.090, M_RAG2, smooth=True)

# 머리: 투구 없이 두건 — 이마띠 + 정수리 덮개 + 뒤통수 매듭, 뒤로 삐져나온 머리칼
neck = ic.cylinder("neck", 0.016, 0.020, 0, 0, 0.178, m.skin, smooth=True)
head = ic.box("head", 0.048, 0.046, 0.042, 0, 0, 0.207, m.skin)
band = ic.box("scarf_band", 0.052, 0.050, 0.014, 0, 0, 0.226, M_SCARF)
# 복면: 얼굴 아래 절반을 감아 눈만 보인다(두건 띠와 사이가 눈 자리)
mask = ic.box("face_mask", 0.052, 0.050, 0.020, 0, 0, 0.195, M_SCARF)
mask_knot = ic.box("mask_knot", 0.012, 0.014, 0.012, 0, 0.030, 0.192, M_SCARF)
cap = ic.box("scarf_cap", 0.044, 0.042, 0.014, 0, 0.002, 0.238, M_SCARF)
knot = ic.box("scarf_knot", 0.014, 0.018, 0.016, 0, 0.030, 0.222, M_SCARF,
              rot_x=math.radians(-24))
hair = ic.box("hair_tail", 0.016, 0.012, 0.026, 0, 0.028, 0.202, M_HAIR,
              rot_x=math.radians(-14))
for part in (patch, skirt, neck, head, band, mask, mask_knot, cap, knot, hair):
    ic.parent_to(part, body)

# ── 맨다리 + 해진 발싸개 ──
for tag, lx in (("l", -0.024), ("r", 0.024)):
    leg = ic.box(f"leg_{tag}", 0.026, 0.028, ic.HIP_Z, lx, 0, ic.HIP_Z, m.skin,
                 origin_shift=(0, 0, -ic.HIP_Z / 2))
    foot = ic.box(f"foot_{tag}", 0.028, 0.046, 0.014, lx, -0.010, 0.007, M_RAG2)
    ic.parent_to(foot, leg)
    ic.parent_to(leg, body)

# ── 맨팔 2(피벗=어깨) ──
arms = {}
for tag, ax, pitch in (("l", -ic.ARM_X, math.radians(-14)), ("r", ic.ARM_X, math.radians(10))):
    arm = ic.box(f"arm_{tag}", 0.022, 0.024, 0.066, ax, 0, ic.SHOULDER_Z, m.skin,
                 rot_x=pitch, origin_shift=(0, 0, -0.033))
    ic.parent_to(arm, body)
    arms[tag] = arm

# ── 거친 도: 이 빠진 넓은 외날칼(남만병 양식보다 뭉툭) ──
DTILT = math.radians(-12)
HX, HY = ic.ARM_X + 0.006, -0.014

grip = ic.cylinder("dao_grip", 0.007, 0.030, HX, HY, ic.HAND_Z + 0.004, m.wood,
                   verts=6, rot_x=DTILT)
guard = ic.box("dao_guard", 0.024, 0.010, 0.006, HX, HY - 0.004, ic.HAND_Z + 0.021,
               m.steel, rot_x=DTILT)
blade = ic.box("dao_blade", 0.007, 0.022, 0.072, HX, HY - 0.012, ic.HAND_Z + 0.058,
               m.steel, rot_x=DTILT)
blade_tip = ic.box("dao_tip", 0.007, 0.018, 0.030, HX, HY - 0.026, ic.HAND_Z + 0.104,
                   m.steel, rot_x=DTILT + math.radians(-20))
notch = ic.box("dao_notch", 0.009, 0.008, 0.008, HX, HY - 0.020, ic.HAND_Z + 0.076,
               M_RAG2, rot_x=DTILT)
for part in (guard, blade, blade_tip, notch):
    ic.parent_to(part, grip)
ic.parent_to(grip, arms["r"])

# ── 작은 나무 버클러: 왼팔 바깥에 묶었다 ──
BX, BY = -(ic.ARM_X + 0.014), -0.006
shield = ic.cylinder("buckler", 0.030, 0.010, BX, BY, ic.HAND_Z + 0.026, m.wood,
                     verts=10, rot_y=math.radians(90))
boss = ic.cylinder("buckler_boss", 0.009, 0.016, BX - 0.004, BY, ic.HAND_Z + 0.026,
                   m.steel, verts=8, rot_y=math.radians(90))
ic.parent_to(boss, shield)
ic.parent_to(shield, arms["l"])

ic.export("troop-bandit.glb")
