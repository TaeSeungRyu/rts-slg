# 병종 15 — 철기병(Cataphract, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_cataphract.py
#
# doc/design-unit.md: [육지, 속도 3, 탐지 3, 사거리 1/1/1]
# 모습(사용자 확정, 2026-08-07): 기사처럼 전신 철갑 + 철갑 군마. 큰 창(랜스).
# 이동·공격은 기존 Cavalry 그대로 — 부위 이름을 기병 규약(leg_fl/rider_arm_r)으로
# 맞춰 갤럽·돌격이 자동 재사용된다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_PLATE = ic.make_mat("plate", (0.52, 0.54, 0.58), metallic=0.55, roughness=0.45)
M_PLATE2 = ic.make_mat("plate2", (0.40, 0.42, 0.46), metallic=0.55, roughness=0.5)
M_HIDE = ic.make_mat("hide", (0.22, 0.18, 0.16))

# ── 말 몸통(철갑 판이 덮이므로 어두운 가죽 톤). 자식이 회전하므로 스케일 굽기 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=1.0,
                                     location=(0, 0.01, 0.178))
body = bpy.context.object
body.name = "body"
body.scale = (0.065, 0.128, 0.055)
body.data.materials.append(M_HIDE)
ic.shade_smooth(body)
ic.bake_scale(body)

# 마갑(바딩): 몸통 옆·위를 덮는 철판
for sx in (-1, 1):
    plate = ic.box(f"barding_{'l' if sx < 0 else 'r'}", 0.014, 0.200, 0.085,
                   sx * 0.062, 0.012, 0.190, M_PLATE2)
    ic.parent_to(plate, body)
top_plate = ic.box("barding_top", 0.120, 0.170, 0.012, 0, 0.030, 0.238, M_PLATE2)
ic.parent_to(top_plate, body)

# ── 다리 4(원점=고관절) — 기병과 동일 규약 ──
HIP = 0.166
for tag, lx, ly in (("fl", -0.030, -0.055), ("fr", 0.030, -0.055),
                    ("bl", -0.030, 0.070), ("br", 0.030, 0.070)):
    leg = ic.box(f"leg_{tag}", 0.026, 0.028, HIP, lx, ly, HIP, M_HIDE,
                 origin_shift=(0, 0, -HIP / 2))
    hoof = ic.box(f"hoof_{tag}", 0.030, 0.032, 0.016, lx, ly, 0.008, M_PLATE2)
    ic.parent_to(hoof, leg)
    ic.parent_to(leg, body)

# ── 목·머리 + 마면갑(챈프런) ──
neck = ic.cone("neck", 0.040, 0.028, 0.115, 0, -0.098, 0.216, M_HIDE,
               rot_x=math.radians(38), smooth=True)
head = ic.box("head", 0.042, 0.088, 0.042, 0, -0.163, 0.263, M_HIDE,
              rot_x=math.radians(20))
chanfron = ic.box("chanfron", 0.046, 0.070, 0.014, 0, -0.172, 0.288, M_PLATE,
                  rot_x=math.radians(20))
ic.parent_to(chanfron, head)
neck_plate = ic.box("neck_plate", 0.050, 0.020, 0.105, 0, -0.108, 0.222, M_PLATE2,
                    rot_x=math.radians(38))
for part in (neck, head, neck_plate):
    ic.parent_to(part, body)

# ── 안장천(세력색) + 안장 ──
cloth = ic.box("saddle_cloth", 0.096, 0.100, 0.010, 0, 0.012, 0.246, m.red)
saddle = ic.box("saddle", 0.062, 0.076, 0.014, 0, 0.012, 0.256, M_HIDE)
for part in (cloth, saddle):
    ic.parent_to(part, body)

# ── 기수: 전신 철갑 + 밀폐 투구 + 세력색 술 ──
rider = ic.cone("rider", 0.036, 0.044, 0.070, 0, 0.012, 0.269, M_PLATE, smooth=True)
rhead = ic.box("rider_head", 0.046, 0.044, 0.044, 0, 0.012, 0.325, M_PLATE)
visor = ic.box("visor", 0.048, 0.010, 0.010, 0, -0.010, 0.330, M_PLATE2)
helm_top = ic.cone("helm_top", 0.030, 0.010, 0.024, 0, 0.012, 0.355, M_PLATE, verts=6)
plume = ic.box("plume", 0.010, 0.010, 0.028, 0, 0.012, 0.380, m.red)
for part in (rhead, visor, helm_top, plume):
    ic.parent_to(part, rider)

# 기수 다리(판금) + 등자
LEG_X = 0.070
for tag, sx in (("l", -LEG_X), ("r", LEG_X)):
    thigh = ic.box(f"rider_thigh_{tag}", 0.028, 0.030, 0.060, sx, -0.010, 0.213, M_PLATE2,
                   rot_x=math.radians(-30))
    shin = ic.box(f"rider_shin_{tag}", 0.026, 0.028, 0.058, sx, -0.014, 0.157, M_PLATE2,
                  rot_x=math.radians(10))
    boot = ic.box(f"rider_boot_{tag}", 0.028, 0.044, 0.018, sx, -0.024, 0.128, M_PLATE2)
    stirrup = ic.box(f"stirrup_{tag}", 0.022, 0.028, 0.008, sx, -0.020, 0.116, m.steel)
    for part in (thigh, shin, boot, stirrup):
        ic.parent_to(part, rider)

# ── 기수 팔(판금, 피벗=어깨) ──
arms = {}
for tag, ax, pitch in (("l", -0.044, math.radians(-26)), ("r", 0.044, math.radians(12))):
    arm = ic.box(f"rider_arm_{tag}", 0.024, 0.026, 0.058, ax, 0.012, 0.291, M_PLATE,
                 rot_x=pitch, origin_shift=(0, 0, -0.029))
    ic.parent_to(arm, rider)
    arms[tag] = arm

ic.parent_to(rider, body)

# ── 랜스(큰 창): 세워 든다. 창날 + 세력색 기수기(페논) ──
HX, HY = 0.050, 0.000
HAND = 0.233
LANCE = 0.300
shaft = ic.cylinder("lance", 0.007, LANCE, HX, HY, HAND - 0.040 + LANCE / 2, m.wood,
                    verts=6, rot_x=math.radians(-6))
tip = ic.cone("lance_tip", 0.012, 0.001, 0.042, HX, HY - 0.028, HAND - 0.040 + LANCE + 0.018,
              m.steel, verts=4, rot_x=math.radians(-6))
pennon = ic.box("lance_pennon", 0.004, 0.036, 0.020, HX, HY - 0.038, HAND - 0.040 + LANCE - 0.030,
                m.red, rot_x=math.radians(-6))
for part in (tip, pennon):
    ic.parent_to(part, shaft)
ic.parent_to(shaft, arms["r"])

ic.export("troop-cataphract.glb")
