# 마을 모양 5(동양풍 격자 마을, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_village5.py
#
# 컨셉(사용자 정의, 2026-08-05): 작은집 6채가 오와 열을 맞춰(2행×3열) 정연하게 늘어선 마을.
# 행 사이에 흙길 골목, 남쪽 출입구에서 골목으로 이어지는 길. 외곽 담은 마을 1~4와 동일.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\village-5.glb"

HEX_R = 0.5774
TILE_H = 0.2

# 기둥 윗면이 몸체 윗면과 같은 평면이면 둘 다 위를 향해 z-파이팅한다(후면 컬링으로 안 없어짐).
# 이만큼 높여 윗면을 위쪽 처마 속에 묻는다.
POST_RISE = 0.006

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


M_GRASS = make_mat("grass", (0.28, 0.60, 0.20))
M_SIDE = make_mat("side", (0.42, 0.30, 0.18))
M_YARD = make_mat("yard", (0.52, 0.40, 0.24))
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))
M_WALL = make_mat("wall", (0.58, 0.42, 0.22))
M_WALL2 = make_mat("wall2", (0.62, 0.48, 0.30))
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def pyramid(name, r_bottom, r_top, h, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r_bottom, radius2=r_top, depth=h,
        location=(x, y, z), rotation=(0, 0, math.radians(45) + rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 풀 마당 육각 기단 ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_GRASS)
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H

# ── 흙길: 행 사이 가로 골목 + 남쪽 출입구에서 골목까지 세로 길 ──
box("lane_mid", 0.68, 0.085, 0.014, 0.0, 0.02, Z + 0.004, M_YARD)
box("lane_south", 0.085, 0.30, 0.014, 0.0, -0.28, Z + 0.004, M_YARD)


def build_house(tag, cx, cy, w, wall_mat, rot_z):
    """동양풍 작은 집(마을 1~4와 동일 양식)."""
    h = w * 0.43
    d = w * 0.8
    box(f"{tag}_body", w, d, h, cx, cy, Z + h / 2, wall_mat, rot_z=rot_z)
    cs, sn = math.cos(rot_z), math.sin(rot_z)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dx, dy = sx * (w / 2 - 0.011), sy * (d / 2 - 0.011)
            box(f"{tag}_post_{sx}_{sy}", 0.024, 0.024, h + POST_RISE,
                cx + dx * cs - dy * sn, cy + dx * sn + dy * cs, Z + (h + POST_RISE) / 2, M_WOOD, rot_z=rot_z)
    pyramid(f"{tag}_eave", w * 0.80, w * 0.46, w * 0.12, cx, cy, Z + h + w * 0.06, M_ROOF, rot_z=rot_z)
    pyramid(f"{tag}_roof", w * 0.52, 0.012, w * 0.26, cx, cy, Z + h + w * 0.12 + w * 0.13, M_ROOF, rot_z=rot_z)


# ── 집 6채: 2행×3열 오와 열 정렬, 모두 골목(가운데)을 향해 남향/북향 ──
W_HOUSE = 0.145
COLS = (-0.24, 0.0, 0.24)
for ci, cx in enumerate(COLS):
    build_house(f"row_n_{ci}", cx, +0.175, W_HOUSE, M_WALL if ci % 2 == 0 else M_WALL2, 0.0)
    build_house(f"row_s_{ci}", cx, -0.135, W_HOUSE, M_WALL2 if ci % 2 == 0 else M_WALL, 0.0)

# ── 외곽 담(마을 1~4와 동일): 타일 육각과 같은 방향, 남쪽 꼭짓점 출입구 ──
FENCE_R = 0.50
FENCE_H = 0.042
FENCE_T = 0.028


def fence_piece(name, x1, y1, x2, y2, dz):
    """dz: 조각별 미세 높이차 — 모서리 겹침 z-파이팅 방지."""
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    length = math.hypot(x2 - x1, y2 - y1)
    ang = math.atan2(y2 - y1, x2 - x1)
    box(f"{name}_wall", length, FENCE_T, FENCE_H + dz, mx, my, Z + (FENCE_H + dz) / 2, M_WALL, rot_z=ang)
    box(f"{name}_cap", length, FENCE_T * 1.5, 0.012, mx, my, Z + FENCE_H + dz + 0.006, M_ROOF, rot_z=ang)


for i in range(6):
    a1, a2 = math.radians(60 * i + 30), math.radians(60 * (i + 1) + 30)
    x1, y1 = FENCE_R * math.cos(a1), FENCE_R * math.sin(a1)
    x2, y2 = FENCE_R * math.cos(a2), FENCE_R * math.sin(a2)
    dz = (i % 3) * 0.0012
    if i == 3:
        fence_piece(f"fence_{i}", x1, y1, x1 + (x2 - x1) * 0.72, y1 + (y2 - y1) * 0.72, dz)
    elif i == 4:
        fence_piece(f"fence_{i}", x1 + (x2 - x1) * 0.28, y1 + (y2 - y1) * 0.28, x2, y2, dz)
    else:
        fence_piece(f"fence_{i}", x1, y1, x2, y2, dz)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
