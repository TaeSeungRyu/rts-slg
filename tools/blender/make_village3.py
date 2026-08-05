# 마을 모양 3(중국풍 사합원, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_village3.py
#
# 컨셉(사용자 정의, 2026-08-05): 네모난 모양에 가운데 구멍(중정)이 있는 중국식풍 2단 건물,
# 중정 가운데 우물, 외곽 담은 마을 1·2와 동일(육각 경계 흙담+기와 갓, 남쪽 출입구).
# ㅁ자 링: 남북 날개(전체 폭) + 동서 날개(사이), 중간 처마 띠로 2단 표현 + 날개별 기와지붕.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\village-3.glb"

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
M_YARD = make_mat("yard", (0.52, 0.40, 0.24))
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))
M_WALL = make_mat("wall", (0.55, 0.36, 0.16))   # 벽은 지붕·풀과 대비되게 채도 강화
M_WALL2 = make_mat("wall2", (0.68, 0.50, 0.26))
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))
M_STONE = make_mat("stone", (0.45, 0.45, 0.43))
M_WATER = make_mat("water", (0.20, 0.45, 0.70), roughness=0.15)


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def hip_roof(name, sx, sy, h, x, y, z, mat):
    """길쭉한 모임지붕: 4각 뿔대(회전을 메시에 적용해 축 정렬) → 직사각 비율로 스케일.
    회전된 오브젝트에 비등방 스케일을 주면 마름모로 왜곡되므로 반드시 apply 후 스케일."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=0.5, radius2=0.055, depth=h,
        location=(x, y, z), rotation=(0, 0, math.radians(45)))
    o = bpy.context.object
    o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    o.scale = (sx * 1.42, sy * 1.42, 1.0)
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

# ── ㅁ자 2단 건물: 바깥 사각 0.54, 날개 두께 0.095 — 중정 구멍(0.35)이 잘 보이게 얇게 ──
OW = 0.54      # 바깥 폭
T = 0.095      # 날개 두께
IW = OW - 2 * T  # 중정 폭 0.35
H1 = 0.080     # 1층 높이
H2 = 0.055     # 2층 높이
YIN = (OW - T) / 2  # 날개 중심 오프셋

# 접촉면 z-파이팅 방지: 겹치는 부재는 아래 부재 속으로 EMB만큼 파묻는다(같은 평면 금지)
EMB = 0.004

wings = {
    "n": (0.0, +YIN, OW, T),
    "s": (0.0, -YIN, OW, T),
    "e": (+YIN, 0.0, T, IW),
    "w": (-YIN, 0.0, T, IW),
}
for tag, (cx, cy, sx, sy) in wings.items():
    # 1층 몸체 + 중간 처마 띠(2단 구분) + 2층 몸체(살짝 안쪽) + 기와지붕
    box(f"wing_{tag}_b1", sx, sy, H1, cx, cy, Z + H1 / 2, M_WALL)
    box(f"wing_{tag}_eave", sx + 0.026, sy + 0.026, 0.016, cx, cy, Z + H1 + 0.008 - EMB, M_ROOF)
    box(f"wing_{tag}_b2", sx * 0.94, sy * 0.94, H2, cx, cy, Z + H1 + 0.016 + H2 / 2 - EMB, M_WALL2)
    hip_roof(f"wing_{tag}_roof", sx * 0.64, sy * 0.64, 0.052, cx, cy,
             Z + H1 + 0.016 + H2 + 0.026 - 2 * EMB, M_ROOF)

# 모서리 기둥 4개(바깥 모서리, 1층 높이)
for sx_ in (-1, 1):
    for sy_ in (-1, 1):
        box(f"corner_{sx_}_{sy_}", 0.032, 0.032, H1,
            sx_ * (OW / 2 - 0.016), sy_ * (OW / 2 - 0.016),
            Z + H1 / 2, M_WOOD)

# 남쪽 날개 정문(어두운 문 + 문 위 작은 기와)
box("gate_door", 0.085, 0.02, 0.062, 0.0, -(OW / 2) - 0.002, Z + 0.031, M_ROOF)
box("gate_eave", 0.12, 0.045, 0.014, 0.0, -(OW / 2) - 0.008, Z + 0.075, M_ROOF)

# ── 중정 바닥 + 우물 (바닥·우물은 아래로 파묻어 접촉면 z-파이팅 방지) ──
box("court_yard", IW * 0.96, IW * 0.96, 0.014, 0.0, 0.0, Z + 0.004, M_YARD)
cylinder("well_ring", 0.055, 0.052, 0.0, 0.0, Z + 0.022, M_STONE, verts=8)
cylinder("well_water", 0.038, 0.056, 0.0, 0.0, Z + 0.023, M_WATER, verts=8)

# ── 외곽 담(마을 1·2와 동일): 타일 육각과 같은 방향, 남쪽 꼭짓점 출입구 ──
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
