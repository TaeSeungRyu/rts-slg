# 소형산(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_mountain.py
#
# 킷 stone-mountain은 돌 뾰족바위 모양이라 형태 개선 불가 → 커스텀 신규 제작(사용자 결정).
# 타일 일체형(반경 0.5774, 높이 0.2): 초록 산자락 + 바위 정상, 봉우리 2~3개.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\mountain-small.glb"

HEX_R = 0.5774
TILE_H = 0.2

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.9, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


M_GRASS = make_mat("grass", (0.14, 0.62, 0.35))   # 타일 윗면(주변 초원과 어울리게)
M_SIDE = make_mat("side", (0.62, 0.45, 0.30))     # 타일 옆면(킷 타일 흙색)
M_SLOPE = make_mat("slope", (0.10, 0.50, 0.24))   # 산자락(수풀 — 저채도 톤을 견디게 채도 강화)
M_ROCK = make_mat("rock", (0.50, 0.48, 0.45))     # 바위 정상


def cone(name, r1, r2, h, x, y, z, mat, verts=7, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체(육각, 타일과 같은 방향): 윗면 초록 + 옆면 흙색 ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2),
    rotation=(0, 0, math.radians(0)))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_GRASS)
# 윗면 폴리곤만 초록 재질로
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H

# ── 주봉: 수풀 산자락(하단 프러스텀) + 바위 정상(상단 콘) ──
cone("main_slope", 0.40, 0.17, 0.30, -0.03, 0.02, Z + 0.15, M_SLOPE, verts=7, rot_z=0.2)
cone("main_rock", 0.18, 0.015, 0.24, -0.03, 0.02, Z + 0.30 + 0.12, M_ROCK, verts=7, rot_z=0.5)

# ── side봉 2개(주봉보다 낮게) ──
cone("side1_slope", 0.24, 0.10, 0.20, 0.24, -0.16, Z + 0.10, M_SLOPE, verts=6, rot_z=0.9)
cone("side1_rock", 0.11, 0.012, 0.14, 0.24, -0.16, Z + 0.20 + 0.07, M_ROCK, verts=6, rot_z=0.3)
cone("side2_slope", 0.20, 0.08, 0.16, -0.20, -0.22, Z + 0.08, M_SLOPE, verts=6, rot_z=1.6)
cone("side2_rock", 0.09, 0.012, 0.11, -0.20, -0.22, Z + 0.16 + 0.055, M_ROCK, verts=6, rot_z=0.1)

# ── 기슭 나무 몇 그루(스케일감) ──
for i, (tx, ty) in enumerate(((0.34, 0.22), (0.10, 0.38), (-0.34, 0.16))):
    cone(f"tree{i}", 0.045, 0.004, 0.09, tx, ty, Z + 0.045, M_SLOPE, verts=6)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
