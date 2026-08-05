# 폭포 절벽산(3타일, 동양풍 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_waterfall_cliff.py
#
# 발자국: 절벽 2타일(앵커·동쪽, 북쪽) + 소(웅덩이) 1타일(남쪽). 모델 원점 = 중심점.
# 북쪽 두 타일에 깎아지른 절벽 산괴, 절벽 남면을 타고 폭포 → 남쪽 타일의 소로 떨어진다.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\waterfall-cliff.glb"

HEX_R = 0.5774
TILE_H = 0.2
# 타일 중심(모델 원점 기준, Blender +Y=북): 절벽 2(북), 소 1(남)
CENTERS = ((-0.5, 0.289), (0.5, 0.289), (0.0, -0.577))

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
M_CLIFF = make_mat("cliff", (0.44, 0.42, 0.38))
M_WATER = make_mat("water", (0.30, 0.62, 0.72), roughness=0.3)
M_FOAM = make_mat("foam", (0.94, 0.96, 0.98), roughness=0.5)


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def cone(name, r1, r2, h, x, y, z, mat, verts=7, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def cyl(name, r, h, x, y, z, mat, verts=12):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=h, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체 3개 ──
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
CLIFF_H = 0.78

# ── 절벽 산괴(북쪽 두 타일 위): 어긋나게 쪼갠 바위 블록들 — 자연스러운 단애 ──
M_CLIFF2 = make_mat("cliff2", (0.38, 0.36, 0.32))
box("cliff_c", 0.62, 0.55, CLIFF_H, 0.0, 0.40, Z + CLIFF_H / 2, M_CLIFF, rot_z=0.03)
box("cliff_w", 0.55, 0.50, CLIFF_H * 0.86, -0.52, 0.44, Z + CLIFF_H * 0.43, M_CLIFF2, rot_z=-0.14)
box("cliff_e", 0.50, 0.48, CLIFF_H * 0.92, 0.50, 0.42, Z + CLIFF_H * 0.46, M_CLIFF2, rot_z=0.12)
box("cliff_back", 1.10, 0.38, CLIFF_H * 0.66, 0.05, 0.74, Z + CLIFF_H * 0.33, M_CLIFF, rot_z=-0.05)
# 정상 수풀(블록별로 얹어 들쭉날쭉하게)
box("top_c", 0.60, 0.52, 0.06, 0.0, 0.40, Z + CLIFF_H + 0.03, M_SLOPE, rot_z=0.03)
box("top_w", 0.52, 0.46, 0.06, -0.52, 0.44, Z + CLIFF_H * 0.86 + 0.03, M_SLOPE, rot_z=-0.14)
box("top_e", 0.47, 0.44, 0.06, 0.50, 0.42, Z + CLIFF_H * 0.92 + 0.03, M_SLOPE, rot_z=0.12)
# 절벽면 바위 이빨(불규칙 요철)
cone("tooth1", 0.09, 0.02, 0.30, -0.24, 0.13, Z + 0.15, M_CLIFF2, verts=5, rot_z=0.7)
cone("tooth2", 0.08, 0.02, 0.22, 0.27, 0.14, Z + 0.11, M_CLIFF, verts=5, rot_z=1.4)
# 양옆 수풀 사면
cone("shoulder_w", 0.32, 0.10, 0.46, -0.86, 0.32, Z + 0.23, M_SLOPE, verts=6, rot_z=0.8)
cone("shoulder_e", 0.30, 0.09, 0.40, 0.86, 0.34, Z + 0.20, M_SLOPE, verts=6, rot_z=1.7)
# 정상 나무
for i, (tx, ty, tz) in enumerate(((-0.5, 0.42, CLIFF_H * 0.86), (-0.1, 0.5, CLIFF_H),
                                  (0.35, 0.4, CLIFF_H * 0.92), (0.1, 0.28, CLIFF_H))):
    cone(f"top_tree{i}", 0.05, 0.005, 0.10, tx, ty, Z + tz + 0.06 + 0.05, M_SLOPE, verts=6)

# ── 폭포: 절벽 남면(y=0.11)을 타고 흐르는 물줄기 + 흰 포말 줄 ──
FALL_Y = 0.105
box("fall", 0.17, 0.025, CLIFF_H, 0.0, FALL_Y, Z + CLIFF_H / 2, M_WATER)
box("fall_foam1", 0.045, 0.028, CLIFF_H * 0.96, -0.05, FALL_Y, Z + CLIFF_H / 2, M_FOAM)
box("fall_foam2", 0.035, 0.028, CLIFF_H * 0.9, 0.05, FALL_Y, Z + CLIFF_H / 2, M_FOAM)
# 낙수 립(절벽 위 물머리)
box("fall_lip", 0.17, 0.10, 0.025, 0.0, 0.16, Z + CLIFF_H + 0.0125, M_WATER)

# ── 소(웅덩이, 남쪽 타일): 물 + 포말 링 + 흘러나가는 개울 ──
cyl("pool", 0.34, 0.015, 0.0, -0.42, Z + 0.008, M_WATER)
cyl("pool_foam", 0.13, 0.018, 0.0, -0.12, Z + 0.010, M_FOAM)
box("stream", 0.10, 0.38, 0.012, 0.0, -0.86, Z + 0.006, M_WATER)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
