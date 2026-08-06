# 마을 모양 4(동양풍 호수 마을, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_village4.py
#
# 컨셉(사용자 정의, 2026-08-05): 원형 호수 주변에 집들이 모여 있는 느낌 —
# 가운데 원형 호수(모래 물가 테)를 작은 기와집 3채가 호수를 향해 둘러싸고,
# 남쪽 물가에 나무 잔교(부두 판자) 하나. 외곽 담은 마을 1~3과 동일.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\village-4.glb"

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
M_SHORE = make_mat("shore", (0.72, 0.58, 0.36))  # 호숫가 모래 테
M_WATER = make_mat("water", (0.18, 0.44, 0.72), roughness=0.12)  # 호수 물
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))
M_WALL = make_mat("wall", (0.58, 0.42, 0.22))
M_WALL2 = make_mat("wall2", (0.62, 0.48, 0.30))
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))
M_PLANK = make_mat("plank", (0.48, 0.32, 0.16))  # 잔교 판자
M_LEAF = make_mat("leaf", (0.16, 0.48, 0.14))
M_TRUNK = make_mat("trunk", (0.34, 0.20, 0.10))


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


def cylinder(name, r, depth, x, y, z, mat, verts=12):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z))
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

# ── 원형 호수: 모래 물가 테(아래) + 물(위로 살짝 솟게 — 접촉면 z-파이팅 방지) ──
cylinder("lake_shore", 0.215, 0.012, 0.0, 0.0, Z + 0.004, M_SHORE, verts=14)
cylinder("lake_water", 0.165, 0.016, 0.0, 0.0, Z + 0.009, M_WATER, verts=14)


def build_house(tag, cx, cy, w, wall_mat, rot_z):
    """동양풍 작은 집(마을 1~3과 동일 양식)."""
    h = w * 0.43
    d = w * 0.8
    box(f"{tag}_body", w, d, h, cx, cy, Z + h / 2, wall_mat, rot_z=rot_z)
    cs, sn = math.cos(rot_z), math.sin(rot_z)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dx, dy = sx * (w / 2 - 0.013), sy * (d / 2 - 0.013)
            box(f"{tag}_post_{sx}_{sy}", 0.028, 0.028, h + POST_RISE,
                cx + dx * cs - dy * sn, cy + dx * sn + dy * cs, Z + (h + POST_RISE) / 2, M_WOOD, rot_z=rot_z)
    pyramid(f"{tag}_eave", w * 0.80, w * 0.46, w * 0.12, cx, cy, Z + h + w * 0.06, M_ROOF, rot_z=rot_z)
    pyramid(f"{tag}_roof", w * 0.52, 0.012, w * 0.26, cx, cy, Z + h + w * 0.12 + w * 0.13, M_ROOF, rot_z=rot_z)


def build_house_2story(tag, cx, cy, w, rot_z):
    """2단집(마을 2와 동일 양식): 1층+처마 기와 → 축소 위층+정지붕."""
    h1 = w * 0.40
    d = w * 0.85
    box(f"{tag}_body1", w, d, h1, cx, cy, Z + h1 / 2, M_WALL, rot_z=rot_z)
    cs, sn = math.cos(rot_z), math.sin(rot_z)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dx, dy = sx * (w / 2 - 0.012), sy * (d / 2 - 0.012)
            box(f"{tag}_post_{sx}_{sy}", 0.026, 0.026, h1 + POST_RISE,
                cx + dx * cs - dy * sn, cy + dx * sn + dy * cs, Z + (h1 + POST_RISE) / 2, M_WOOD, rot_z=rot_z)
    z1 = Z + h1
    pyramid(f"{tag}_eave1", w * 0.82, w * 0.42, w * 0.11, cx, cy, z1 + w * 0.055, M_ROOF, rot_z=rot_z)
    w2, h2 = w * 0.60, w * 0.30
    z2 = z1 + w * 0.11
    box(f"{tag}_body2", w2, w2 * 0.85, h2, cx, cy, z2 + h2 / 2, M_WALL2, rot_z=rot_z)
    pyramid(f"{tag}_eave2", w2 * 0.82, w2 * 0.40, w2 * 0.12, cx, cy, z2 + h2 + w2 * 0.06, M_ROOF, rot_z=rot_z)
    pyramid(f"{tag}_roof", w2 * 0.50, 0.010, w2 * 0.30, cx, cy, z2 + h2 + w2 * 0.12 + w2 * 0.15, M_ROOF, rot_z=rot_z)


# ── 집 4채(줄인 크기): 북쪽은 2단집, 나머지 3채는 작은집 — 각자 호수를 향해 돌아앉음 ──
R_HOUSE = 0.335
a_n = math.radians(90)
build_house_2story("house_n2", R_HOUSE * math.cos(a_n), R_HOUSE * math.sin(a_n),
                   0.17, a_n + math.radians(90))
for tag, deg, w, mat in (
    ("house_w", 158, 0.15, M_WALL2),
    ("house_sw", 214, 0.145, M_WALL),
    ("house_se", 330, 0.15, M_WALL2),
):
    a = math.radians(deg)
    hx, hy = R_HOUSE * math.cos(a), R_HOUSE * math.sin(a)
    build_house(tag, hx, hy, w, mat, a + math.radians(90))

# ── 남쪽 물가 잔교: 판자 + 말뚝 2개 ──
box("pier", 0.05, 0.14, 0.012, 0.02, -0.175, Z + 0.024, M_PLANK, rot_z=math.radians(-8))
for i, (px, py) in enumerate(((0.045, -0.115), (-0.005, -0.235))):
    cylinder(f"pier_post_{i}", 0.010, 0.045, px, py, Z + 0.012, M_WOOD, verts=5)

# ── 나무 1그루(북동 물가) ──
TX, TY = 0.30, 0.24
cylinder("tree_trunk", 0.018, 0.07, TX, TY, Z + 0.035, M_TRUNK, verts=6)
for i, (r, h, dz) in enumerate(((0.075, 0.09, 0.07), (0.055, 0.08, 0.135))):
    bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=r, radius2=0.008, depth=h,
                                    location=(TX, TY, Z + dz + h / 2))
    leaf = bpy.context.object
    leaf.name = f"tree_leaf_{i}"
    leaf.data.materials.append(M_LEAF)

# ── 외곽 담(마을 1~3과 동일): 타일 육각과 같은 방향, 남쪽 꼭짓점 출입구 ──
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
