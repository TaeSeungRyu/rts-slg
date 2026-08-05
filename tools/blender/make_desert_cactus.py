# 선인장 사막(1타일, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_desert_cactus.py
#
# 컨셉(사용자 정의, 2026-08-05): 사막인데 선인장과 동물 뼈가 있는 지역. 이동 가능.
# 타일 일체형(반경 0.5774, 높이 0.2): 모래 바닥 + 기둥 선인장 2 + 작은 선인장 + 두개골·갈비뼈.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\desert-cactus.glb"

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


# 잉크워시 톤(채도 0.5)에서 살아남도록 원색을 강하게 준다
M_SAND = make_mat("sand", (0.86, 0.60, 0.26))    # 모래 바닥 — 톤에 씻기지 않게 주황기를 강하게
M_SIDE = make_mat("side", (0.66, 0.46, 0.22))    # 타일 옆면
M_CACTUS = make_mat("cactus", (0.16, 0.52, 0.16))  # 선인장 초록
M_BONE = make_mat("bone", (0.94, 0.90, 0.80), roughness=0.6)  # 뼈(마른 흰색)
M_ROCK = make_mat("rock", (0.62, 0.50, 0.34))    # 사막 돌


def cylinder(name, r, depth, x, y, z, mat, verts=7, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def sphere(name, r, x, y, z, mat, seg=8, rings=5):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=seg, ring_count=rings, radius=r, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def box(name, sx, sy, sz, x, y, z, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


# ── 타일 본체(윗면 모래 + 옆면 마른 모래) ──
bpy.ops.mesh.primitive_cylinder_add(
    vertices=6, radius=HEX_R, depth=TILE_H, location=(0, 0, TILE_H / 2))
base = bpy.context.object
base.name = "base"
base.data.materials.append(M_SIDE)
base.data.materials.append(M_SAND)
for poly in base.data.polygons:
    if poly.center.z > TILE_H * 0.49 and abs(poly.normal.z) > 0.5:
        poly.material_index = 1

Z = TILE_H


def saguaro(tag, x, y, h, r, arm_side):
    """기둥 선인장: 본체 + 팔(가로 토막→세로 토막). arm_side=±1."""
    cylinder(f"{tag}_body", r, h, x, y, Z + h / 2, M_CACTUS, verts=7)
    sphere(f"{tag}_top", r * 0.98, x, y, Z + h, M_CACTUS, seg=7, rings=4)
    ax = x + arm_side * (r + 0.028)
    az = Z + h * 0.52
    cylinder(f"{tag}_arm_h", r * 0.55, 0.055, x + arm_side * (r + 0.014), y, az, M_CACTUS,
             verts=6, rot=(0, math.radians(90), 0))
    cylinder(f"{tag}_arm_v", r * 0.55, h * 0.42, ax, y, az + h * 0.21, M_CACTUS, verts=6)
    sphere(f"{tag}_arm_top", r * 0.53, ax, y, az + h * 0.42, M_CACTUS, seg=6, rings=4)


# ── 선인장: 큰 기둥 2개(팔 방향 반대) + 작은 통 선인장 1개 ──
saguaro("cactus_a", -0.18, 0.14, 0.34, 0.045, +1)
saguaro("cactus_b", 0.24, -0.20, 0.24, 0.038, -1)
cylinder("cactus_small", 0.035, 0.07, 0.10, 0.24, Z + 0.035, M_CACTUS, verts=7)
sphere("cactus_small_top", 0.034, 0.10, 0.24, Z + 0.07, M_CACTUS, seg=7, rings=4)

# ── 동물 뼈: 두개골(뿔 달린 소 두개골 느낌) + 갈비뼈 아치 3개 ──
SKX, SKY = 0.02, -0.10
sphere("skull", 0.042, SKX, SKY, Z + 0.035, M_BONE, seg=8, rings=5)
box("skull_snout", 0.045, 0.055, 0.030, SKX, SKY - 0.048, Z + 0.026, M_BONE)
for side in (-1, 1):  # 좌우 뿔
    cylinder(f"horn_{side}", 0.010, 0.075, SKX + side * 0.055, SKY + 0.012, Z + 0.052, M_BONE,
             verts=5, rot=(0, math.radians(70 * side), 0))
for i, dx in enumerate((-0.045, 0.0, 0.045)):  # 모래에 반쯤 묻힌 갈비뼈 아치
    cylinder(f"rib_{i}", 0.007, 0.11, -0.24 + dx, -0.22, Z + 0.028, M_BONE,
             verts=5, rot=(math.radians(12 * (i - 1)), math.radians(28), 0))

# ── 사막 돌 2개 ──
sphere("rock_1", 0.045, 0.28, 0.10, Z + 0.028, M_ROCK, seg=6, rings=4)
sphere("rock_2", 0.032, -0.32, -0.06, Z + 0.020, M_ROCK, seg=6, rings=4)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
