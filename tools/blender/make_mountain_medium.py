# 중간산(2타일, 동양풍 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_mountain_medium.py
#
# 발자국: 육각 2개(앵커+동쪽 이웃). 모델 원점 = 두 타일의 중심점.
# 소형산보다 웅장한 산괴: 주봉(높음) + 부봉 + 연결 능선. 이동 불가 예정.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\mountain-medium.glb"

HEX_R = 0.5774
TILE_H = 0.2
# 두 타일 중심(모델 원점 기준): 서쪽(-0.5, 0), 동쪽(+0.5, 0) — 이웃 간격 1.0
CENTERS = ((-0.5, 0.0), (0.5, 0.0))

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.9, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


M_GRASS = make_mat("grass", (0.14, 0.62, 0.35))
M_SIDE = make_mat("side", (0.62, 0.45, 0.30))
M_SLOPE = make_mat("slope", (0.10, 0.50, 0.24))
M_ROCK = make_mat("rock", (0.50, 0.48, 0.45))


def cone(name, r1, r2, h, x, y, z, mat, verts=7, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체 2개(육각, 타일과 같은 방향): 윗면 초록 + 옆면 흙색 ──
for i, (cx, cy) in enumerate(CENTERS):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=HEX_R, depth=TILE_H, location=(cx, cy, TILE_H / 2),
        rotation=(0, 0, math.radians(0)))
    base = bpy.context.object
    base.name = f"base{i}"
    base.data.materials.append(M_SIDE)
    base.data.materials.append(M_GRASS)
    for poly in base.data.polygons:
        if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
            poly.material_index = 1

Z = TILE_H

# ── 주봉(서쪽 타일): 소형산보다 높게 ──
cone("main_slope", 0.46, 0.20, 0.42, -0.52, 0.03, Z + 0.21, M_SLOPE, verts=7, rot_z=0.3)
cone("main_rock", 0.21, 0.015, 0.34, -0.52, 0.03, Z + 0.42 + 0.17, M_ROCK, verts=7, rot_z=0.8)

# ── 부봉(동쪽 타일) ──
cone("sub_slope", 0.38, 0.16, 0.32, 0.55, -0.04, Z + 0.16, M_SLOPE, verts=7, rot_z=1.2)
cone("sub_rock", 0.17, 0.013, 0.24, 0.55, -0.04, Z + 0.32 + 0.12, M_ROCK, verts=7, rot_z=0.2)

# ── 연결 능선(두 봉 사이 낮은 산등성이) ──
cone("ridge", 0.30, 0.12, 0.22, 0.02, 0.02, Z + 0.11, M_SLOPE, verts=6, rot_z=0.6)
cone("ridge_rock", 0.10, 0.012, 0.10, 0.02, 0.02, Z + 0.22 + 0.05, M_ROCK, verts=6)

# ── 기슭 나무 ──
for i, (tx, ty) in enumerate(((-0.82, 0.28), (-0.20, -0.38), (0.30, 0.36), (0.86, 0.20))):
    cone(f"tree{i}", 0.045, 0.004, 0.09, tx, ty, Z + 0.045, M_SLOPE, verts=6)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
