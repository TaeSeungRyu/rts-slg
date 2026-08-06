# 중형 항구(동양풍, 2타일 지물, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_port_medium.py
#
# 컨셉: 소형 항구의 확장 — 동서 2타일에 걸친 부두.
# 남쪽(-Y)이 물과 접하는 자리에 배치한다(회전 없음, 기단 포함 — 산 지물과 같은 방식).
# 구성: 서 타일 대형 창고 + 동 타일 2단 사무소, 남쪽 물가를 따라 넓은 선창(워프),
#       잔교 2줄(초롱), 나무 기중기, 정박한 정크선(돛)·나룻배, 짐 더미, 북쪽 울타리.
# 원점 = 두 타일의 중점, 타일 중심은 (±0.5, 0). 지면 z = 0.2.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\port-medium.glb"

HEX_R = 0.5774
TILE_H = 0.2
Z = TILE_H

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
M_SAND = make_mat("sand", (0.84, 0.58, 0.24))
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))
M_WALL = make_mat("wall", (0.58, 0.40, 0.18))
M_WALL2 = make_mat("wall2", (0.64, 0.48, 0.26))
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))
M_PLANK = make_mat("plank", (0.52, 0.30, 0.11))
M_PLANK2 = make_mat("plank2", (0.46, 0.26, 0.10))  # 판자 교대 톤(이음매처럼 읽힘)
M_SEAM = make_mat("seam", (0.38, 0.21, 0.08))
M_BOAT = make_mat("boat", (0.38, 0.20, 0.08))
M_SAIL = make_mat("sail", (0.80, 0.68, 0.42), roughness=0.9)  # 정크 돛
M_JAR = make_mat("jar", (0.36, 0.20, 0.10))
M_CRATE = make_mat("crate", (0.60, 0.38, 0.13))
M_LANTERN = make_mat("lantern", (0.85, 0.30, 0.12), roughness=0.5)


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0, rot_y=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, rot_y, rot_z))
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


