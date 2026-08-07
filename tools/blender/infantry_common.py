# 보병 공용 몸통 — doc/spec-unit.md "모델 제작 메모"의 보병 묶음 7종이 공유한다.
# 이 파일은 단독 실행하지 않는다. make_troop_*.py가 import해서 몸통을 세우고 무기만 얹는다.
#
# 프로시저럴 애니메이션을 위해 부위별 노드를 나누고 이름을 붙인다:
#   body (부모) ← 나머지 전부가 자식
#   leg_l / leg_r : 다리(원점=고관절)
#   arm_l / arm_r : 팔(원점=어깨). 무기·방패는 이 둘의 자식으로 붙인다
# 기병과 같은 규약: 정면은 -Y(남쪽) → Godot에서 +Z가 정면.
#
# 크기: 총높이 약 0.27. 기병 기수 머리 높이(0.345)의 0.72배 — 말 탄 사람보다 낮아야 한다.
import bpy
import math
from mathutils import Matrix

MODEL_DIR = r"D:\dev\window\slg\SanguoSLG.Game\assets\models"

# 부위별 기준 높이 — 무기를 얹을 때 참조한다
HIP_Z = 0.078
SHOULDER_Z = 0.170
HAND_Z = 0.104          # 팔 끝(손) 높이
ARM_X = 0.052           # 어깨 좌우 오프셋


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


class Mats:
    """스크립트마다 재질을 다시 만들지 않도록 한 번에 묶어 만든다."""

    def __init__(self):
        self.armor = make_mat("armor", (0.26, 0.25, 0.24))
        self.cloth = make_mat("cloth", (0.34, 0.30, 0.24))
        self.skin = make_mat("skin", (0.80, 0.60, 0.44))
        self.wood = make_mat("wood", (0.35, 0.24, 0.14))
        self.steel = make_mat("steel", (0.66, 0.67, 0.70), metallic=0.6, roughness=0.32)
        # 세력색 전용(계획 3): 런타임에 이 재질의 표면만 색을 바꾼다
        self.red = make_mat("red", (0.62, 0.15, 0.12))


def box(name, sx, sy, sz, x, y, z, mat, rot_x=0.0, rot_y=0.0, rot_z=0.0, origin_shift=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(rot_x, rot_y, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    if origin_shift is not None:
        # 메시를 옮겨 피벗 위치를 바꾼다 — 다리·팔 스윙은 관절 기준 회전이어야 한다.
        # 스케일을 먼저 메시에 구워야 한다. 안 그러면 이동량이 스케일 배수만큼 줄어
        # 피벗이 관절이 아니라 부위 한가운데에 남는다.
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        o.data.transform(Matrix.Translation(origin_shift))
    return o


def cylinder(name, r, depth, x, y, z, mat, verts=8, rot_x=0.0, rot_y=0.0, smooth=False):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=depth, location=(x, y, z), rotation=(rot_x, rot_y, 0))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    if smooth:
        shade_smooth(o)
    return o


def cone(name, r_bottom, r_top, depth, x, y, z, mat, verts=8, rot_x=0.0, smooth=False):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r_bottom, radius2=r_top, depth=depth,
        location=(x, y, z), rotation=(rot_x, 0, 0))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    if smooth:
        shade_smooth(o)
    return o


def shade_smooth(o):
    """계획 2(반곡선): 몸통·엉덩이·목만 스무스. 갑옷·무기는 각지게 둔다."""
    for poly in o.data.polygons:
        poly.use_smooth = True


