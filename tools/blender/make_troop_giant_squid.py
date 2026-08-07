# 이벤트 유닛 27 — 대왕오징어(GiantSquid, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_giant_squid.py
#
# doc/spec-unit.md: [대하, 속도 0, 탐지 2, 사거리 1/1/1] 타일 3개(삼각 클러스터), 항상 1마리.
# 모습(사용자 확정, 2026-08-07): 머리·몸통은 물에 절반만 나와 있고(z<0은 수면 아래로
# 잠겨 타일에 가려진다), 차지한 타일마다 촉수가 물을 뚫고 솟는다.
# 몸통은 spine_0(물결 규약 — 상하로 오르내림), 촉수는 tentacle_0..7(원점=물밑 뿌리,
# 런타임이 rotation:x로 흔들고 공격 때 내리친다). 클러스터 타일 중심은 중간성 발자국과
# 같다: Godot (0,-0.5774)(±0.5,0.2887) → Blender +Y=북이므로 y 부호 반전.
# 중립(이벤트) 유닛이라 세력색 red 재질을 쓰지 않는다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic
from mathutils import Matrix

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_SQUID = ic.make_mat("squid", (0.70, 0.28, 0.26))
M_SQUID2 = ic.make_mat("squid2", (0.56, 0.20, 0.20))
M_BELLY = ic.make_mat("squid_belly", (0.88, 0.76, 0.66))
M_EYE_W = ic.make_mat("eye_white", (0.92, 0.90, 0.84), roughness=0.4)
M_EYE_B = ic.make_mat("eye_black", (0.06, 0.05, 0.05), roughness=0.3)

# ── 뿌리(부모): 수면 아래 앵커 — 물결은 자식 마디(spine_0)가 탄다 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.02,
                                     location=(0, 0, -0.06))
body = bpy.context.object
body.name = "body"
body.data.materials.append(M_SQUID2)

# ── 몸통(spine_0): 위로 뾰족한 외투막 원뿔 + 지느러미 2 + 머리 구 + 큰 눈 2.
# 절반만 수면 위 — 머리 구의 아래쪽과 외투막 하단은 z<0으로 잠긴다 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.018,
                                     location=(0, 0, 0.0))
mantle_root = bpy.context.object
mantle_root.name = "spine_0"
mantle_root.data.materials.append(M_SQUID)

mantle = ic.cone("mantle", 0.135, 0.020, 0.34, 0, 0.02, 0.175, M_SQUID, smooth=True)
for s in (-1, 1):
    fin = ic.box(f"fin_{'l' if s < 0 else 'r'}", 0.075, 0.055, 0.012,
                 s * 0.085, 0.03, 0.300, M_SQUID2, rot_y=math.radians(s * -30))
    ic.parent_to(fin, mantle)
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.125,
                                     location=(0, -0.02, 0.020))
head = bpy.context.object
head.name = "head"
head.data.materials.append(M_SQUID)
ic.shade_smooth(head)
for s in (-1, 1):
    white = ic.cylinder(f"eye_{'l' if s < 0 else 'r'}", 0.036, 0.014,
                        s * 0.112, -0.045, 0.078, M_EYE_W, verts=10,
                        rot_y=math.radians(90), smooth=True)
    pupil = ic.cylinder(f"pupil_{'l' if s < 0 else 'r'}", 0.017, 0.008,
                        s * 0.120, -0.048, 0.078, M_EYE_B, verts=8,
                        rot_y=math.radians(90))
    ic.parent_to(pupil, white)
    ic.parent_to(white, head)
for part in (mantle, head):
    ic.parent_to(part, mantle_root)
ic.parent_to(mantle_root, body)

# ── 촉수 8: 몸통 곁 2 + 클러스터 타일마다 2 — 물을 뚫고 솟아 안쪽으로 굽는다.
# 원점=물밑 뿌리. yaw는 클러스터 중심에서 바깥을 향하게 계산한다 ──
TILES = ((0.0, 0.5774), (0.5, -0.2887), (-0.5, -0.2887))
SPOTS = []
for tx, ty in TILES:
    SPOTS.append((tx + 0.16, ty + 0.05, 0.28, 1.00))
    SPOTS.append((tx - 0.13, ty - 0.12, 0.23, 0.85))
SPOTS.append((0.22, -0.02, 0.20, 0.75))
SPOTS.append((-0.20, 0.10, 0.18, 0.70))

for i, (px, py, h, s) in enumerate(SPOTS):
    d = math.hypot(px, py)
    yaw = math.atan2(px / d, -py / d)
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.030 * s, radius2=0.014 * s,
                                    depth=h, location=(px, py, -0.03))
    arm = bpy.context.object
    arm.name = f"tentacle_{i}"
    arm.data.transform(Matrix.Translation((0, 0, h / 2)))
    arm.data.materials.append(M_SQUID)
    ic.shade_smooth(arm)
    # 끝마디는 안쪽(+Y, 회전 전)으로 굽는 납작한 삼각 날 — 3면 뿔을 옆으로 눌렀다.
    # 회전 전에 parent해야 yaw를 같이 탄다
    tip = ic.cone(f"tentacle_{i}_tip", 0.036 * s, 0.002, 0.095 * s,
                  px, py + 0.030 * s, -0.03 + h + 0.020, M_SQUID2,
                  verts=3, rot_x=math.radians(-48))
    tip.scale = (0.30, 1.0, 1.0)
    ic.parent_to(tip, arm)
    arm.rotation_euler = (math.radians(14), 0, yaw)
    ic.parent_to(arm, body)

ic.export("troop-giant-squid.glb")
