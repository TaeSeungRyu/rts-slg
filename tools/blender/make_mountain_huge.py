# 매우 큰산(5타일, 랜드마크 기암괴석) 생성 → GLB 익스포트
# 실행: blender --background --python make_mountain_huge.py
#
# 발자국: 중심(앵커)+동·서·남서·남동 5타일. 모델 원점 = 발자국 중심점.
# 컨셉: 기존 원뿔 산과 완전히 다른 장가계/계림풍 수직 돌기둥 숲 —
# 기둥마다 초목 캡, 두 번째 기둥 위에 작은 정자(명산의 누각). 이동 불가 예정.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\mountain-huge.glb"

HEX_R = 0.5774
TILE_H = 0.2
# 타일 중심(모델 원점 = 발자국 중심점 기준, Blender +Y=북)
# 앵커(0,0)+E+W+SW+SE의 월드 위치에서 중심점(0,-0.346)을 뺀 값
CENTERS = ((0.0, 0.346), (1.0, 0.346), (-1.0, 0.346), (-0.5, -0.520), (0.5, -0.520))

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
M_PILLAR = make_mat("pillar", (0.52, 0.46, 0.38))   # 기암 몸통(사암 띠)
M_PILLAR2 = make_mat("pillar2", (0.42, 0.38, 0.32)) # 기암 어두운 띠
M_GREEN = make_mat("green", (0.10, 0.50, 0.24))     # 기둥 꼭대기 초목
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))       # 정자 기와
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))       # 정자 기둥


def cyl(name, r_bottom, r_top, h, x, y, z, mat, verts=6, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r_bottom, radius2=r_top, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def box(name, sx, sy, sz, x, y, z, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


# ── 타일 본체 5개 ──
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


def pillar(name, x, y, r, h, lean_x=0.0, lean_y=0.0, dark=False):
    """수직 기암 기둥: 몸통(2단 띠) + 초목 캡. lean으로 살짝 기울임."""
    mat_lo = M_PILLAR2 if dark else M_PILLAR
    mat_hi = M_PILLAR if dark else M_PILLAR2
    h1, h2 = h * 0.62, h * 0.38
    cyl(f"{name}_lo", r, r * 0.88, h1, x, y, Z + h1 / 2, mat_lo, verts=6, rot_z=x * 3 + y)
    cyl(f"{name}_hi", r * 0.88, r * 0.72, h2, x + lean_x, y + lean_y,
        Z + h1 + h2 / 2, mat_hi, verts=6, rot_z=y * 2 + x)
    # 초목 캡(납작 콘)
    cyl(f"{name}_cap", r * 1.05, r * 0.35, 0.07, x + lean_x, y + lean_y,
        Z + h + 0.035, M_GREEN, verts=6, rot_z=x + y * 2)


# ── 기암 기둥 숲(높이 제각각, 중앙이 가장 높음) ──
pillar("p_main", 0.02, 0.30, 0.150, 1.45)                      # 최고봉
pillar("p2", -0.30, 0.10, 0.125, 1.10, lean_x=-0.02, dark=True)  # 정자 기둥
pillar("p3", 0.34, -0.05, 0.115, 0.95, lean_x=0.03)
pillar("p4", -0.62, 0.44, 0.100, 0.80, lean_y=0.02, dark=True)
pillar("p5", 0.70, 0.48, 0.095, 0.72)
pillar("p6", -0.15, -0.42, 0.090, 0.62, lean_x=-0.02)
pillar("p7", 0.45, -0.50, 0.080, 0.50, dark=True)
pillar("p8", -0.85, -0.15, 0.075, 0.44)
pillar("p9", 1.02, 0.05, 0.070, 0.38, dark=True)
pillar("p10", -0.45, -0.62, 0.060, 0.30)

# ── 정자(p2 꼭대기): 붉은 기둥 4 + 청기와 지붕 ──
PX, PY, PZ = -0.32, 0.10, Z + 1.10 + 0.07
box("pav_floor", 0.16, 0.16, 0.02, PX, PY, PZ + 0.01, M_WOOD)
for sx in (-1, 1):
    for sy in (-1, 1):
        box(f"pav_post_{sx}_{sy}", 0.018, 0.018, 0.09,
            PX + sx * 0.06, PY + sy * 0.06, PZ + 0.02 + 0.045, M_WOOD)
cyl("pav_roof", 0.13, 0.01, 0.07, PX, PY, PZ + 0.11 + 0.035, M_ROOF, verts=4, rot_z=math.radians(45))

# ── 기슭 초목 ──
for i, (tx, ty) in enumerate(((1.3, 0.4), (-1.3, 0.35), (0.0, -0.85), (0.95, -0.55), (-0.95, -0.55))):
    cyl(f"tree{i}", 0.045, 0.004, 0.09, tx, ty, Z + 0.045, M_GREEN, verts=6)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