def bake_scale(o):
    """스케일을 메시에 굽는다. 런타임에 회전하는 자식을 거느릴 부모는 반드시 거쳐야 한다.
    비등방 스케일이 노드에 남은 채 자식이 돌면 전단 변형으로 일그러진다."""
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def parent_to(child, parent):
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def build_body(m, arm_l_pitch=0.0, arm_r_pitch=0.0):
    """보병 공용 몸통을 세우고 (body, arm_l, arm_r)을 돌려준다.

    무기는 호출한 쪽에서 arm_l / arm_r의 자식으로 붙인다.
    arm_*_pitch: 어깨 기준 팔 회전(라디안). 무기 자세에 맞춰 조정한다.
    """
    # 몸통(부모) — 허리에서 어깨로 갈수록 넓어진다
    body = cone("body", 0.036, 0.045, 0.068, 0, 0, 0.138, m.armor, smooth=True)

    # 갑옷 치마(엉덩이)
    skirt = cone("skirt", 0.053, 0.040, 0.040, 0, 0, 0.088, m.cloth, smooth=True)

    # 목·머리·투구·투구술
    neck = cylinder("neck", 0.016, 0.020, 0, 0, 0.178, m.skin, smooth=True)
    head = box("head", 0.048, 0.046, 0.042, 0, 0, 0.207, m.skin)
    helmet = cone("helmet", 0.033, 0.013, 0.026, 0, 0, 0.238, m.armor, verts=6)
    plume = box("plume", 0.010, 0.010, 0.026, 0, 0, 0.262, m.red)

    for part in (skirt, neck, head, helmet, plume):
        parent_to(part, body)

    # 다리 2개 — 피벗을 고관절(위쪽)로 옮겨 스윙 가능
    for tag, lx in (("l", -0.024), ("r", 0.024)):
        leg = box(f"leg_{tag}", 0.028, 0.030, HIP_Z, lx, 0, HIP_Z, m.cloth,
                  origin_shift=(0, 0, -HIP_Z / 2))
        foot = box(f"foot_{tag}", 0.030, 0.050, 0.016, lx, -0.010, 0.008, m.armor)
        parent_to(foot, leg)
        parent_to(leg, body)

    # 팔 2개 — 피벗을 어깨로 옮긴다. 무기는 이 노드의 자식이 된다
    arms = {}
    for tag, ax, pitch in (("l", -ARM_X, arm_l_pitch), ("r", ARM_X, arm_r_pitch)):
        arm = box(f"arm_{tag}", 0.024, 0.026, 0.066, ax, 0, SHOULDER_Z, m.armor,
                  rot_x=pitch, origin_shift=(0, 0, -0.033))
        parent_to(arm, body)
        arms[tag] = arm

    return body, arms["l"], arms["r"]


def build_siege_crew(m, parent, i, sx, s=0.8):
    """공성 병기를 끄는 병사 1명(수레 옆, 난간을 쥔다). s: 병사 축소 비율 —
    병기가 커 보이도록 사람을 20% 줄인 0.8이 기본이다."""
    cx = sx * 0.082
    cy = -0.040
    torso = cone(f"crew{i}_torso", 0.030 * s, 0.038 * s, 0.058 * s, cx, cy, 0.133 * s,
                 m.armor, smooth=True)
    head = box(f"crew{i}_head", 0.040 * s, 0.038 * s, 0.036 * s, cx, cy, 0.184 * s, m.skin)
    helmet = cone(f"crew{i}_helmet", 0.027 * s, 0.010 * s, 0.022 * s, cx, cy, 0.210 * s,
                  m.armor, verts=6)
    hip = HIP_Z * s
    for tag, lx in (("l", -0.020 * s), ("r", 0.020 * s)):
        leg = box(f"crew{i}_leg_{tag}", 0.024 * s, 0.026 * s, hip, cx + lx, cy, hip,
                  m.cloth, origin_shift=(0, 0, -hip / 2))
        foot = box(f"crew{i}_foot_{tag}", 0.026 * s, 0.044 * s, 0.014 * s,
                   cx + lx, cy - 0.008 * s, 0.007 * s, m.armor)
        parent_to(foot, leg)
        parent_to(leg, torso)
    # 어깨는 몸통 원뿔 반경 바깥에 둬야 팔이 몸에 묻히지 않는다
    arm_in = box(f"crew{i}_arm_in", 0.018 * s, 0.020 * s, 0.058 * s,
                 cx - sx * 0.042 * s, cy + 0.014 * s, 0.132 * s, m.armor,
                 rot_x=math.radians(-48))
    arm_out = box(f"crew{i}_arm_out", 0.018 * s, 0.020 * s, 0.056 * s,
                  cx + sx * 0.042 * s, cy + 0.004 * s, 0.130 * s, m.armor,
                  rot_x=math.radians(-12))
    for part in (head, helmet, arm_in, arm_out):
        parent_to(part, torso)
    parent_to(torso, parent)


def export(filename):
    out = MODEL_DIR + "\\" + filename
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True)
    print("EXPORTED:", out)
