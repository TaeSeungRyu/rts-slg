# 거대한 얼음벽(1타일, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_ice_wall_large.py
#
# 컨셉: 타일을 동서로 가로지르는 높은 빙벽 — 끝이 뾰족한 얼음 칼날(블레이드)들이
# 서로 다른 각도로 기울어 겹치며 들쑥날쑥한 능선을 만든다.
# (1차 수직 판 일렬 버전은 "아파트 같다"는 피드백으로 교차·불규칙 형태로 재작업, 2026-08-05)
# 얼음산과 같은 한랭 지형군. 이동 불가 예정.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\ice-wall-large.glb"

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


# 잉크워시 톤(채도 0.5)에서 살아남도록 얼음 파랑은 강하게 채도를 준다
M_SNOW = make_mat("snow", (0.90, 0.93, 0.96), roughness=0.7)      # 눈 바닥·눈 얹힘
M_SIDE = make_mat("side", (0.55, 0.55, 0.60), roughness=0.8)      # 언 땅 옆면
M_ICE = make_mat("ice", (0.30, 0.62, 0.92), roughness=0.12)       # 광택 얼음 판
M_ICE2 = make_mat("ice2", (0.20, 0.48, 0.80), roughness=0.35)     # 짙은 얼음 판
M_ICE3 = make_mat("ice3", (0.55, 0.78, 0.95), roughness=0.25)     # 옅은 얼음 판


def slab(name, sx, sy, sz, x, y, z, mat, rot_z=0.0, rot_y=0.0):
    """직육면체 얼음 판. s*는 절반이 아닌 전체 치수."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(x, y, z),
                                    rotation=(0, rot_y, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def cone(name, r1, r2, h, x, y, z, mat, verts=6, rot_z=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 타일 본체(윗면 눈 + 옆면 언 땅) ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2),
    rotation=(0, 0, math.radians(0)))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_SNOW)
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H

def blade(name, x, y, r1, h, sx, sy, mat, rot_z, rot_y):
    """끝이 뾰족한 얼음 칼날 — 4각 뿔대를 납작하게 눌러 블레이드 형태로 만든다."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r1, radius2=r1 * 0.18, depth=h,
        location=(x, y, Z + h / 2), rotation=(0, rot_y, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, 1.0)
    o.data.materials.append(mat)
    return o


# ── 빙벽 본체: 서로 다른 각도로 기울어 겹치는 칼날 6장 — 들쑥날쑥한 능선 ──
# (x, y, 밑반경, 높이, x배율, y배율, 재질, 평면 회전, 옆 기울기)
blades = [
    (-0.40, +0.04, 0.16, 0.40, 1.45, 0.42, M_ICE2, +0.35, +0.14),
    (-0.23, -0.06, 0.18, 0.66, 1.60, 0.38, M_ICE,  -0.30, -0.12),
    (-0.03, +0.03, 0.20, 0.85, 1.50, 0.40, M_ICE3, +0.12, +0.05),
    (+0.10, -0.08, 0.15, 0.52, 1.40, 0.40, M_ICE2, +0.70, -0.20),
    (+0.22, +0.05, 0.17, 0.68, 1.55, 0.38, M_ICE,  -0.45, +0.10),
    (+0.40, -0.03, 0.14, 0.42, 1.45, 0.45, M_ICE3, +0.55, -0.16),
]
for i, (x, y, r1, h, sx, sy, mat, rz, ry) in enumerate(blades):
    blade(f"blade_{i}", x, y, r1, h, sx, sy, mat, rz, ry)

# ── 칼날 사이를 비스듬히 가로지르는 깨진 판 2장(교차감) ──
slab("cross_1", 0.34, 0.09, 0.14, -0.13, 0.02, Z + 0.34, M_ICE, rot_z=+0.9, rot_y=0.55)
slab("cross_2", 0.30, 0.08, 0.12, 0.18, -0.03, Z + 0.28, M_ICE2, rot_z=-1.1, rot_y=-0.50)

# ── 눈: 칼날 어깨에 기울어진 작은 눈덧(평평한 지붕 금지) ──
slab("snow_1", 0.14, 0.10, 0.03, -0.23, -0.05, Z + 0.40, M_SNOW, rot_z=-0.3, rot_y=0.35)
slab("snow_2", 0.15, 0.11, 0.03, -0.02, 0.04, Z + 0.55, M_SNOW, rot_z=0.2, rot_y=-0.30)
slab("snow_3", 0.12, 0.09, 0.025, 0.23, 0.06, Z + 0.42, M_SNOW, rot_z=0.5, rot_y=0.32)

# ── 벽 기슭: 무너진 얼음 조각 + 눈 둔덕 ──
slab("debris_1", 0.10, 0.08, 0.10, -0.30, 0.20, Z + 0.05, M_ICE2, rot_z=0.7)
slab("debris_2", 0.08, 0.07, 0.07, 0.28, -0.22, Z + 0.035, M_ICE, rot_z=1.9)
cone("mound_1", 0.14, 0.05, 0.06, 0.10, 0.27, Z + 0.03, M_SNOW, verts=7)
cone("mound_2", 0.11, 0.04, 0.05, -0.14, -0.27, Z + 0.025, M_SNOW, verts=7)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
