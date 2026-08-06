# 기암 소석림(1타일, 매우 큰산과 같은 양식) 생성 → GLB 익스포트
# 실행: blender --background --python make_karst_small.py
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\karst-small.glb"

HEX_R = 0.5774
TILE_H = 0.2

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
M_PILLAR = make_mat("pillar", (0.52, 0.46, 0.38))
M_PILLAR2 = make_mat("pillar2", (0.42, 0.38, 0.32))
M_GREEN = make_mat("green", (0.10, 0.50, 0.24))


def cyl(name, r1, r2, h, x, y, z, mat, verts=6, rot_z=0.0):
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


def pillar(name, x, y, r, h, dark=False):
    mat_lo = M_PILLAR2 if dark else M_PILLAR
    mat_hi = M_PILLAR if dark else M_PILLAR2
    h1, h2 = h * 0.62, h * 0.38
    cyl(f"{name}_lo", r, r * 0.88, h1, x, y, Z + h1 / 2, mat_lo, rot_z=x * 3 + y)
    cyl(f"{name}_hi", r * 0.88, r * 0.72, h2, x, y, Z + h1 + h2 / 2, mat_hi, rot_z=y * 2 + x)
    cyl(f"{name}_cap", r * 1.05, r * 0.35, 0.06, x, y, Z + h + 0.03, M_GREEN, rot_z=x + y)


# ── 기둥 3개(높이 제각각) ──
pillar("p1", -0.05, 0.08, 0.125, 0.78)
pillar("p2", 0.22, -0.16, 0.095, 0.52, dark=True)
pillar("p3", -0.26, -0.20, 0.080, 0.38)

# ── 기슭 초목 ──
for i, (tx, ty) in enumerate(((0.30, 0.24), (-0.34, 0.20), (0.05, -0.40))):
    cyl(f"tree{i}", 0.045, 0.004, 0.09, tx, ty, Z + 0.045, M_GREEN)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
