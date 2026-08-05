# 소형 항구(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_port_small.py
#
# 컨셉(사용자 정의, 2026-08-05): 킷 dock은 테마 불일치라 커스텀.
# ※ 이 모델은 "내용물만" 담는다 — 육각 기단 없음. 바닥은 Godot에서 일반 풀 타일을
#   회전 없이 깔고, 이 내용물만 인접 물 방향으로 회전시킨다(기단이 돌면 그리드와 어긋남).
# 구성: 선착장 창고(기와 겹지붕) + 물 위로 뻗는 잔교(판자 이음매·난간·초롱) +
#       정박 나룻배(이물 뾰족) + 상자·통·항아리 + 물가 모래톱.
# 내용물의 지면 기준 z = 0.2(타일 윗면), 물 위 요소(배)는 z ~0.07.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\port-small.glb"

Z = 0.2  # 타일 윗면(지면) 높이

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


M_SAND = make_mat("sand", (0.84, 0.58, 0.24))
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))
M_WALL = make_mat("wall", (0.58, 0.40, 0.18))
M_WALL2 = make_mat("wall2", (0.64, 0.48, 0.26))
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))
M_PLANK = make_mat("plank", (0.52, 0.30, 0.11))
M_SEAM = make_mat("seam", (0.38, 0.21, 0.08))    # 판자 이음매(어두운 줄)
M_BOAT = make_mat("boat", (0.38, 0.20, 0.08))
M_JAR = make_mat("jar", (0.36, 0.20, 0.10))
M_CRATE = make_mat("crate", (0.60, 0.38, 0.13))
M_LANTERN = make_mat("lantern", (0.85, 0.30, 0.12), roughness=0.5)  # 붉은 초롱


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


# ── 물가 모래톱(잔교가 시작되는 남쪽) ──
box("shore_sand", 0.40, 0.17, 0.014, 0.0, -0.32, Z + 0.004, M_SAND)

# ── 선착장 창고: 마을과 같은 양식(모서리 기둥+겹 기와지붕), 잔교를 바라봄 ──
HW, HD, HH = 0.25, 0.19, 0.105
HX, HY, HROT = -0.13, 0.14, math.radians(8)
box("house_body", HW, HD, HH, HX, HY, Z + HH / 2, M_WALL, rot_z=HROT)
cs, sn = math.cos(HROT), math.sin(HROT)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (HW / 2 - 0.013), sy * (HD / 2 - 0.013)
        box(f"house_post_{sx}_{sy}", 0.028, 0.028, HH,
            HX + dx * cs - dy * sn, HY + dx * sn + dy * cs, Z + HH / 2, M_WOOD, rot_z=HROT)
box("house_door", 0.075, 0.02, 0.075, HX + 0.02 * cs + (HD / 2 + 0.004) * sn,
    HY + 0.02 * sn - (HD / 2 + 0.004) * cs, Z + 0.038, M_ROOF, rot_z=HROT)
pyramid("house_eave", HW * 0.80, HW * 0.46, HW * 0.12, HX, HY, Z + HH + HW * 0.06, M_ROOF, rot_z=HROT)
pyramid("house_roof", HW * 0.52, 0.012, HW * 0.26, HX, HY,
        Z + HH + HW * 0.12 + HW * 0.13, M_ROOF, rot_z=HROT)

# ── 잔교: 물 위로 뻗는 갑판 + 판자 이음매 + 한쪽 난간 + 말뚝 + 계선주 + 붉은 초롱 ──
DECK_Z = Z + 0.030
PIER_ROT = math.radians(-4)
box("pier_deck", 0.15, 0.50, 0.020, 0.04, -0.44, DECK_Z, M_PLANK, rot_z=PIER_ROT)
pcs, psn = math.cos(PIER_ROT), math.sin(PIER_ROT)
for i, off in enumerate((-0.17, -0.05, 0.07, 0.19)):  # 가로 이음매 줄
    # 갑판 윗면(+0.010)과 거의 같은 평면이면 원거리에서 반짝인다 — 확실히 띄워 얹는다
    box(f"pier_seam_{i}", 0.15, 0.012, 0.006, 0.04 - off * psn, -0.44 + off * pcs,
        DECK_Z + 0.016, M_SEAM, rot_z=PIER_ROT)
for i, (px, py) in enumerate(((-0.02, -0.32), (0.10, -0.34), (-0.03, -0.52), (0.11, -0.54), (0.00, -0.66), (0.12, -0.67))):
    cylinder(f"pier_post_{i}", 0.013, DECK_Z - 0.04, px, py, (DECK_Z + 0.04) / 2, M_WOOD, verts=5)
# 동쪽 한줄 난간
for i, off in enumerate((-0.16, 0.0, 0.16)):
    box(f"rail_post_{i}", 0.014, 0.014, 0.045, 0.105 - off * psn, -0.44 + off * pcs,
        DECK_Z + 0.0325, M_WOOD, rot_z=PIER_ROT)
box("rail_bar", 0.012, 0.44, 0.012, 0.105 + 0.02 * psn, -0.44, DECK_Z + 0.055, M_WOOD, rot_z=PIER_ROT)
cylinder("mooring", 0.017, 0.055, -0.045, -0.62, DECK_Z + 0.028, M_WOOD, verts=5)
# 잔교 끝 붉은 초롱대
cylinder("lantern_pole", 0.010, 0.14, 0.105, -0.655, DECK_Z + 0.07, M_WOOD, verts=5)
box("lantern", 0.030, 0.030, 0.040, 0.105, -0.655, DECK_Z + 0.15, M_LANTERN)
box("lantern_cap", 0.040, 0.040, 0.010, 0.105, -0.655, DECK_Z + 0.175, M_ROOF)

