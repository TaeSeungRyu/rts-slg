# 잔해(rubble) 조각 모음 생성 → GLB 익스포트
# 실행: blender --background --python make_rubble.py
#
# 파괴 상태 표현의 공통 부품. 지형·건물마다 따로 만들지 않고 이 모델 하나를
# 여러 번 인스턴스해서 회전·크기를 달리해 흩뿌린다(DamageView.ScatterRubble).
# 원점은 지면(z=0), 반경 ~0.12 안에 들어가도록 작게 만든다.
import bpy
import math

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\rubble.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.95):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return m


# 잔해는 이미 어두운 색이라 톤 보정보다 명도 대비를 우선한다
M_CHAR = make_mat("char", (0.10, 0.08, 0.07))     # 숯이 된 목재
M_PLANK = make_mat("plank", (0.34, 0.22, 0.11))   # 부러진 판자
M_STONE = make_mat("stone", (0.40, 0.38, 0.35))   # 깨진 돌
M_ASH = make_mat("ash", (0.30, 0.28, 0.26))       # 재 무더기


def box(name, sx, sy, sz, x, y, z, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def cone(name, r1, r2, h, x, y, z, mat, verts=6, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=h,
        location=(x, y, z), rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ── 재 무더기(바닥에 깔리는 납작한 원뿔) ──
cone("rubble_ash", 0.085, 0.030, 0.014, 0.0, 0.0, 0.007, M_ASH, verts=7)

# ── 부러진 판자 2장(눕혀서 서로 어긋나게) ──
box("rubble_plank_a", 0.105, 0.026, 0.010, 0.012, -0.010, 0.011, M_PLANK,
    rot=(0, math.radians(4), math.radians(22)))
box("rubble_plank_b", 0.080, 0.022, 0.009, -0.028, 0.026, 0.010, M_PLANK,
    rot=(0, math.radians(-6), math.radians(-40)))

# ── 숯이 된 들보(비스듬히 걸침) ──
box("rubble_beam", 0.120, 0.020, 0.020, -0.006, 0.008, 0.026, M_CHAR,
    rot=(math.radians(9), math.radians(-13), math.radians(66)))

# ── 깨진 돌 2개 ──
cone("rubble_stone_a", 0.032, 0.018, 0.030, 0.046, 0.034, 0.015, M_STONE, verts=6,
     rot=(0, 0, math.radians(30)))
cone("rubble_stone_b", 0.022, 0.012, 0.021, -0.052, -0.030, 0.010, M_STONE, verts=5,
     rot=(math.radians(12), 0, math.radians(70)))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