def cylinder(name, r, depth, x, y, z, mat, verts=7, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 기단: 육각 타일 2개(동서), 윗면 풀 + 옆면 흙 ──
for i, cx in enumerate((-0.5, 0.5)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=HEX_R, depth=TILE_H, location=(cx, 0, TILE_H / 2))
    b = bpy.context.object
    b.name = f"base_{i}"
    b.data.materials.append(M_SIDE)
    b.data.materials.append(M_GRASS)
    for poly in b.data.polygons:
        if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
            poly.material_index = 1

# ── 선창(워프): 남쪽 물가를 따라 두 타일에 걸친 넓은 판자 단 + 모래톱 ──
box("shore_sand", 1.30, 0.16, 0.014, 0.0, -0.34, Z + 0.004, M_SAND)
# 선창·잔교 모두 판자 단위로 쪼갠다. 맞붙여 놓아 정상 상태 실루엣은 통짜와 같고,
# 부서진 상태에서 판자를 몇 장 숨기면 그대로 구멍이 된다(DamageView).
WHARF_PLANKS = 10
WHARF_LEN = 1.34 / WHARF_PLANKS
for i in range(WHARF_PLANKS):
    wx = -0.67 + WHARF_LEN * (i + 0.5)
    box(f"wharf_plank_{i}", WHARF_LEN, 0.20, 0.026, wx, -0.44, Z + 0.020,
        M_PLANK if i % 2 == 0 else M_PLANK2)

# ── 잔교 2줄: 워프에서 남쪽 물 위로 ──
DECK_Z = Z + 0.030
for pi, px in enumerate((-0.52, 0.42)):
    PIER_PLANKS = 7
    PIER_LEN = 0.42 / PIER_PLANKS
    for j in range(PIER_PLANKS):
        off = -0.21 + PIER_LEN * (j + 0.5)
        box(f"pier{pi}_plank_{j}", 0.14, PIER_LEN, 0.020, px, -0.72 + off, DECK_Z,
            M_PLANK if j % 2 == 0 else M_PLANK2)
    for j, (dx, dy) in enumerate(((-0.05, -0.60), (0.06, -0.63), (-0.05, -0.80), (0.06, -0.83))):
        cylinder(f"pier{pi}_post_{j}", 0.013, DECK_Z - 0.04, px + dx, dy, (DECK_Z + 0.04) / 2, M_WOOD, verts=5)
    cylinder(f"pier{pi}_lpole", 0.010, 0.14, px + 0.055, -0.90, DECK_Z + 0.07, M_WOOD, verts=5)
    box(f"pier{pi}_lantern", 0.030, 0.030, 0.040, px + 0.055, -0.90, DECK_Z + 0.15, M_LANTERN)
    box(f"pier{pi}_lcap", 0.040, 0.040, 0.010, px + 0.055, -0.90, DECK_Z + 0.175, M_ROOF)

# ── 대형 창고(서 타일): 길쭉한 몸체 + 긴 능선 기와지붕 ──
WL, WD, WH = 0.44, 0.20, 0.11
WX, WY, WROT = -0.55, 0.10, math.radians(6)
box("warehouse_body", WL, WD, WH, WX, WY, Z + WH / 2, M_WALL, rot_z=WROT)
wcs, wsn = math.cos(WROT), math.sin(WROT)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (WL / 2 - 0.008), sy * (WD / 2 - 0.008)
        box(f"warehouse_post_{sx}_{sy}", 0.030, 0.030, WH,
            WX + dx * wcs - dy * wsn, WY + dx * wsn + dy * wcs, Z + WH / 2, M_WOOD, rot_z=WROT)
box("warehouse_door", 0.09, 0.02, 0.085, WX + 0.05 * wcs + (WD / 2 + 0.004) * wsn,
    WY + 0.05 * wsn - (WD / 2 + 0.004) * wcs, Z + 0.043, M_ROOF, rot_z=WROT)
ridge_roof("warehouse_roof", WL * 1.14, WD * 1.35, 0.085, WX, WY, Z + WH + 0.035, M_ROOF, rot_z=WROT)

# ── 2단 사무소(동 타일): 마을 2단집 양식 ──
OW2 = 0.22
OX, OY, OROT = 0.52, 0.14, math.radians(-8)
oh1 = OW2 * 0.40
od = OW2 * 0.85
box("office_body1", OW2, od, oh1, OX, OY, Z + oh1 / 2, M_WALL2, rot_z=OROT)
ocs, osn = math.cos(OROT), math.sin(OROT)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (OW2 / 2 - 0.008), sy * (od / 2 - 0.008)
        box(f"office_post_{sx}_{sy}", 0.026, 0.026, oh1,
            OX + dx * ocs - dy * osn, OY + dx * osn + dy * ocs, Z + oh1 / 2, M_WOOD, rot_z=OROT)
oz1 = Z + oh1
pyramid("office_eave1", OW2 * 0.82, OW2 * 0.42, OW2 * 0.11, OX, OY, oz1 + OW2 * 0.055, M_ROOF, rot_z=OROT)
ow2, oh2 = OW2 * 0.60, OW2 * 0.30
oz2 = oz1 + OW2 * 0.11
box("office_body2", ow2, ow2 * 0.85, oh2, OX, OY, oz2 + oh2 / 2, M_WALL, rot_z=OROT)
pyramid("office_eave2", ow2 * 0.82, ow2 * 0.40, ow2 * 0.12, OX, OY, oz2 + oh2 + ow2 * 0.06, M_ROOF, rot_z=OROT)
pyramid("office_roof", ow2 * 0.50, 0.010, ow2 * 0.30, OX, OY,
        oz2 + oh2 + ow2 * 0.12 + ow2 * 0.15, M_ROOF, rot_z=OROT)

# ── 추가 건물 1: 숙소(작은 기와집) — 두 타일 사이 북쪽 ──
AW, AD, AH = 0.17, 0.135, 0.073
AX, AY, AROT = -0.13, 0.30, math.radians(-12)
box("lodge_body", AW, AD, AH, AX, AY, Z + AH / 2, M_WALL2, rot_z=AROT)
acs, asn = math.cos(AROT), math.sin(AROT)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (AW / 2 - 0.006), sy * (AD / 2 - 0.006)
        box(f"lodge_post_{sx}_{sy}", 0.024, 0.024, AH,
            AX + dx * acs - dy * asn, AY + dx * asn + dy * acs, Z + AH / 2, M_WOOD, rot_z=AROT)
pyramid("lodge_eave", AW * 0.80, AW * 0.44, AW * 0.11, AX, AY, Z + AH + AW * 0.055, M_ROOF, rot_z=AROT)
pyramid("lodge_roof", AW * 0.50, 0.010, AW * 0.24, AX, AY,
        Z + AH + AW * 0.11 + AW * 0.12, M_ROOF, rot_z=AROT)

# ── 추가 건물 2: 어구 오두막 — 사무소 동쪽 ──
BW, BD, BH = 0.15, 0.12, 0.068
BXX, BYY, BROT_S = 0.76, -0.08, math.radians(28)
box("hut_body", BW, BD, BH, BXX, BYY, Z + BH / 2, M_WALL, rot_z=BROT_S)
hcs, hsn = math.cos(BROT_S), math.sin(BROT_S)
for sx in (-1, 1):
    for sy in (-1, 1):
        dx, dy = sx * (BW / 2 - 0.005), sy * (BD / 2 - 0.005)
        box(f"hut_post_{sx}_{sy}", 0.022, 0.022, BH,
            BXX + dx * hcs - dy * hsn, BYY + dx * hsn + dy * hcs, Z + BH / 2, M_WOOD, rot_z=BROT_S)
pyramid("hut_eave", BW * 0.80, BW * 0.44, BW * 0.11, BXX, BYY, Z + BH + BW * 0.055, M_ROOF, rot_z=BROT_S)
pyramid("hut_roof", BW * 0.50, 0.010, BW * 0.24, BXX, BYY,
        Z + BH + BW * 0.11 + BW * 0.12, M_ROOF, rot_z=BROT_S)

# ── 가운데 이음새 구조물(건물 아님): 깃대·짐 깔판·손수레·그물 건조대 ──
# 깃대 + 붉은 기(항구 표식)
cylinder("flag_pole", 0.011, 0.34, 0.06, 0.16, Z + 0.17, M_WOOD, verts=5)
box("flag", 0.012, 0.10, 0.062, 0.06, 0.115, Z + 0.30, M_LANTERN)

# 짐 깔판: 판자 위 가마니(눕힌 원통) 2+1단 + 항아리
box("pallet", 0.17, 0.13, 0.014, -0.08, 0.02, Z + 0.007, M_PLANK)
for i, (dx, dy, dz, rz) in enumerate(((-0.035, 0.0, 0.03, 0.06), (0.035, 0.01, 0.03, -0.08),
                                      (0.0, -0.005, 0.085, 0.02))):
    cylinder(f"sack_{i}", 0.030, 0.115, -0.08 + dx, 0.02 + dy, Z + 0.014 + dz, M_SAIL,
             verts=7, rot=(math.radians(90), 0, rz))
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.030, radius2=0.017, depth=0.062,
                                location=(0.045, 0.05, Z + 0.031))
