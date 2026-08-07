# 병종 4 — 벽력거(ThunderCart, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_thunder_cart.py
#
# doc/design-unit.md: [육지, 속도 1, 탐지 1, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-06): 거대한 연필 모양 말뚝이 수레에 실려 있고, 병사들이 끈다.
#
# 부위 노드 규약(공성 공통 — Catapult·SiegeTower가 재사용할 기반):
#   body (부모=수레 바닥) ← 나머지 전부가 자식
#   wheel_l / wheel_r : 바퀴. 이동 거리에 비례해 굴린다(회전을 메시에 구워 축이 X)
#   ram / ram_tip     : 연필 말뚝. 공격 때 앞으로 내지른다
#   crew{i}_leg_l/r   : 끄는 병사 다리(원점=고관절)
# 정면은 -Y(남쪽) → Godot에서 +Z가 정면.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_PENCIL = ic.make_mat("pencil", (0.72, 0.52, 0.22))


def bake_rotation(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


# ── 수레 바닥(부모) + 옆 난간 ──
body = ic.box("body", 0.100, 0.130, 0.016, 0, 0.010, 0.062, m.wood)
ic.bake_scale(body)
for sx in (-1, 1):
    rail = ic.box(f"rail_{'l' if sx < 0 else 'r'}", 0.012, 0.165, 0.012,
                  sx * 0.062, -0.005, 0.104, m.wood)
    ic.parent_to(rail, body)

# ── 바퀴 2 + 차축. 회전을 메시에 구워 축을 X로 만든다 — 런타임에 rotation.x로 굴린다 ──
WHEEL_R = 0.055
for sx, tag in ((-1, "l"), (1, "r")):
    wheel = ic.cylinder(f"wheel_{tag}", WHEEL_R, 0.014, sx * 0.066, 0.030, WHEEL_R,
                        m.wood, verts=10, rot_y=math.radians(90))
    bake_rotation(wheel)
    ic.parent_to(wheel, body)
axle = ic.cylinder("axle", 0.010, 0.150, 0, 0.030, WHEEL_R, m.wood,
                   verts=6, rot_y=math.radians(90))
bake_rotation(axle)
ic.parent_to(axle, body)

# ── 받침대 2 + 연필 말뚝(앞으로 14도 들림) ──
for dy, h, tag in ((-0.038, 0.070, "f"), (0.052, 0.046, "b")):
    post = ic.box(f"support_{tag}", 0.062, 0.016, h, 0, dy, 0.070 + h / 2, m.wood)
    ic.parent_to(post, body)

RAM_PITCH = math.radians(16)
ram = ic.cylinder("ram", 0.034, 0.160, 0, 0.005, 0.128, M_PENCIL,
                  verts=6, rot_x=math.radians(90) - RAM_PITCH)
bake_rotation(ram)
tip = ic.cone("ram_tip", 0.034, 0.002, 0.055, 0, 0.005 - 0.103, 0.128 + 0.103 * math.tan(RAM_PITCH),
              m.steel, verts=6, rot_x=-math.radians(90) - RAM_PITCH)
bake_rotation(tip)
ic.parent_to(tip, ram)
ic.parent_to(ram, body)

# ── 끄는 병사 2 — 수레 양옆에서 난간을 쥔다. 팔은 난간 높이로 뻗어 고정 ──
for i, sx in enumerate((-1, 1)):
    cx = sx * 0.085
    cy = -0.040
    torso = ic.cone(f"crew{i}_torso", 0.030, 0.038, 0.058, cx, cy, 0.133, m.armor, smooth=True)
    head = ic.box(f"crew{i}_head", 0.040, 0.038, 0.036, cx, cy, 0.184, m.skin)
    helmet = ic.cone(f"crew{i}_helmet", 0.027, 0.010, 0.022, cx, cy, 0.210, m.armor, verts=6)
    for tag, lx in (("l", -0.020), ("r", 0.020)):
        leg = ic.box(f"crew{i}_leg_{tag}", 0.024, 0.026, ic.HIP_Z, cx + lx, cy, ic.HIP_Z,
                     m.cloth, origin_shift=(0, 0, -ic.HIP_Z / 2))
        foot = ic.box(f"crew{i}_foot_{tag}", 0.026, 0.044, 0.014, cx + lx, cy - 0.008, 0.007, m.armor)
        ic.parent_to(foot, leg)
        ic.parent_to(leg, torso)
    # 안쪽 팔이 난간을 쥔다(고정 자세)
    arm = ic.box(f"crew{i}_arm", 0.020, 0.022, 0.055, cx - sx * 0.022, cy + 0.018, 0.135,
                 m.armor, rot_x=math.radians(-55))
    for part in (head, helmet, arm):
        ic.parent_to(part, torso)
    ic.parent_to(torso, body)

ic.export("troop-thunder-cart.glb")
