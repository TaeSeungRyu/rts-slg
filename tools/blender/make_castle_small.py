# 작은성(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_castle_small.py
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\castle-small.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


M_ROOF = make_mat("roof", (0.13, 0.19, 0.28))    # 짙은 청기와
M_WALL = make_mat("wall", (0.88, 0.83, 0.72))    # 회벽(토담)
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))    # 붉은 목재
M_STONE = make_mat("stone", (0.52, 0.52, 0.50))  # 석축


def box(name, sx, sy, sz, x, y, z, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def pyramid(name, r_bottom, r_top, h, x, y, z, mat):
    # 사각 지붕(정렬 45°로 벽과 면 맞춤)
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r_bottom, radius2=r_top, depth=h,
        location=(x, y, z), rotation=(0, 0, math.radians(45)))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 석축 기단 ──
box("base", 1.40, 1.40, 0.14, 0, 0, 0.07, M_STONE)

# ── 성벽(회벽) + 여장 ──
WALL_H = 0.26
WALL_T = 0.10
HALF = 0.65
box("wall_n", HALF * 2, WALL_T, WALL_H, 0, HALF, 0.14 + WALL_H / 2, M_WALL)
box("wall_e", WALL_T, HALF * 2, WALL_H, HALF, 0, 0.14 + WALL_H / 2, M_WALL)
box("wall_w", WALL_T, HALF * 2, WALL_H, -HALF, 0, 0.14 + WALL_H / 2, M_WALL)
# 남벽은 문 자리를 비워 두 조각
box("wall_s1", 0.40, WALL_T, WALL_H, -0.45, -HALF, 0.14 + WALL_H / 2, M_WALL)
box("wall_s2", 0.40, WALL_T, WALL_H, 0.45, -HALF, 0.14 + WALL_H / 2, M_WALL)

# 여장(성가퀴): 벽 위 작은 이빨
merlon_z = 0.14 + WALL_H + 0.035
for i in range(5):
    t = -HALF + 0.13 + i * 0.26
    box(f"merlon_n{i}", 0.10, WALL_T * 0.8, 0.07, t, HALF, merlon_z, M_WALL)
    box(f"merlon_e{i}", WALL_T * 0.8, 0.10, 0.07, HALF, t, merlon_z, M_WALL)
    box(f"merlon_w{i}", WALL_T * 0.8, 0.10, 0.07, -HALF, t, merlon_z, M_WALL)

# ── 정문 문루(남쪽) ──
box("gate_frame", 0.34, 0.16, 0.30, 0, -HALF, 0.14 + 0.15, M_WOOD)
pyramid("gate_roof", 0.30, 0.05, 0.14, 0, -HALF, 0.14 + 0.30 + 0.07, M_ROOF)

# ── 중앙 누각 1층 ──
T1_H = 0.34
box("keep1", 0.60, 0.60, T1_H, 0, 0.05, 0.14 + T1_H / 2, M_WALL)
# 모서리 붉은 기둥
for sx in (-1, 1):
    for sy in (-1, 1):
        box(f"post1_{sx}_{sy}", 0.06, 0.06, T1_H, sx * 0.30, 0.05 + sy * 0.30, 0.14 + T1_H / 2, M_WOOD)
# 넓은 처마 지붕(1층)
pyramid("roof1", 0.55, 0.30, 0.12, 0, 0.05, 0.14 + T1_H + 0.06, M_ROOF)

# ── 중앙 누각 2층 ──
T2_H = 0.24
z2 = 0.14 + T1_H + 0.12
box("keep2", 0.38, 0.38, T2_H, 0, 0.05, z2 + T2_H / 2, M_WALL)
for sx in (-1, 1):
    for sy in (-1, 1):
        box(f"post2_{sx}_{sy}", 0.05, 0.05, T2_H, sx * 0.19, 0.05 + sy * 0.19, z2 + T2_H / 2, M_WOOD)
# 꼭대기 지붕(2층) + 용마루 장식
pyramid("roof2", 0.40, 0.03, 0.18, 0, 0.05, z2 + T2_H + 0.09, M_ROOF)
box("finial", 0.05, 0.05, 0.07, 0, 0.05, z2 + T2_H + 0.18 + 0.035, M_WOOD)

# ── GLB 익스포트 ──
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