jar2 = bpy.context.object
jar2.name = "jar2"
jar2.data.materials.append(M_JAR)

# 손수레: 짐칸 + 바퀴 2 + 끌채 2
CTX, CTY, CTR = 0.19, 0.00, math.radians(25)
ccs2, csn2 = math.cos(CTR), math.sin(CTR)
box("cart_bed", 0.075, 0.13, 0.014, CTX, CTY, Z + 0.052, M_PLANK, rot_z=CTR)
box("cart_side_l", 0.010, 0.13, 0.030, CTX - 0.037 * ccs2, CTY - 0.037 * csn2, Z + 0.072, M_PLANK, rot_z=CTR)
box("cart_side_r", 0.010, 0.13, 0.030, CTX + 0.037 * ccs2, CTY + 0.037 * csn2, Z + 0.072, M_PLANK, rot_z=CTR)
for side in (-1, 1):
    cylinder(f"cart_wheel_{side}", 0.036, 0.014, CTX + side * 0.048 * ccs2, CTY + side * 0.048 * csn2,
             Z + 0.036, M_WOOD, verts=8, rot=(0, math.radians(90), CTR))
for side in (-1, 1):
    box(f"cart_handle_{side}", 0.010, 0.11, 0.010, CTX + side * 0.028 * ccs2 - 0.10 * -csn2,
        CTY + side * 0.028 * csn2 - 0.10 * ccs2, Z + 0.062, M_WOOD, rot_z=CTR)

# 그물 건조대: 기둥 2 + 가로대 + 늘어뜨린 그물
NX, NY = -0.26, -0.13
for side in (-1, 1):
    cylinder(f"net_post_{side}", 0.010, 0.13, NX + side * 0.09, NY, Z + 0.065, M_WOOD, verts=5)
cylinder("net_bar", 0.008, 0.19, NX, NY, Z + 0.125, M_WOOD, verts=5, rot=(0, math.radians(90), 0))
box("net", 0.165, 0.006, 0.085, NX, NY + 0.003, Z + 0.078, M_SEAM)

# ── 나무 기중기: 기둥 + 비스듬한 팔 + 밧줄 + 매달린 상자 ──
CX, CY = 0.05, -0.40
cylinder("crane_pole", 0.020, 0.30, CX, CY, Z + 0.15, M_WOOD, verts=6)
box("crane_arm", 0.035, 0.30, 0.030, CX, CY - 0.115, Z + 0.30, M_WOOD, rot_y=0.0, rot_z=0.0)
cylinder("crane_rope", 0.005, 0.16, CX, CY - 0.24, Z + 0.20, M_SEAM, verts=4)
box("crane_crate", 0.050, 0.050, 0.050, CX, CY - 0.24, Z + 0.095, M_CRATE, rot_z=0.4)

