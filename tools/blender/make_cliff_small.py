# 작은 절벽(1타일, 폭포 절벽산과 같은 양식·폭포 없음) 생성 → GLB 익스포트
# 실행: blender --background --python make_cliff_small.py
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\cliff-small.glb"

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


M_GRASS = make_mat("grass", (0.14, 0.62, 0.35))
M_SIDE = make_mat("side", (0.62, 0.45, 0.30))
M_SLOPE = make_mat("slope", (0.10, 0.50, 0.24))
M_CLIFF = make_mat("cliff", (0.44, 0.42, 0.38))
M_CLIFF2 = make_mat("cliff2", (0.38, 0.36, 0.32))


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, 0, rot_z))
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


# ── 타일 본체 ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2),
    rotation=(0, 0, math.radians(0)))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_GRASS)
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H
CLIFF_H = 0.46

# ── 어긋난 바위 단애(북쪽 절반) + 들쭉날쭉 정상 수풀 ──
box("cliff_c", 0.40, 0.34, CLIFF_H, 0.02, 0.14, Z + CLIFF_H / 2, M_CLIFF, rot_z=0.05)
box("cliff_w", 0.30, 0.28, CLIFF_H * 0.8, -0.26, 0.18, Z + CLIFF_H * 0.4, M_CLIFF2, rot_z=-0.16)
box("cliff_e", 0.26, 0.26, CLIFF_H * 0.88, 0.30, 0.16, Z + CLIFF_H * 0.44, M_CLIFF2, rot_z=0.14)
box("top_c", 0.38, 0.32, 0.05, 0.02, 0.14, Z + CLIFF_H + 0.025, M_SLOPE, rot_z=0.05)
box("top_w", 0.28, 0.26, 0.05, -0.26, 0.18, Z + CLIFF_H * 0.8 + 0.025, M_SLOPE, rot_z=-0.16)
box("top_e", 0.24, 0.24, 0.05, 0.30, 0.16, Z + CLIFF_H * 0.88 + 0.025, M_SLOPE, rot_z=0.14)
# 절벽면 바위 이빨 + 앞쪽 낙석
cone("tooth", 0.07, 0.02, 0.20, 0.05, -0.06, Z + 0.10, M_CLIFF2, verts=5, rot_z=0.8)
cone("scree", 0.06, 0.015, 0.10, -0.18, -0.16, Z + 0.05, M_CLIFF, verts=5, rot_z=1.5)

# ── 기슭 나무 ──
for i, (tx, ty) in enumerate(((0.32, -0.26), (-0.32, -0.28), (0.0, 0.44))):
    tz = CLIFF_H + 0.05 if ty > 0 else 0.0
    cone(f"tree{i}", 0.045, 0.004, 0.09, tx, ty, Z + tz + 0.045, M_SLOPE, verts=6)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
