# 늪(1타일, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_swamp.py
#
# 컨셉(사용자 정의, 2026-08-05): 갈색 늪 — 탁한 갈색 수면 + 진흙 섬 + 갈대.
# 방울이 피어올랐다 사라지는 효과는 Godot CPUParticles3D(MapView3D.BuildSwampBubbles)가 담당.
# 이동 가능 지형. 타일 일체형(반경 0.5774, 높이 0.2).
import bpy
import math

OUT = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models\swamp.glb"

HEX_R = 0.5774
TILE_H = 0.2

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


# 잉크워시 톤(채도 0.5)에서 살아남도록 원색을 강하게 준다
M_MUD = make_mat("mud", (0.34, 0.24, 0.12))            # 진흙 둔덕·타일 윗면 테
M_SIDE = make_mat("side", (0.28, 0.20, 0.11))          # 타일 옆면
M_WATER = make_mat("swamp_water", (0.42, 0.30, 0.10), roughness=0.30)  # 탁한 갈색 수면
M_REED = make_mat("reed", (0.18, 0.40, 0.10))          # 갈대 줄기
M_REED2 = make_mat("reed2", (0.45, 0.36, 0.14))        # 갈대 이삭(마른 색)
M_WOOD = make_mat("deadwood", (0.25, 0.16, 0.09))      # 고사목 가지


def cone(name, r1, r2, h, x, y, z, mat, verts=7, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def cylinder(name, r, depth, x, y, z, mat, verts=8, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체(윗면 진흙 + 옆면 어두운 진흙) ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_MUD)
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H

# ── 늪 수면: 진흙 테 안쪽의 탁한 갈색 물(육각, 타일과 같은 방향) ──
cylinder("swamp_water", HEX_R * 0.82, 0.014, 0, 0, Z + 0.007, M_WATER, verts=6)

# ── 진흙 섬(낮은 둔덕) 3개 ──
cone("islet_1", 0.13, 0.05, 0.035, -0.20, 0.16, Z + 0.0175, M_MUD)
cone("islet_2", 0.10, 0.04, 0.030, 0.24, -0.06, Z + 0.015, M_MUD)
cone("islet_3", 0.08, 0.03, 0.026, -0.04, -0.26, Z + 0.013, M_MUD)

# ── 갈대 무리: 가는 줄기 + 마른 이삭 ──
reeds = [
    (-0.22, 0.20, 0.11), (-0.16, 0.14, 0.09), (-0.26, 0.11, 0.08),
    (0.27, -0.02, 0.10), (0.21, -0.10, 0.12),
    (-0.02, -0.28, 0.09), (0.05, -0.23, 0.07),
]
for i, (rx, ry, rh) in enumerate(reeds):
    cylinder(f"reed_{i}", 0.008, rh, rx, ry, Z + 0.03 + rh / 2, M_REED, verts=5)
    cone(f"reed_head_{i}", 0.014, 0.004, 0.035, rx, ry, Z + 0.03 + rh + 0.0175, M_REED2, verts=5)

# ── 물에 잠긴 고사목 가지 ──
cylinder("deadwood", 0.016, 0.30, 0.05, 0.18, Z + 0.035, M_WOOD, verts=6,
         rot=(math.radians(75), 0, math.radians(35)))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
