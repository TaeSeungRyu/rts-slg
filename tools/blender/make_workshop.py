# 공방 모양(동양풍, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_workshop.py
#
# 킷의 공방 후보(smelter/mill)는 중세풍이라 사용 불가(사용자 결정) → 커스텀.
# 타일 일체형(반경 0.5774, 높이 0.2): 흙 마당 + 한옥풍 작업채(넓은 처마) + 가마 + 장작더미.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\workshop.glb"

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


M_YARD = make_mat("yard", (0.50, 0.38, 0.24))    # 흙 마당
M_ROOF = make_mat("roof", (0.06, 0.09, 0.14))    # 짙은 청기와
M_WALL = make_mat("wall", (0.55, 0.40, 0.22))    # 흙벽
M_WOOD = make_mat("wood", (0.42, 0.18, 0.12))    # 붉은 목재
M_STONE = make_mat("stone", (0.45, 0.45, 0.43))  # 가마 돌
M_DARK = make_mat("dark", (0.05, 0.04, 0.04))    # 가마 아궁이
M_LOG = make_mat("log", (0.35, 0.24, 0.14))      # 장작


def box(name, sx, sy, sz, x, y, z, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def pyramid(name, r_bottom, r_top, h, x, y, z, mat):
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r_bottom, radius2=r_top, depth=h,
        location=(x, y, z), rotation=(0, 0, math.radians(45)))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def cylinder(name, r, depth, x, y, z, mat, rot=(0, 0, 0), verts=10):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 흙 마당 육각 기단(타일 본체, 타일과 같은 방향) ──
cylinder("base", HEX_R, TILE_H, 0, 0, TILE_H / 2, M_YARD, verts=6)

Z = TILE_H  # 지면(타일 윗면)

# ── 작업채(한옥풍): 흙벽 + 모서리 기둥 + 겹지붕 ──
HX, HY = -0.10, 0.13   # 북서쪽에 배치
W, H = 0.30, 0.13
box("house", W, W * 0.8, H, HX, HY, Z + H / 2, M_WALL)
for sx in (-1, 1):
    for sy in (-1, 1):
        box(f"post_{sx}_{sy}", 0.035, 0.035, H,
            HX + sx * (W / 2 - 0.015), HY + sy * (W * 0.8 / 2 - 0.015), Z + H / 2, M_WOOD)
pyramid("house_eave", W * 0.78, W * 0.45, 0.035, HX, HY, Z + H + 0.0175, M_ROOF)
pyramid("house_roof", W * 0.52, 0.015, 0.075, HX, HY, Z + H + 0.035 + 0.0375, M_ROOF)

# ── 가마(돌): 몸통 + 이궁이 + 굴뚝 ──
KX, KY = 0.20, -0.10
cylinder("kiln_body", 0.085, 0.085, KX, KY, Z + 0.0425, M_STONE, verts=8)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.085, radius2=0.02, depth=0.06,
                                location=(KX, KY, Z + 0.085 + 0.03))
_kiln_top = bpy.context.object
_kiln_top.name = "kiln_top"
_kiln_top.data.materials.append(M_STONE)
box("kiln_mouth", 0.045, 0.02, 0.045, KX, KY - 0.082, Z + 0.032, M_DARK)
cylinder("kiln_chimney", 0.018, 0.09, KX + 0.045, KY + 0.04, Z + 0.13, M_STONE, verts=6)

# ── 장작더미(눕힌 통나무 3개) ──
LX, LY = -0.16, -0.20
for i, (dx, dz) in enumerate(((0, 0), (0.05, 0), (0.025, 0.045))):
    cylinder(f"log{i}", 0.024, 0.16, LX + dx, LY, Z + 0.024 + dz, M_LOG,
             rot=(math.radians(90), 0, math.radians(15)), verts=7)

# ── 작업대(모루 대용) ──
box("bench", 0.09, 0.05, 0.055, 0.14, 0.16, Z + 0.0275, M_WOOD)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
