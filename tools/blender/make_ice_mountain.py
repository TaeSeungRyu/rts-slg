# 얼음산(1타일, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_ice_mountain.py
#
# 컨셉: 산 전체가 빙설 — 눈 덮인 바닥 + 결정질 얼음 첨탑(유리광택).
# 큰산(초록 산+눈 정상)과 구분되는 한랭 지형. 이동 불가 예정.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\ice-mountain.glb"

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


M_SNOW = make_mat("snow", (0.90, 0.93, 0.96), roughness=0.7)      # 눈 바닥
M_SIDE = make_mat("side", (0.55, 0.55, 0.60), roughness=0.8)      # 언 땅 옆면
M_ICE = make_mat("ice", (0.55, 0.76, 0.90), roughness=0.12)       # 얼음(광택)
M_ICE2 = make_mat("ice2", (0.36, 0.60, 0.80), roughness=0.15)     # 짙은 얼음


def spire(name, r, h, x, y, mat, rot_z=0.0):
    # 결정질 얼음 첨탑: 각진 5각 콘
    bpy.ops.mesh.primitive_cone_add(
        vertices=5, radius1=r, radius2=0.008, depth=h,
        location=(x, y, TILE_H + h / 2), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def mound(name, r, h, x, y):
    # 눈 둔덕(납작 콘)
    bpy.ops.mesh.primitive_cone_add(
        vertices=7, radius1=r, radius2=r * 0.35, depth=h,
        location=(x, y, TILE_H + h / 2))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(M_SNOW)
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

# ── 얼음 첨탑들(주탑 + 곁탑, 살짝 기울이고 회전 제각각) ──
spire("spire_main", 0.155, 0.72, -0.02, 0.04, M_ICE, rot_z=0.4)
spire("spire_2", 0.115, 0.48, 0.22, -0.14, M_ICE2, rot_z=1.3)
spire("spire_3", 0.095, 0.36, -0.24, -0.18, M_ICE, rot_z=2.1)
spire("spire_4", 0.075, 0.26, 0.10, 0.28, M_ICE2, rot_z=0.9)
spire("shard_1", 0.045, 0.14, -0.32, 0.16, M_ICE2, rot_z=1.7)
spire("shard_2", 0.040, 0.11, 0.34, 0.12, M_ICE, rot_z=0.2)

# ── 눈 둔덕(첨탑 기슭) ──
mound("mound_1", 0.16, 0.07, -0.16, 0.24)
mound("mound_2", 0.13, 0.06, 0.28, -0.30)
mound("mound_3", 0.11, 0.05, -0.34, -0.06)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
