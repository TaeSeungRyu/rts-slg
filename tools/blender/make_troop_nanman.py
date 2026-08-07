# 병종 12 — 남만병(Nanman, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_nanman.py
#
# doc/spec-unit.md: [육지, 속도 2, 탐지 2, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-07): 도(칼)를 든 남만 전사 — 맨팔, 가죽 갑옷, 맨다리,
# 머리에 깃털 장식(세력색), 등에 둥근 방패. 이동·공격 모션은 도검병과 동일.
#
# 부위 이름은 보병 규약(body/leg_*/arm_*)을 그대로 써서 행군·휘두르기 모션을 재사용한다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_LEATHER = ic.make_mat("leather", (0.44, 0.27, 0.13))
M_LEATHER2 = ic.make_mat("leather2", (0.36, 0.21, 0.10))

# ── 몸통: 가죽 갑옷 원뿔(스무스) + 허리 가죽 치마 ──
body = ic.cone("body", 0.036, 0.045, 0.068, 0, 0, 0.138, M_LEATHER, smooth=True)
skirt = ic.cone("skirt", 0.050, 0.040, 0.036, 0, 0, 0.090, M_LEATHER2, smooth=True)

# 목·머리 — 투구 없이 가죽 머리띠 + 세력색 깃털 2
neck = ic.cylinder("neck", 0.016, 0.020, 0, 0, 0.178, m.skin, smooth=True)
head = ic.box("head", 0.048, 0.046, 0.042, 0, 0, 0.207, m.skin)
band = ic.box("headband", 0.052, 0.050, 0.012, 0, 0, 0.224, M_LEATHER2)
feather1 = ic.box("feather1", 0.008, 0.006, 0.042, 0.012, 0.010, 0.252, m.red,
                  rot_x=math.radians(-10))
feather2 = ic.box("feather2", 0.008, 0.006, 0.034, -0.010, 0.012, 0.248, m.red,
                  rot_x=math.radians(-18))
for part in (skirt, neck, head, band, feather1, feather2):
    ic.parent_to(part, body)

# ── 맨다리 2 + 맨발(피벗=고관절) ──
for tag, lx in (("l", -0.024), ("r", 0.024)):
    leg = ic.box(f"leg_{tag}", 0.026, 0.028, ic.HIP_Z, lx, 0, ic.HIP_Z, m.skin,
                 origin_shift=(0, 0, -ic.HIP_Z / 2))
    foot = ic.box(f"foot_{tag}", 0.028, 0.046, 0.014, lx, -0.010, 0.007, m.skin)
    ic.parent_to(foot, leg)
    ic.parent_to(leg, body)

# ── 맨팔 2(피벗=어깨) ──
arms = {}
for tag, ax, pitch in (("l", -ic.ARM_X, math.radians(-14)), ("r", ic.ARM_X, math.radians(10))):
    arm = ic.box(f"arm_{tag}", 0.022, 0.024, 0.066, ax, 0, ic.SHOULDER_Z, m.skin,
                 rot_x=pitch, origin_shift=(0, 0, -0.033))
    ic.parent_to(arm, body)
    arms[tag] = arm

# ── 도(넓고 휜 외날칼): 곧은 몸날 + 꺾인 끝날 두 마디로 곡도를 흉내 ──
DTILT = math.radians(-12)
HX, HY = ic.ARM_X + 0.006, -0.014

grip = ic.cylinder("dao_grip", 0.007, 0.030, HX, HY, ic.HAND_Z + 0.004, m.wood,
                   verts=6, rot_x=DTILT)
guard = ic.cylinder("dao_guard", 0.016, 0.006, HX, HY - 0.004, ic.HAND_Z + 0.021,
                    m.steel, verts=8, rot_x=DTILT)
blade = ic.box("dao_blade", 0.006, 0.020, 0.080, HX, HY - 0.013, ic.HAND_Z + 0.062,
               m.steel, rot_x=DTILT)
blade_tip = ic.box("dao_tip", 0.006, 0.017, 0.036, HX, HY - 0.030, ic.HAND_Z + 0.114,
                   m.steel, rot_x=DTILT + math.radians(-24))
for part in (guard, blade, blade_tip):
    ic.parent_to(part, grip)
ic.parent_to(grip, arms["r"])

# ── 등의 둥근 방패: 원판 + 테두리 + 세력색 돌기. 몸통 자식(등짐) ──
BY = 0.052
shield = ic.cylinder("shield_round", 0.048, 0.010, 0.004, BY, 0.150, m.wood,
                     verts=12, rot_x=math.radians(90))
rim = ic.cylinder("shield_rim", 0.051, 0.016, 0.004, BY, 0.150, m.steel,
                  verts=12, rot_x=math.radians(90))
boss = ic.cylinder("shield_boss", 0.014, 0.018, 0.004, BY + 0.004, 0.150, m.red,
                   verts=8, rot_x=math.radians(90))
for part in (rim, boss):
    ic.parent_to(part, shield)
ic.parent_to(shield, body)

ic.export("troop-nanman.glb")
