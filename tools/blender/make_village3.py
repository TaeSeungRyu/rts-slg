# 마을 모양 3(동양풍 길가 마을, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_village3.py
#
# 컨셉(사용자 정의, 2026-08-05): 마을 모양 2 계열(작은 기와집)을 다른 배치·모양으로 —
# 남쪽 출입구에서 북쪽으로 흙길이 이어지고, 길 서쪽에 길쭉한 창고채,
# 동쪽에 작은집 2채가 길을 향해 늘어선 길가 마을. 장독 3개 + 나무 2그루.
# (1차 ㅁ자 사합원 안은 "성 느낌"이라 폐기 — 2026-08-05)
# 외곽 담은 마을 1·2와 동일(육각 경계 흙담+기와 갓, 남쪽 출입구).
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\village-3.glb"

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
M_JAR = make_mat("jar", (0.38, 0.22, 0.12))
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


def ridge_roof(name, sx, sy, h, x, y, z, mat, rot_z=0.0):
    """길쭉한 지붕: 회전 적용 후 비율 스케일(회전된 오브젝트에 비등방 스케일 금지)."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=0.5, radius2=0.055, depth=h,
        location=(x, y, z), rotation=(0, 0, math.radians(45)))
    o = bpy.context.object
    o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    o.scale = (sx * 1.42, sy * 1.42, 1.0)
    o.rotation_euler = (0, 0, rot_z)
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

# ── 흙길: 남쪽 출입구(담 트임)에서 북쪽으로 이어지는 길 + 가운데 갈림 마당 ──
box("path_s", 0.10, 0.34, 0.014, 0.0, -0.26, Z + 0.004, M_YARD)
box("path_n", 0.09, 0.26, 0.014, -0.03, 0.06, Z + 0.004, M_YARD, rot_z=math.radians(8))
cylinder("path_plaza", 0.115, 0.014, 0.0, -0.06, Z + 0.004, M_YARD)


def build_house(tag, cx, cy, w, wall_mat, rot_z):
    """동양풍 작은 집(마을 1·2와 동일 양식)."""
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


def build_longhouse(tag, cx, cy, rot_z):
    """길쭉한 창고채: 낮고 긴 몸체 + 능선 있는 긴 기와지붕."""
    L, D, H = 0.34, 0.15, 0.085
    box(f"{tag}_body", L, D, H, cx, cy, Z + H / 2, M_WALL, rot_z=rot_z)
    cs, sn = math.cos(rot_z), math.sin(rot_z)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dx, dy = sx * (L / 2 - 0.014), sy * (D / 2 - 0.012)
            box(f"{tag}_post_{sx}_{sy}", 0.026, 0.026, H + POST_RISE,
                cx + dx * cs - dy * sn, cy + dx * sn + dy * cs, Z + (H + POST_RISE) / 2, M_WOOD, rot_z=rot_z)
    # footprint 반너비 = 0.5*s이므로 몸체(L, D)보다 크게 줘야 처마가 밖으로 나온다
    ridge_roof(f"{tag}_roof", L * 1.18, D * 1.35, 0.075, cx, cy, Z + H + 0.030, M_ROOF, rot_z=rot_z)


# ── 배치: 길 서쪽 길쭉한 창고채, 길 동쪽 작은집 2채(길을 향해 살짝 돌림) ──
build_longhouse("store", -0.24, 0.06, math.radians(78))
build_house("house_a", 0.22, 0.16, 0.19, M_WALL2, math.radians(-58))
build_house("house_b", 0.21, -0.18, 0.165, M_WALL, math.radians(-115))

# ── 장독 3개(창고채 남쪽 끝) ──
for i, (jx, jy, jr) in enumerate(((-0.16, -0.20, 0.034), (-0.22, -0.15, 0.028), (-0.10, -0.15, 0.024))):
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=jr, radius2=jr * 0.55,
                                    depth=jr * 2.1, location=(jx, jy, Z + jr * 1.05))
    jar = bpy.context.object
    jar.name = f"jar_{i}"
    jar.data.materials.append(M_JAR)


def tree(tag, tx, ty, s=1.0):
    cylinder(f"{tag}_trunk", 0.018 * s, 0.07 * s, tx, ty, Z + 0.035 * s, M_TRUNK, verts=6)
    for i, (r, h, dz) in enumerate(((0.075 * s, 0.09 * s, 0.07 * s), (0.055 * s, 0.08 * s, 0.135 * s))):
        bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=r, radius2=0.008, depth=h,
                                        location=(tx, ty, Z + dz + h / 2))
        leaf = bpy.context.object
        leaf.name = f"{tag}_leaf_{i}"
        leaf.data.materials.append(M_LEAF)


# ── 나무 2그루(북쪽·남동쪽) ──
tree("tree_n", -0.02, 0.30, 1.0)
tree("tree_se", 0.34, -0.02, 0.8)

# ── 외곽 담(마을 1·2와 동일): 타일 육각과 같은 방향, 남쪽 꼭짓점 출입구 ──
FENCE_R = 0.50
FENCE_H = 0.042
FENCE_T = 0.028


def fence_piece(name, x1, y1, x2, y2, dz):
    """dz: 조각별 미세 높이차 — 이웃 조각과 모서리에서 겹칠 때 윗면 z-파이팅 방지."""
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
