# 소형 항구(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_port_small.py
#
# 컨셉(사용자 정의, 2026-08-05): 킷 dock은 테마 불일치라 커스텀.
# 물가 땅 타일: 선착장 창고(기와집) + 남쪽(-Y)으로 물 위까지 뻗는 나무 잔교 +
# 잔교 옆 나룻배 + 상자·항아리·계선주. 잔교가 향하는 남쪽이 물이 되도록
# Godot(MapView3D)에서 인접 물 타일 방향으로 회전시킨다.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\port-small.glb"

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


M_GRASS = make_mat("grass", (0.28, 0.60, 0.20))
M_SIDE = make_mat("side", (0.42, 0.30, 0.18))
M_SAND = make_mat("sand", (0.84, 0.58, 0.24))    # 물가 모래 — 톤 씻김 보정 채도 강화
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))
M_WALL = make_mat("wall", (0.58, 0.40, 0.18))
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))
M_PLANK = make_mat("plank", (0.52, 0.30, 0.11))  # 잔교 판자
M_BOAT = make_mat("boat", (0.38, 0.20, 0.08))    # 나룻배 선체
M_JAR = make_mat("jar", (0.36, 0.20, 0.10))
M_CRATE = make_mat("crate", (0.60, 0.38, 0.13))


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


def cylinder(name, r, depth, x, y, z, mat, verts=7):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 물가 땅 타일(윗면 풀 + 옆면 흙) ──
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

# ── 남쪽 물가 모래톱(잔교가 시작되는 자리) ──
box("shore_sand", 0.46, 0.20, 0.014, 0.0, -0.33, Z + 0.004, M_SAND)

# ── 선착장 창고: 기와 창고채(북서쪽, 잔교를 바라봄) ──
HW, HD, HH = 0.26, 0.19, 0.10
HX, HY, HROT = -0.12, 0.15, math.radians(10)
box("house_body", HW, HD, HH, HX, HY, Z + HH / 2, M_WALL, rot_z=HROT)
cs, sn = math.cos(HROT), math.sin(HROT)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (HW / 2 - 0.013), sy * (HD / 2 - 0.013)
        box(f"house_post_{sx}_{sy}", 0.028, 0.028, HH,
            HX + dx * cs - dy * sn, HY + dx * sn + dy * cs, Z + HH / 2, M_WOOD, rot_z=HROT)
# 창고 문(잔교 쪽)
box("house_door", 0.07, 0.02, 0.07, HX + 0.03, HY - HD / 2 - 0.004, Z + 0.035, M_ROOF, rot_z=HROT)
pyramid("house_eave", HW * 0.72, HW * 0.40, 0.030, HX, HY, Z + HH + 0.015, M_ROOF, rot_z=HROT)
pyramid("house_roof", HW * 0.46, 0.012, 0.062, HX, HY, Z + HH + 0.030 + 0.031, M_ROOF, rot_z=HROT)

# ── 나무 잔교: 남쪽으로 물 위까지 — 갑판 + 말뚝(물속으로) + 계선주 ──
DECK_Z = Z + 0.030
box("pier_deck", 0.15, 0.50, 0.020, 0.04, -0.44, DECK_Z, M_PLANK, rot_z=math.radians(-4))
box("pier_step", 0.17, 0.08, 0.012, 0.03, -0.22, Z + 0.012, M_PLANK)
for i, (px, py) in enumerate(((-0.02, -0.32), (0.10, -0.34), (-0.03, -0.52), (0.11, -0.54), (0.00, -0.66), (0.12, -0.67))):
    cylinder(f"pier_post_{i}", 0.013, DECK_Z - 0.04, px, py, (DECK_Z + 0.04) / 2, M_WOOD, verts=5)
cylinder("mooring", 0.017, 0.055, 0.13, -0.60, DECK_Z + 0.028, M_WOOD, verts=5)

# ── 나룻배: 잔교 서쪽에 정박(선체 + 뱃전 + 가로 걸판 2) ──
BX, BY, BROT = -0.16, -0.56, math.radians(12)
bcs, bsn = math.cos(BROT), math.sin(BROT)
box("boat_hull", 0.085, 0.22, 0.035, BX, BY, 0.075, M_BOAT, rot_z=BROT)
box("boat_inner", 0.060, 0.185, 0.030, BX, BY, 0.086, M_JAR, rot_z=BROT)
for i, off in enumerate((-0.055, 0.05)):
    box(f"boat_bench_{i}", 0.062, 0.02, 0.012, BX - off * bsn, BY + off * bcs, 0.098, M_PLANK, rot_z=BROT)

# ── 짐: 상자 2 + 항아리 1 (잔교 초입) ──
box("crate_1", 0.055, 0.055, 0.055, 0.16, -0.18, Z + 0.0275, M_CRATE, rot_z=0.3)
box("crate_2", 0.045, 0.045, 0.045, 0.22, -0.24, Z + 0.0225, M_CRATE, rot_z=0.9)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.032, radius2=0.018, depth=0.068,
                                location=(0.24, -0.13, Z + 0.034))
jar = bpy.context.object
jar.name = "jar"
jar.data.materials.append(M_JAR)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
