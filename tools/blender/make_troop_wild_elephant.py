# 이벤트 유닛 23 — 코끼리(WildElephant, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_wild_elephant.py
#
# doc/spec-unit.md: [육지, 속도 2, 탐지 2, 사거리 1/1/1] 야생 코끼리 — 편대 없이 항상 1마리.
# 상병(make_troop_war_elephant.py)에서 등판·꼬마 병사를 걷어낸 맨몸이다.
# trunk 규약으로 측대보·코 들이받기가 자동 재사용된다(walker 없는 쪽 분기).
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
M_HIDE = ic.make_mat("hide", (0.46, 0.41, 0.37))
M_HIDE2 = ic.make_mat("hide2", (0.40, 0.35, 0.31))
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

# ── 다리 4: 원기둥(스무스) + 어깨 근육 + 마디 선 2 — 상병과 동일 ──
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
    bpy.ops.mesh.primitive_uv_sphere_add(segments=9, ring_count=6, radius=0.036,
                                         location=(lx, ly, 0.150))
    pad = bpy.context.object
    pad.name = f"hip_{tag}"
    pad.scale = (1.0, 1.1, 0.82)
    pad.data.materials.append(M_HIDE)
    ic.shade_smooth(pad)
    ic.bake_scale(pad)
    ic.parent_to(pad, body)

# ── 머리 + 귀 2 + 상아 2(야생이라 상병보다 길다) ──
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
    tusk = ic.cone(f"tusk_{'l' if sx < 0 else 'r'}", 0.010, 0.001, 0.078,
                   sx * 0.026, -0.204, 0.208, M_IVORY, verts=6, rot_x=math.radians(-58))
    ic.parent_to(tusk, head)

# ── 코: 원점=코 뿌리, 런타임 회전. 상병과 동일 ──
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

tail = ic.box("tail", 0.016, 0.018, 0.075, 0, 0.128, 0.190, M_HIDE2,
              rot_x=math.radians(18))
ic.parent_to(tail, body)

ic.export("troop-wild-elephant.glb")
