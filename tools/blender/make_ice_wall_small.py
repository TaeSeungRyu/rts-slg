# 작은 얼음벽 모음(1타일, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_ice_wall_small.py
#
# 컨셉: 거대한 얼음벽의 잔해처럼 낮은 얼음 판 조각들이 끊어진 두 줄로 흩어진 타일.
# 판들은 낮고(0.12~0.26) 일부는 기울어져 있으며 위에 눈이 살짝 얹힌다.
# 한랭 지형군. 이동 불가 예정.
import bpy
import math

OUT = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models\ice-wall-small.glb"

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


M_SNOW = make_mat("snow", (0.90, 0.93, 0.96), roughness=0.7)
M_SIDE = make_mat("side", (0.55, 0.55, 0.60), roughness=0.8)
M_ICE = make_mat("ice", (0.30, 0.62, 0.92), roughness=0.12)
M_ICE2 = make_mat("ice2", (0.20, 0.48, 0.80), roughness=0.35)
M_ICE3 = make_mat("ice3", (0.55, 0.78, 0.95), roughness=0.25)


def slab(name, sx, sy, sz, x, y, z, mat, rot_z=0.0, rot_y=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(x, y, z),
                                    rotation=(0, rot_y, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def cone(name, r1, r2, h, x, y, z, mat, verts=6, rot_z=0.0):
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

# ── 앞줄: 비스듬히 이어지는 낮은 판 4장(끊어진 벽) ──
front = [
    (-0.34, 0.10, 0.16, M_ICE2, +0.30, 0.00),
    (-0.14, 0.16, 0.24, M_ICE,  +0.34, -0.10),
    (0.08,  0.13, 0.26, M_ICE3, +0.28, +0.12),
    (0.30,  0.06, 0.15, M_ICE,  +0.42, 0.00),
]
for i, (x, y, h, mat, rz, ry) in enumerate(front):
    slab(f"front_{i}", 0.16, 0.09, h, x, y - 0.16, Z + h / 2, mat, rot_z=rz, rot_y=ry)
    slab(f"front_cap_{i}", 0.165, 0.10, 0.025, x, y - 0.16, Z + h + 0.012, M_SNOW,
         rot_z=rz, rot_y=ry)

# ── 뒷줄: 반대로 기운 판 3장 ──
back = [
    (-0.20, -0.06, 0.20, M_ICE3, -0.55, +0.08),
    (0.02,  -0.10, 0.14, M_ICE2, -0.48, 0.00),
    (0.24,  -0.04, 0.18, M_ICE,  -0.62, -0.09),
]
for i, (x, y, h, mat, rz, ry) in enumerate(back):
    slab(f"back_{i}", 0.14, 0.08, h, x, y - 0.12, Z + h / 2, mat, rot_z=rz, rot_y=ry)
    slab(f"back_cap_{i}", 0.145, 0.09, 0.02, x, y - 0.12, Z + h + 0.01, M_SNOW,
         rot_z=rz, rot_y=ry)

# ── 잔해 조각·작은 결정·눈 둔덕 ──
slab("chunk_1", 0.07, 0.06, 0.06, -0.36, -0.24, Z + 0.03, M_ICE, rot_z=1.1)
slab("chunk_2", 0.06, 0.05, 0.05, 0.38, 0.10, Z + 0.025, M_ICE2, rot_z=0.4)
cone("shard_1", 0.030, 0.007, 0.10, 0.16, 0.34, Z + 0.05, M_ICE, verts=5, rot_z=0.8)
cone("mound_1", 0.12, 0.04, 0.05, -0.06, 0.36, Z + 0.025, M_SNOW, verts=7)
cone("mound_2", 0.10, 0.035, 0.045, 0.02, -0.34, Z + 0.022, M_SNOW, verts=7)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
