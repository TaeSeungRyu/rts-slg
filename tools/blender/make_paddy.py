# 논 모양(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_paddy.py
#
# 타일 규격 실측(grass.glb): 육각 반경 0.5774, 윗면 z 0.2, 이웃 간격 1.0.
# 논은 지형 일체형 타일로 제작: 흙 육각 기단 + 물 댄 논(옅은 청록) + 모(벼) 줄 + 흙 둑.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\paddy.glb"

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


# 저채도 톤 보정(inkwash, 채도 0.5)을 견디도록 채도를 강하게 넣는다.
M_DIRT = make_mat("dirt", (0.45, 0.30, 0.15))      # 흙(기단·둑)
M_WATER = make_mat("water", (0.18, 0.50, 0.48), roughness=0.35)  # 논물(청록)
M_RICE = make_mat("rice", (0.15, 0.52, 0.12))      # 모(벼)


def hexagon(name, radius, depth, z_center, mat):
    # 꼭짓점이 남북(±Y)을 향하는 육각(타일과 같은 방향)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=radius, depth=depth,
        location=(0, 0, z_center), rotation=(0, 0, math.radians(0)))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


# ── 흙 육각 기단(타일 본체) ──
hexagon("base", HEX_R, TILE_H, TILE_H / 2, M_DIRT)

# ── 논물(기단 윗면보다 살짝 낮게 파인 느낌: 얇은 수면을 위에 얹음) ──
hexagon("water", HEX_R * 0.90, 0.012, TILE_H + 0.006, M_WATER)

# ── 흙 둑: 바깥 테두리(육각 여섯 변) + 가로 논둑 ──
apothem = HEX_R * math.sqrt(3) / 2
edge_len = HEX_R
for k in range(6):
    theta = math.radians(60 * k)
    ex, ey = apothem * 0.97 * math.cos(theta), apothem * 0.97 * math.sin(theta)
    box(f"bank{k}", edge_len * 1.02, 0.045, 0.035, ex, ey, TILE_H + 0.014, M_DIRT,
        rot_z=theta + math.pi / 2)

# 가로 논둑 2줄(논 구획)
for i, y in enumerate((-0.155, 0.155)):
    half_w = (HEX_R - abs(y) / math.tan(math.radians(60))) * math.sqrt(3) / 2
    box(f"divider{i}", half_w * 2 * 0.94, 0.035, 0.030, 0, y, TILE_H + 0.012, M_DIRT)

# ── 모(벼) 줄: 구획마다 가로 방향 심기 ──
rows = (-0.38, -0.295, -0.22, -0.08, 0.0, 0.08, 0.22, 0.295, 0.38)
for y in rows:
    # 해당 y에서 육각 내부 폭 계산(포인티탑: 폭은 y에 따라 감소)
    half_w = (HEX_R - abs(y) / math.tan(math.radians(60))) * math.sqrt(3) / 2
    usable = half_w * 0.82
    n = max(2, int(usable * 2 / 0.075))
    for i in range(n):
        x = -usable + (2 * usable) * i / (n - 1)
        box(f"rice_{y}_{i}", 0.030, 0.030, 0.045, x, y, TILE_H + 0.030, M_RICE)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