# ── 나룻배: 잔교 서쪽 정박 — 몸통 + 뾰족한 이물·고물 + 걸판 ──
BX, BY, BROT = -0.17, -0.55, math.radians(14)
bcs, bsn = math.cos(BROT), math.sin(BROT)
box("boat_hull", 0.085, 0.20, 0.038, BX, BY, 0.074, M_BOAT, rot_z=BROT)
box("boat_inner", 0.058, 0.165, 0.030, BX, BY, 0.088, M_JAR, rot_z=BROT)
for endsign, tag in ((1, "bow"), (-1, "stern")):  # 뾰족한 이물/고물
    box(f"boat_{tag}", 0.060, 0.06, 0.034, BX - endsign * 0.115 * bsn, BY + endsign * 0.115 * bcs,
        0.078, M_BOAT, rot_z=BROT + endsign * math.radians(28))
for i, off in enumerate((-0.05, 0.05)):
    box(f"boat_bench_{i}", 0.062, 0.02, 0.012, BX - off * bsn, BY + off * bcs, 0.098, M_PLANK, rot_z=BROT)

# ── 오두막(어구 창고): 동쪽 작은 건물 ──
SW, SD, SH = 0.15, 0.12, 0.070
SX, SY, SROT = 0.24, 0.12, math.radians(-22)
box("shed_body", SW, SD, SH, SX, SY, Z + SH / 2, M_WALL2, rot_z=SROT)
scs, ssn = math.cos(SROT), math.sin(SROT)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (SW / 2 - 0.011), sy * (SD / 2 - 0.011)
        box(f"shed_post_{sx}_{sy}", 0.022, 0.022, SH,
            SX + dx * scs - dy * ssn, SY + dx * ssn + dy * scs, Z + SH / 2, M_WOOD, rot_z=SROT)
pyramid("shed_eave", SW * 0.80, SW * 0.44, SW * 0.11, SX, SY, Z + SH + SW * 0.055, M_ROOF, rot_z=SROT)
pyramid("shed_roof", SW * 0.50, 0.010, SW * 0.24, SX, SY,
        Z + SH + SW * 0.11 + SW * 0.12, M_ROOF, rot_z=SROT)

# ── 간단한 테두리: 나무 말뚝+가로대 울타리(육각 경계, 남쪽 꼭짓점은 잔교 자리로 트임) ──
# 내용물은 물 방향(60° 단위)으로만 회전하므로 울타리 방향은 항상 타일 그리드와 맞는다.
FENCE_R = 0.485
RAIL_Z = Z + 0.036
for i in range(6):
    a1, a2 = math.radians(60 * i + 30), math.radians(60 * (i + 1) + 30)
    x1, y1 = FENCE_R * math.cos(a1), FENCE_R * math.sin(a1)
    x2, y2 = FENCE_R * math.cos(a2), FENCE_R * math.sin(a2)
    t0, t1 = 0.0, 1.0
    if i == 3:      # 남쪽 꼭짓점으로 끝나는 변: 끝을 트임
        t1 = 0.62
    elif i == 4:    # 남쪽 꼭짓점에서 시작하는 변: 앞을 트임
        t0 = 0.38
    sx1, sy1 = x1 + (x2 - x1) * t0, y1 + (y2 - y1) * t0
    sx2, sy2 = x1 + (x2 - x1) * t1, y1 + (y2 - y1) * t1
    mx, my = (sx1 + sx2) / 2, (sy1 + sy2) / 2
    seg = math.hypot(sx2 - sx1, sy2 - sy1)
    ang = math.atan2(sy2 - sy1, sx2 - sx1)
    box(f"fence_rail_{i}", seg, 0.016, 0.014, mx, my, RAIL_Z + i % 3 * 0.0012, M_PLANK, rot_z=ang)
    for j, ft in enumerate((0.12, 0.5, 0.88)):
        px, py = sx1 + (sx2 - sx1) * ft, sy1 + (sy2 - sy1) * ft
        cylinder(f"fence_post_{i}_{j}", 0.011, RAIL_Z - Z + 0.010, px, py,
                 Z + (RAIL_Z - Z + 0.010) / 2, M_WOOD, verts=5)

# ── 짐: 상자 2단 쌓기 + 통 2 + 항아리 ──
box("crate_1", 0.058, 0.058, 0.058, 0.17, -0.17, Z + 0.029, M_CRATE, rot_z=0.3)
box("crate_2", 0.046, 0.046, 0.046, 0.17, -0.17, Z + 0.058 + 0.023, M_CRATE, rot_z=0.8)
box("crate_3", 0.048, 0.048, 0.048, 0.245, -0.235, Z + 0.024, M_CRATE, rot_z=1.1)
for i, (px, py) in enumerate(((0.26, -0.11), (0.215, -0.055))):
    cylinder(f"barrel_{i}", 0.026, 0.055, px, py, Z + 0.0275, M_WOOD, verts=8)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.032, radius2=0.018, depth=0.068,
                                location=(0.14, -0.05, Z + 0.034))
jar = bpy.context.object
jar.name = "jar"
jar.data.materials.append(M_JAR)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
