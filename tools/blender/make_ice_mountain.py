# 얼음산(1타일, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_ice_mountain.py
#
# 컨셉: 산의 실루엣(넓은 기슭+경사면)을 가진 빙설 산체 — 눈 사면 + 광택 얼음 정상.
# (1차 첨탑 버전은 "얼음 가시"처럼 보인다는 피드백으로 산 형태로 재작업, 2026-08-04)
# 큰산(초록 산+눈 정상)과 구분되는 한랭 지형. 이동 불가 예정.
import bpy
import math

OUT = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models\ice-mountain.glb"

HEX_R = 0.5774
TILE_H = 0.2

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


M_SNOW = make_mat("snow", (0.90, 0.93, 0.96), roughness=0.7)      # 눈 바닥·둔덕
M_SIDE = make_mat("side", (0.55, 0.55, 0.60), roughness=0.8)      # 언 땅 옆면
M_BODY = make_mat("body", (0.80, 0.87, 0.93), roughness=0.55)     # 빙설 산체(눈 사면)
M_ICE = make_mat("ice", (0.55, 0.76, 0.90), roughness=0.12)       # 얼음 정상(광택)
M_ICE2 = make_mat("ice2", (0.42, 0.64, 0.82), roughness=0.35)     # 얼음 절벽 띠


def cone(name, r1, r2, h, x, y, z, mat, verts=7, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체(윗면 눈 + 옆면 언 땅) ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2),
    rotation=(0, 0, math.radians(0)))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_SNOW)
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H

# ── 주봉: 눈 사면(넓은 기슭) → 얼음 절벽 띠 → 광택 얼음 정상 ──
cone("main_slope", 0.40, 0.20, 0.30, -0.03, 0.03, Z + 0.15, M_BODY, verts=7, rot_z=0.3)
cone("main_band", 0.21, 0.13, 0.14, -0.03, 0.03, Z + 0.30 + 0.07, M_ICE2, verts=7, rot_z=0.8)
cone("main_peak", 0.14, 0.010, 0.22, -0.03, 0.03, Z + 0.44 + 0.11, M_ICE, verts=7, rot_z=0.5)

# ── 곁봉 2개(주봉보다 낮게) ──
cone("side1_slope", 0.24, 0.11, 0.22, 0.24, -0.17, Z + 0.11, M_BODY, verts=6, rot_z=1.1)
cone("side1_peak", 0.10, 0.009, 0.15, 0.24, -0.17, Z + 0.22 + 0.075, M_ICE, verts=6, rot_z=0.4)
cone("side2_slope", 0.19, 0.09, 0.17, -0.24, -0.20, Z + 0.085, M_BODY, verts=6, rot_z=2.0)
cone("side2_peak", 0.08, 0.008, 0.12, -0.24, -0.20, Z + 0.17 + 0.06, M_ICE, verts=6, rot_z=1.3)

# ── 기슭 눈 둔덕 + 작은 얼음 조각 포인트 ──
cone("mound_1", 0.15, 0.05, 0.07, -0.15, 0.30, Z + 0.035, M_SNOW, verts=7)
cone("mound_2", 0.12, 0.04, 0.06, 0.30, 0.22, Z + 0.03, M_SNOW, verts=7)
cone("shard", 0.035, 0.008, 0.12, -0.36, 0.10, Z + 0.06, M_ICE, verts=5, rot_z=0.9)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
