# 마을 모양 2(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_village2.py
#
# 컨셉(사용자 정의, 2026-08-05): 동양풍 작은집 2채 + 2단집 1채 + 작은나무 1,
# 작은집 1채에는 굴뚝(연기는 Godot 파티클, 위치 고정 필요 — 타일 회전 금지),
# 외곽 담은 마을 모양 1과 동일(육각 경계 흙담+기와 갓, 남쪽 출입구).
import bpy
import math

OUT = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models\village-2.glb"

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
M_STONE = make_mat("stone", (0.45, 0.45, 0.43))
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


def cylinder(name, r, depth, x, y, z, mat, verts=8):
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

# ── 가운데 흙마당 ──
cylinder("yard", 0.17, 0.012, 0.0, -0.03, Z + 0.006, M_YARD)


def build_house(tag, cx, cy, w, wall_mat, rot_z):
    """동양풍 작은 집(마을 1과 동일 양식)."""
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
    """2단집: 1층 몸체+처마 기와 → 위층 몸체+정지붕 (작은성 2단 건물 축소 양식)."""
    h1 = w * 0.40
    d = w * 0.85
    box(f"{tag}_body1", w, d, h1, cx, cy, Z + h1 / 2, M_WALL, rot_z=rot_z)
    cs, sn = math.cos(rot_z), math.sin(rot_z)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dx, dy = sx * (w / 2 - 0.013), sy * (d / 2 - 0.013)
            box(f"{tag}_post_{sx}_{sy}", 0.030, 0.030, h1 + POST_RISE,
                cx + dx * cs - dy * sn, cy + dx * sn + dy * cs, Z + (h1 + POST_RISE) / 2, M_WOOD, rot_z=rot_z)
    z1 = Z + h1
    pyramid(f"{tag}_eave1", w * 0.82, w * 0.42, w * 0.11, cx, cy, z1 + w * 0.055, M_ROOF, rot_z=rot_z)
    # 위층(축소 몸체) + 정지붕
    w2, h2 = w * 0.60, w * 0.30
    z2 = z1 + w * 0.11
    box(f"{tag}_body2", w2, w2 * 0.85, h2, cx, cy, z2 + h2 / 2, M_WALL2, rot_z=rot_z)
    pyramid(f"{tag}_eave2", w2 * 0.82, w2 * 0.40, w2 * 0.12, cx, cy, z2 + h2 + w2 * 0.06, M_ROOF, rot_z=rot_z)
    pyramid(f"{tag}_roof", w2 * 0.50, 0.010, w2 * 0.30, cx, cy, z2 + h2 + w2 * 0.12 + w2 * 0.15, M_ROOF, rot_z=rot_z)


# ── 집 배치: 북쪽 2단집, 남서 작은집(굴뚝), 남동 작은집 ──
build_house_2story("main", 0.0, 0.24, 0.26, math.radians(0))
build_house("small_a", -0.25, -0.11, 0.19, M_WALL2, math.radians(28))   # 굴뚝 있는 집
build_house("small_b", 0.24, -0.16, 0.17, M_WALL, math.radians(-30))

# ── 작은집 A의 돌 굴뚝(연기 파티클 기준점 — Godot 오프셋 (-0.11, 0.38, 0.08)과 일치해야 함) ──
CHX, CHY, CH_TOP = -0.11, -0.08, 0.36
cylinder("chimney", 0.020, CH_TOP - Z, CHX, CHY, (Z + CH_TOP) / 2, M_STONE, verts=6)

# ── 작은나무 1그루(동쪽) ──
TX, TY = 0.30, 0.06
cylinder("tree_trunk", 0.018, 0.07, TX, TY, Z + 0.035, M_TRUNK, verts=6)
for i, (r, h, dz) in enumerate(((0.075, 0.09, 0.07), (0.055, 0.08, 0.135))):
    bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=r, radius2=0.008, depth=h,
                                    location=(TX, TY, Z + dz + h / 2))
    leaf = bpy.context.object
    leaf.name = f"tree_leaf_{i}"
    leaf.data.materials.append(M_LEAF)

# ── 외곽 담(마을 모양 1과 동일): 타일 육각과 같은 방향, 남쪽 꼭짓점 출입구 ──
FENCE_R = 0.50
FENCE_H = 0.042
FENCE_T = 0.028


def fence_piece(name, x1, y1, x2, y2):
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    length = math.hypot(x2 - x1, y2 - y1)
    ang = math.atan2(y2 - y1, x2 - x1)
    box(f"{name}_wall", length, FENCE_T, FENCE_H, mx, my, Z + FENCE_H / 2, M_WALL, rot_z=ang)
    box(f"{name}_cap", length, FENCE_T * 1.5, 0.012, mx, my, Z + FENCE_H + 0.006, M_ROOF, rot_z=ang)


for i in range(6):
    a1, a2 = math.radians(60 * i + 30), math.radians(60 * (i + 1) + 30)
    x1, y1 = FENCE_R * math.cos(a1), FENCE_R * math.sin(a1)
    x2, y2 = FENCE_R * math.cos(a2), FENCE_R * math.sin(a2)
    if i == 3:
        fence_piece(f"fence_{i}", x1, y1, x1 + (x2 - x1) * 0.72, y1 + (y2 - y1) * 0.72)
    elif i == 4:
        fence_piece(f"fence_{i}", x1 + (x2 - x1) * 0.28, y1 + (y2 - y1) * 0.28, x2, y2)
    else:
        fence_piece(f"fence_{i}", x1, y1, x2, y2)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
