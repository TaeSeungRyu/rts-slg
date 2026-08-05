# 마을 모양 1(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_village1.py
#
# 컨셉(사용자 정의, 2026-08-05): 동양풍의 작은 집 3채가 모여있는 형식.
# 킷 building-village는 유럽풍이라 커스텀 — 공방·성과 같은 양식(흙벽+모서리 기둥+겹 기와지붕).
# 타일 일체형(반경 0.5774, 높이 0.2): 풀 마당 + 가운데 흙마당을 둘러싼 집 3채 + 우물·장독.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\village-1.glb"

HEX_R = 0.5774
TILE_H = 0.2

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


# 잉크워시 톤(채도 0.5)에서 살아남도록 채도를 강하게 준다
M_GRASS = make_mat("grass", (0.28, 0.60, 0.20))  # 풀 마당
M_SIDE = make_mat("side", (0.42, 0.30, 0.18))    # 타일 옆면 흙
M_YARD = make_mat("yard", (0.52, 0.40, 0.24))    # 가운데 흙마당
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))    # 짙은 청기와
M_WALL = make_mat("wall", (0.58, 0.42, 0.22))    # 흙벽
M_WALL2 = make_mat("wall2", (0.62, 0.48, 0.30))  # 밝은 흙벽(집마다 변화)
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))    # 붉은 목재
M_STONE = make_mat("stone", (0.45, 0.45, 0.43))  # 우물 돌
M_WATER = make_mat("water", (0.20, 0.45, 0.70), roughness=0.15)  # 우물물
M_JAR = make_mat("jar", (0.38, 0.22, 0.12))      # 장독


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


def cylinder(name, r, depth, x, y, z, mat, verts=10):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 풀 마당 육각 기단(윗면 풀 + 옆면 흙, 타일과 같은 방향) ──
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

# ── 가운데 흙마당(집들이 둘러싸는 공용 마당) ──
cylinder("yard", 0.19, 0.012, 0.0, -0.02, Z + 0.006, M_YARD, verts=8)


def build_house(tag, cx, cy, w, wall_mat, rot_z):
    """동양풍 작은 집: 흙벽 + 모서리 기둥 + 처마·지붕 겹 기와. rot_z로 향을 돌린다."""
    h = w * 0.43
    d = w * 0.8
    box(f"{tag}_body", w, d, h, cx, cy, Z + h / 2, wall_mat, rot_z=rot_z)
    cs, sn = math.cos(rot_z), math.sin(rot_z)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dx, dy = sx * (w / 2 - 0.013), sy * (d / 2 - 0.013)
            box(f"{tag}_post_{sx}_{sy}", 0.028, 0.028, h,
                cx + dx * cs - dy * sn, cy + dx * sn + dy * cs, Z + h / 2, M_WOOD, rot_z=rot_z)
    pyramid(f"{tag}_eave", w * 0.80, w * 0.46, w * 0.12, cx, cy, Z + h + w * 0.06, M_ROOF, rot_z=rot_z)
    pyramid(f"{tag}_roof", w * 0.52, 0.012, w * 0.26, cx, cy, Z + h + w * 0.12 + w * 0.13, M_ROOF, rot_z=rot_z)


# ── 집 3채: 크기·향·벽색을 조금씩 다르게, 마당을 향해 모여 앉는다 ──
build_house("house_a", -0.02, 0.26, 0.24, M_WALL, math.radians(0))     # 북쪽 큰 집
build_house("house_b", -0.27, -0.13, 0.19, M_WALL2, math.radians(32))  # 남서쪽 집
build_house("house_c", 0.25, -0.16, 0.17, M_WALL, math.radians(-28))   # 남동쪽 집

# ── 우물: 돌 테두리 + 물 ──
WX, WY = 0.02, -0.03
cylinder("well_ring", 0.055, 0.05, WX, WY, Z + 0.025, M_STONE, verts=8)
cylinder("well_water", 0.038, 0.052, WX, WY, Z + 0.026, M_WATER, verts=8)

# ── 낮은 테두리 담: 육각 경계를 따라 도는 흙담 + 기와 갓, 남쪽에 출입구 트임 ──
FENCE_R = 0.50       # 담 반경(타일 경계 0.5774 안쪽)
FENCE_H = 0.042      # 담 높이(집보다 훨씬 낮게)
FENCE_T = 0.028      # 담 두께


def fence_piece(name, x1, y1, x2, y2):
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    length = math.hypot(x2 - x1, y2 - y1)
    ang = math.atan2(y2 - y1, x2 - x1)
    box(f"{name}_wall", length, FENCE_T, FENCE_H, mx, my, Z + FENCE_H / 2, M_WALL, rot_z=ang)
    # 담 위 기와 갓(살짝 넓게)
    box(f"{name}_cap", length, FENCE_T * 1.5, 0.012, mx, my, Z + FENCE_H + 0.006, M_ROOF, rot_z=ang)


# 타일 육각의 꼭짓점은 30°+60k 방향(포인티탑, Blender 기준) — 담도 같은 방향으로 맞춘다
for i in range(6):
    a1, a2 = math.radians(60 * i + 30), math.radians(60 * (i + 1) + 30)
    x1, y1 = FENCE_R * math.cos(a1), FENCE_R * math.sin(a1)
    x2, y2 = FENCE_R * math.cos(a2), FENCE_R * math.sin(a2)
    if i == 3:    # 남쪽 꼭짓점(270°)으로 끝나는 변: 끝을 잘라 출입구 반쪽
        fence_piece(f"fence_{i}", x1, y1, x1 + (x2 - x1) * 0.72, y1 + (y2 - y1) * 0.72)
    elif i == 4:  # 남쪽 꼭짓점에서 시작하는 변: 앞을 잘라 출입구 반쪽
        fence_piece(f"fence_{i}", x1 + (x2 - x1) * 0.28, y1 + (y2 - y1) * 0.28, x2, y2)
    else:
        fence_piece(f"fence_{i}", x1, y1, x2, y2)

# ── 장독 2개(큰 집 옆) ──
for i, (jx, jy, jr) in enumerate(((0.20, 0.20, 0.035), (0.26, 0.13, 0.028))):
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=jr, radius2=jr * 0.55,
                                    depth=jr * 2.1, location=(jx, jy, Z + jr * 1.05))
    jar = bpy.context.object
    jar.name = f"jar_{i}"
    jar.data.materials.append(M_JAR)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
