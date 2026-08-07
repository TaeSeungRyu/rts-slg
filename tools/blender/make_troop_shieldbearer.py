# 병종 13 — 등갑병(Shieldbearer, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_shieldbearer.py
#
# doc/spec-unit.md: [육지, 속도 1, 탐지 2, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-07): 도검병 참고 — 방패는 크게, 몸집은 두껍게.
# 등갑(등나무 갑옷) 톤. 이동·공격 모션은 도검병과 동일(보병 규약 이름 재사용).
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_RATTAN = ic.make_mat("rattan", (0.62, 0.48, 0.22))
M_RATTAN2 = ic.make_mat("rattan2", (0.52, 0.39, 0.17))

# ── 두꺼운 몸통: 등갑 원뿔(폭 1.3배) + 등갑 치마 ──
body = ic.cone("body", 0.047, 0.058, 0.070, 0, 0, 0.139, M_RATTAN, smooth=True)
skirt = ic.cone("skirt", 0.066, 0.052, 0.042, 0, 0, 0.088, M_RATTAN2, smooth=True)

neck = ic.cylinder("neck", 0.018, 0.020, 0, 0, 0.180, m.skin, smooth=True)
head = ic.box("head", 0.050, 0.048, 0.042, 0, 0, 0.209, m.skin)
helmet = ic.cone("helmet", 0.038, 0.012, 0.026, 0, 0, 0.240, M_RATTAN2, verts=6)
plume = ic.box("plume", 0.010, 0.010, 0.024, 0, 0, 0.263, m.red)
for part in (skirt, neck, head, helmet, plume):
    ic.parent_to(part, body)

# ── 굵은 다리·발 ──
for tag, lx in (("l", -0.028), ("r", 0.028)):
    leg = ic.box(f"leg_{tag}", 0.034, 0.036, ic.HIP_Z, lx, 0, ic.HIP_Z, M_RATTAN2,
                 origin_shift=(0, 0, -ic.HIP_Z / 2))
    foot = ic.box(f"foot_{tag}", 0.036, 0.052, 0.016, lx, -0.010, 0.008, M_RATTAN2)
    ic.parent_to(foot, leg)
    ic.parent_to(leg, body)

# ── 굵은 팔(왼팔은 방패를 들도록 앞으로) ──
arms = {}
for tag, ax, pitch in (("l", -(ic.ARM_X + 0.006), math.radians(-14)),
                       ("r", ic.ARM_X + 0.006, math.radians(10))):
    arm = ic.box(f"arm_{tag}", 0.030, 0.032, 0.068, ax, 0, ic.SHOULDER_Z, M_RATTAN,
                 rot_x=pitch, origin_shift=(0, 0, -0.034))
    ic.parent_to(arm, body)
    arms[tag] = arm

# ── 칼: 도검병과 같은 양식 ──
SWORD_TILT = math.radians(-12)
HX, HY = ic.ARM_X + 0.012, -0.014
grip = ic.cylinder("sword_grip", 0.007, 0.032, HX, HY, ic.HAND_Z + 0.004, m.wood,
                   verts=6, rot_x=SWORD_TILT)
guard = ic.box("sword_guard", 0.032, 0.012, 0.008, HX, HY - 0.004, ic.HAND_Z + 0.022,
               m.steel, rot_x=SWORD_TILT)
blade = ic.box("sword_blade", 0.015, 0.008, 0.100, HX, HY - 0.015, ic.HAND_Z + 0.077,
               m.steel, rot_x=SWORD_TILT)
tip = ic.cone("sword_tip", 0.011, 0.001, 0.022, HX, HY - 0.028, ic.HAND_Z + 0.138,
              m.steel, verts=4, rot_x=SWORD_TILT)
for part in (guard, blade, tip):
    ic.parent_to(part, grip)
ic.parent_to(grip, arms["r"])

# ── 큰 방패(타워): 몸을 거의 가리는 등갑 판 + 테두리 + 세력색 문양 ──
SX, SY, SZ = -(ic.ARM_X + 0.020), -0.040, 0.118
panel = ic.box("shield", 0.098, 0.014, 0.150, SX, SY, SZ, M_RATTAN)
rim_t = 0.012
for tag, dx, dz, sx, sz in (("t", 0.0, 0.078, 0.108, rim_t), ("b", 0.0, -0.078, 0.108, rim_t),
                            ("l", -0.052, 0.0, rim_t, 0.168), ("r", 0.052, 0.0, rim_t, 0.168)):
    ic.parent_to(
        ic.box(f"shield_rim_{tag}", sx, 0.016, sz, SX + dx, SY, SZ + dz, M_RATTAN2), panel)
boss = ic.box("shield_boss", 0.036, 0.010, 0.036, SX, SY - 0.010, SZ, m.red,
              rot_y=math.radians(45))
ic.parent_to(boss, panel)
ic.parent_to(panel, arms["l"])

ic.export("troop-shieldbearer.glb")