# ── 정박선: 서 잔교 옆 정크선(돛대+돛), 동 잔교 옆 나룻배 ──
JX, JY, JROT = -0.72, -0.78, math.radians(4)
jcs, jsn = math.cos(JROT), math.sin(JROT)
box("junk_hull", 0.13, 0.34, 0.050, JX, JY, 0.080, M_BOAT, rot_z=JROT)
box("junk_inner", 0.095, 0.28, 0.040, JX, JY, 0.098, M_JAR, rot_z=JROT)
for endsign, tag in ((1, "bow"), (-1, "stern")):
    box(f"junk_{tag}", 0.090, 0.09, 0.048, JX - endsign * 0.19 * jsn, JY + endsign * 0.19 * jcs,
        0.090, M_BOAT, rot_z=JROT + endsign * math.radians(24))
cylinder("junk_mast", 0.010, 0.30, JX, JY + 0.02, 0.10 + 0.15, M_WOOD, verts=5)
box("junk_sail", 0.012, 0.15, 0.20, JX + 0.02, JY + 0.02, 0.10 + 0.19, M_SAIL, rot_z=math.radians(10))
BX2, BY2, BROT2 = 0.62, -0.76, math.radians(-10)
box("row_hull", 0.075, 0.19, 0.036, BX2, BY2, 0.072, M_BOAT, rot_z=BROT2)
box("row_inner", 0.052, 0.155, 0.028, BX2, BY2, 0.085, M_JAR, rot_z=BROT2)

# ── 짐 더미: 워프 위 상자·통·항아리 ──
box("crate_a1", 0.058, 0.058, 0.058, -0.20, -0.42, Z + 0.062, M_CRATE, rot_z=0.2)
box("crate_a2", 0.046, 0.046, 0.046, -0.20, -0.42, Z + 0.062 + 0.052, M_CRATE, rot_z=0.7)
box("crate_b", 0.052, 0.052, 0.052, 0.24, -0.44, Z + 0.059, M_CRATE, rot_z=1.0)
for i, (px, py) in enumerate(((0.30, -0.40), (-0.30, -0.46), (0.02, -0.30))):
    cylinder(f"barrel_{i}", 0.026, 0.055, px, py, Z + 0.060, M_WOOD, verts=8)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.034, radius2=0.019, depth=0.070,
                                location=(-0.05, -0.30, Z + 0.035))
jar = bpy.context.object
jar.name = "jar"
jar.data.materials.append(M_JAR)

# ── 울타리: 남쪽(물가)과 두 타일 공유 변을 뺀 경계에 나무 말뚝+가로대, 북서 변에 출입구 ──
FENCE_R = 0.485
RAIL_Z = Z + 0.036


def fence_edge(tag, cx, i, t0=0.0, t1=1.0, dz=0.0):
    a1, a2 = math.radians(60 * i + 30), math.radians(60 * (i + 1) + 30)
    x1, y1 = cx + FENCE_R * math.cos(a1), FENCE_R * math.sin(a1)
    x2, y2 = cx + FENCE_R * math.cos(a2), FENCE_R * math.sin(a2)
    sx1, sy1 = x1 + (x2 - x1) * t0, y1 + (y2 - y1) * t0
    sx2, sy2 = x1 + (x2 - x1) * t1, y1 + (y2 - y1) * t1
    mx, my = (sx1 + sx2) / 2, (sy1 + sy2) / 2
    seg = math.hypot(sx2 - sx1, sy2 - sy1)
    ang = math.atan2(sy2 - sy1, sx2 - sx1)
    box(f"fence_rail_{tag}", seg, 0.016, 0.014, mx, my, RAIL_Z + dz, M_PLANK, rot_z=ang)
    for j, ft in enumerate((0.12, 0.5, 0.88)):
        px, py = sx1 + (sx2 - sx1) * ft, sy1 + (sy2 - sy1) * ft
        cylinder(f"fence_post_{tag}_{j}", 0.011, RAIL_Z - Z + 0.010, px, py,
                 Z + (RAIL_Z - Z + 0.010) / 2, M_WOOD, verts=5)


# 변 i의 중점 각도 = 60(i+1)°. 남쪽(240°/300°) = i 3·4 트임, 공유 변: 서 타일 i5(0°)·동 타일 i2(180°)
for i in (0, 1, 2):
    fence_edge(f"w{i}", -0.5, i, dz=i % 3 * 0.0012)
fence_edge("e0", 0.5, 0, dz=0.0012)
fence_edge("e5", 0.5, 5, dz=0.0024)
fence_edge("e1a", 0.5, 1, 0.0, 0.35)   # 북서 변 가운데 출입구
fence_edge("e1b", 0.5, 1, 0.65, 1.0)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
