# 큰산(3타일 삼각, 동양풍 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_mountain_large.py
#
# 발자국: 육각 3개(12시 앵커·4시·8시 — 삼각/원형). 모델 원점 = 세 타일의 중심점.
# 패턴은 중간산(일자 쌍봉+능선)과 다르게: 중앙 거봉을 세 방향 곁봉이 둘러싼 산괴.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\mountain-large.glb"

HEX_R = 0.5774
TILE_H = 0.2
APO = 0.5  # 이웃 간격 1.0의 절반
# 세 타일 중심(모델 원점 = 공유 꼭짓점): 12시, 4시, 8시
CENTERS = ((0.0, HEX_R), (APO, -HEX_R / 2), (-APO, -HEX_R / 2))

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.9, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


M_GRASS = make_mat("grass", (0.14, 0.62, 0.35))
M_SIDE = make_mat("side", (0.62, 0.45, 0.30))
M_SLOPE = make_mat("slope", (0.10, 0.50, 0.24))
M_ROCK = make_mat("rock", (0.42, 0.40, 0.36))
M_CLIFF = make_mat("cliff", (0.38, 0.35, 0.31))  # 거봉 절벽 밴드(중간산과 차별)
M_SNOW = make_mat("snow", (0.96, 0.97, 0.99), roughness=0.6)  # 만년설(의도적 설산)


def cone(name, r1, r2, h, x, y, z, mat, verts=7, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체 3개(육각, 타일과 같은 방향) ──
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

# ── 중앙 거봉(공유 꼭짓점 위): 수풀 → 절벽 밴드 → 바위 → 만년설 정상의 4단 구성 ──
cone("giant_slope", 0.52, 0.30, 0.34, 0.0, 0.0, Z + 0.17, M_SLOPE, verts=8, rot_z=0.4)
cone("giant_cliff", 0.31, 0.20, 0.26, 0.0, 0.0, Z + 0.34 + 0.13, M_CLIFF, verts=8, rot_z=0.15)
cone("giant_rock", 0.21, 0.13, 0.20, 0.0, 0.0, Z + 0.60 + 0.10, M_ROCK, verts=8, rot_z=0.7)
cone("giant_snow", 0.14, 0.012, 0.22, 0.0, 0.0, Z + 0.80 + 0.11, M_SNOW, verts=8, rot_z=0.3)

# ── 세 방향 곁봉(각 타일 중심, 높이를 서로 다르게, 정상엔 작은 눈) ──
FLANKS = ((CENTERS[0], 0.30, 0.22, 1.1), (CENTERS[1], 0.26, 0.18, 0.3), (CENTERS[2], 0.22, 0.14, 1.9))
for i, ((fx, fy), sh, rh, rot) in enumerate(FLANKS):
    cone(f"flank{i}_slope", 0.30, 0.13, sh, fx, fy, Z + sh / 2, M_SLOPE, verts=6, rot_z=rot)
    cone(f"flank{i}_rock", 0.12, 0.05, rh * 0.6, fx, fy, Z + sh + rh * 0.3, M_ROCK, verts=6, rot_z=rot + 0.5)
    cone(f"flank{i}_snow", 0.055, 0.008, rh * 0.5, fx, fy, Z + sh + rh * 0.6 + rh * 0.25, M_SNOW, verts=6, rot_z=rot)

# ── 기슭 나무 ──
for i, (tx, ty) in enumerate(((0.0, 1.02), (0.46, 0.55), (-0.50, 0.50),
                              (0.88, -0.42), (-0.88, -0.42), (0.0, -0.78))):
    cone(f"tree{i}", 0.045, 0.004, 0.09, tx, ty, Z + 0.045, M_SLOPE, verts=6)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
