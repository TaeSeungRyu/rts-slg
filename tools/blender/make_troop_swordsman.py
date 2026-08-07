# 병종 1 — 도검병(Swordsman, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_swordsman.py
#
# doc/spec-unit.md: [육지, 이동력 3, 사거리 1/1/1] 칼과 방패를 들고 있다.
# 몸통은 infantry_common.build_body가 세우고 여기서는 무기만 얹는다(보병 7종 공용).
#   sword  : 오른팔 자식 — 칼자루·코등이·칼날
#   shield : 왼팔 자식 — 방패판·테두리·문양(세력색)
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()

# 왼팔은 방패를 앞으로 들도록 살짝 앞으로, 오른팔은 칼을 세워 들도록 뒤로 당긴다
body, arm_l, arm_r = ic.build_body(m, arm_l_pitch=math.radians(-14), arm_r_pitch=math.radians(10))

# ── 칼: 오른손에서 위로 세워 든다(약간 앞으로 기움) ──
SWORD_TILT = math.radians(-12)
HX, HY = ic.ARM_X + 0.006, -0.014

grip = ic.cylinder("sword_grip", 0.007, 0.032, HX, HY, ic.HAND_Z + 0.004, m.wood,
                   verts=6, rot_x=SWORD_TILT)
guard = ic.box("sword_guard", 0.032, 0.012, 0.008, HX, HY - 0.004, ic.HAND_Z + 0.022,
               m.steel, rot_x=SWORD_TILT)
blade = ic.box("sword_blade", 0.015, 0.008, 0.100, HX, HY - 0.015, ic.HAND_Z + 0.077,
               m.steel, rot_x=SWORD_TILT)
# 칼끝: 날 위에 얹는 사각뿔
tip = ic.cone("sword_tip", 0.011, 0.001, 0.022, HX, HY - 0.028, ic.HAND_Z + 0.138,
              m.steel, verts=4, rot_x=SWORD_TILT)

for part in (guard, blade, tip):
    ic.parent_to(part, grip)
ic.parent_to(grip, arm_r)

# ── 방패: 왼팔 앞쪽에 세로로 든다 ──
SX, SY, SZ = -(ic.ARM_X + 0.014), -0.034, 0.128

panel = ic.box("shield", 0.066, 0.014, 0.086, SX, SY, SZ, m.wood)
# 테두리는 판 바깥으로 살짝 키워 감싼다(같은 평면이 생기지 않게 두께를 다르게 준다)
rim_t = 0.010
for tag, dx, dz, sx, sz in (("t", 0.0, 0.046, 0.074, rim_t), ("b", 0.0, -0.046, 0.074, rim_t),
                            ("l", -0.037, 0.0, rim_t, 0.098), ("r", 0.037, 0.0, rim_t, 0.098)):
    ic.parent_to(
        ic.box(f"shield_rim_{tag}", sx, 0.016, sz, SX + dx, SY, SZ + dz, m.steel), panel)
# 가운데 문양(세력색) — 판 앞면에서 확실히 돌출시킨다
boss = ic.box("shield_boss", 0.030, 0.010, 0.030, SX, SY - 0.010, SZ, m.red,
              rot_y=math.radians(45))
ic.parent_to(boss, panel)

ic.parent_to(panel, arm_l)

ic.export("troop-swordsman.glb")
