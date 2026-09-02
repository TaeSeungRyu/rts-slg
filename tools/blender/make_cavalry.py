# 기병대(3기 묶음, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_cavalry.py
#
# 프로시저럴 애니메이션을 위해 부위별로 노드를 나누고 이름을 붙인다:
#   u{i}_body (부모) ← 목/머리/꼬리/다리4/기수/창 이 모두 자식
#   u{i}_leg_fl/fr/bl/br : 다리(원점=고관절, 회전 스윙용)
#   u{i}_spear : 창(찌르기용)
# 말은 -Y(남쪽)를 향한다 → Godot에서 +Z가 정면.
import bpy
import math
from mathutils import Matrix

OUT = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models\cavalry.glb"

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


M_HORSE1 = make_mat("horse1", (0.33, 0.20, 0.11))   # 밤색
M_HORSE2 = make_mat("horse2", (0.20, 0.14, 0.10))   # 흑갈색(대장)
M_MANE = make_mat("mane", (0.10, 0.08, 0.07))       # 갈기·꼬리
M_ARMOR = make_mat("armor", (0.24, 0.22, 0.21))     # 기수 갑옷
M_RED = make_mat("red", (0.62, 0.15, 0.12))         # 세력색 포인트
M_WOOD = make_mat("wood", (0.35, 0.24, 0.14))       # 창대
M_TIP = make_mat("tip", (0.65, 0.66, 0.68), metallic=0.6, roughness=0.35)  # 창날


def box(name, sx, sy, sz, x, y, z, mat, rot_x=0.0, rot_y=0.0, origin_shift=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(rot_x, rot_y, 0))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    if origin_shift is not None:
        # 메시를 이동시켜 원점(피벗)을 옮긴다 — 다리 스윙은 고관절 기준 회전이어야 한다.
        o.data.transform(Matrix.Translation(origin_shift))
    return o


def cylinder(name, r, depth, x, y, z, mat, rot_x=0.0):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=r, depth=depth, location=(x, y, z), rotation=(rot_x, 0, 0))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def parent_to(child, parent):
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def horseman(i, ox, oy, yaw, horse_mat):
    """기수 1기: body가 부모, 나머지는 자식. (ox,oy)에 배치, yaw로 미세 회전."""
    # 몸통(부모 노드) — 정면 -Y
    body = box(f"u{i}_body", 0.11, 0.26, 0.10, ox, oy, 0.165, horse_mat)
    body.rotation_euler = (0, 0, yaw)

    # 목 + 머리 + 귀 + 꼬리 (몸통 자식)
    neck = box(f"u{i}_neck", 0.05, 0.055, 0.11, ox, oy - 0.115, 0.235, horse_mat, rot_x=math.radians(-28))
    head = box(f"u{i}_head", 0.045, 0.10, 0.045, ox, oy - 0.165, 0.285, horse_mat, rot_x=math.radians(12))
    mane = box(f"u{i}_mane", 0.02, 0.06, 0.10, ox, oy - 0.10, 0.25, M_MANE, rot_x=math.radians(-28))
    tail = box(f"u{i}_tail", 0.028, 0.032, 0.11, ox, oy + 0.145, 0.135, M_MANE, rot_x=math.radians(35))
    for part in (neck, head, mane, tail):
        parent_to(part, body)

    # 다리 4개 — 원점을 고관절(위쪽)로 이동시켜 회전 스윙 가능
    for tag, lx, ly in (("fl", -0.035, -0.085), ("fr", 0.035, -0.085),
                        ("bl", -0.035, 0.095), ("br", 0.035, 0.095)):
        leg = box(f"u{i}_leg_{tag}", 0.026, 0.026, 0.13, ox + lx, oy + ly, 0.13,
                  horse_mat, origin_shift=(0, 0, -0.065))
        parent_to(leg, body)

    # 기수: 몸통·머리·투구술
    torso = box(f"u{i}_rider", 0.05, 0.045, 0.095, ox, oy + 0.015, 0.275, M_ARMOR)
    rhead = box(f"u{i}_rhead", 0.034, 0.034, 0.034, ox, oy + 0.015, 0.345, M_ARMOR)
    plume = box(f"u{i}_plume", 0.012, 0.012, 0.03, ox, oy + 0.015, 0.375, M_RED)
    for part in (torso, rhead, plume):
        parent_to(part, body)

    # 창(오른손 쪽, 앞으로 비스듬히) — 찌르기 애니메이션 대상
    spear = cylinder(f"u{i}_spear", 0.006, 0.30, ox + 0.05, oy - 0.03, 0.30, M_WOOD,
                     rot_x=math.radians(-55))
    tip = cylinder(f"u{i}_tip", 0.010, 0.035, ox + 0.05, oy - 0.03 - 0.125, 0.30 + 0.085, M_TIP,
                   rot_x=math.radians(-55))
    parent_to(tip, spear)
    parent_to(spear, body)


# ── 기병 3기(선두 1 + 후열 2) ──
horseman(0, 0.0, -0.05, 0.0, M_HORSE2)     # 대장(흑갈색)
horseman(1, -0.13, 0.12, math.radians(6), M_HORSE1)
horseman(2, 0.13, 0.14, math.radians(-5), M_HORSE1)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)
