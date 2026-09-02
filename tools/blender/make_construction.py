# 공사장(건설 중) 저폴리 에셋 → GLB. 모든 시설 공용(건설 진행 중 타일에 얹는다).
# 실행: "D:\Blander\blender.exe" --background --python make_construction.py
#
# 평지/숲 타일 '위'에 얹히므로 육각 기단 없이 z=0부터 위로 쌓는다(타일이 바닥으로 보임).
# 구성: 흙 파임 자국 + 반쯤 쌓은 석재 기초 + 목재 비계(기둥4·상단 프레임·가로 난간·대각 버팀·발판) + 자재 더미.
import bpy
import math

OUT = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models\construction.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True  # Godot z-파이팅 방지(필수)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


# 저채도 톤 보정(inkwash)을 견디도록 채도를 강하게.
M_DIRT = make_mat("c_dirt", (0.44, 0.30, 0.16))
M_WOOD = make_mat("c_wood", (0.60, 0.40, 0.19))
M_PLANK = make_mat("c_plank", (0.74, 0.56, 0.30))
M_STONE = make_mat("c_stone", (0.60, 0.58, 0.55))


def box(name, sx, sy, sz, x, y, z, mat, rot_y=0.0, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, rot_y, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


# ── 흙 파임 자국(공사판 바닥) ──
box("ground", 0.74, 0.74, 0.03, 0, 0, 0.015, M_DIRT)

# ── 반쯤 쌓은 석재 기초(가운데, 짓는 중 느낌) ──
box("footing", 0.36, 0.36, 0.10, 0, 0, 0.075, M_STONE)
box("footing2", 0.24, 0.24, 0.09, 0.03, -0.02, 0.16, M_STONE)  # 한 켜 더(비대칭 = 미완성)

# ── 목재 비계: 기둥 4 ──
POST_H = 0.58
POST_W = 0.05
HALF = 0.30
posts = [(-HALF, -HALF), (HALF, -HALF), (-HALF, HALF), (HALF, HALF)]
for i, (px, py) in enumerate(posts):
    box(f"post{i}", POST_W, POST_W, POST_H, px, py, POST_H / 2, M_WOOD)

# ── 상단 프레임(네 변) ──
BEAM_Z = POST_H - 0.03
box("beam_n", HALF * 2 + POST_W, POST_W, POST_W, 0, HALF, BEAM_Z, M_WOOD)
box("beam_s", HALF * 2 + POST_W, POST_W, POST_W, 0, -HALF, BEAM_Z, M_WOOD)
box("beam_e", POST_W, HALF * 2 + POST_W, POST_W, HALF, 0, BEAM_Z, M_WOOD)
box("beam_w", POST_W, HALF * 2 + POST_W, POST_W, -HALF, 0, BEAM_Z, M_WOOD)

# ── 가로 난간(중간 높이, 앞·옆 두 변) ──
RAIL_Z = 0.30
box("rail_s", HALF * 2, 0.035, 0.035, 0, -HALF, RAIL_Z, M_WOOD)
box("rail_e", 0.035, HALF * 2, 0.035, HALF, 0, RAIL_Z, M_WOOD)

# ── 대각 버팀목(한 변) ──
box("brace", 0.045, 0.045, POST_H * 0.92, -HALF, -HALF + 0.30, POST_H / 2, M_WOOD, rot_y=math.radians(34))

# ── 발판(비계 위 널) ──
box("deck", 0.60, 0.22, 0.03, 0, 0.02, 0.34, M_PLANK)

# ── 자재 더미(옆에 쌓인 널빤지 3장) ──
for i in range(3):
    box(f"stack{i}", 0.30, 0.10, 0.028, 0.30, -0.02, 0.03 + i * 0.03, M_PLANK,
        rot_z=math.radians(12 * (i - 1)))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
