# 삼국지풍 저폴리 관문 생성 → GLB 익스포트
# 실행 예:
# blender --background --python make_gate.py
#
# 기존 make_castles.py 스타일을 따름:
# - 육각 타일 1칸 기준
# - Blender XY 지면, +Y=북
# - K_XY / K_Z 보정 유지
# - 저폴리 박스 + 사각뿔 지붕 조합
# - GLB 출력

import bpy
import math
import os

# ------------------------------------------------------------
# 출력 경로
# 필요하면 본인 프로젝트 경로에 맞게 수정하세요.
# ------------------------------------------------------------
OUT_DIR = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models"
OUT_FILE = "gate.glb"

# ------------------------------------------------------------
# 기존 성 에셋과 동일한 좌표/스케일 규칙
# ------------------------------------------------------------
HEX_R = 1.0
PLATFORM_H = 0.12

K_XY = 0.5774
K_Z = 0.72

PLATFORM_R = HEX_R * 0.995
POST_RISE = 0.006


# ------------------------------------------------------------
# 기본 유틸
# ------------------------------------------------------------
def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True

    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


def box(name, sx, sy, sz, x, y, z, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(x * K_XY, y * K_XY, z * K_Z),
        rotation=(0, 0, rot_z),
    )
    o = bpy.context.object
    o.name = name
    o.scale = (sx * K_XY, sy * K_XY, sz * K_Z)
    o.data.materials.append(mat)
    return o


def pyramid(
    name,
    r_bottom,
    r_top,
    h,
    x,
    y,
    z,
    mat,
    rot_z=math.radians(45),
):
    bpy.ops.mesh.primitive_cone_add(
        vertices=4,
        radius1=r_bottom * K_XY,
        radius2=r_top * K_XY,
        depth=h * K_Z,
        location=(x * K_XY, y * K_XY, z * K_Z),
        rotation=(0, 0, rot_z),
    )
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def hex_platform(name, x, y, mat):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6,
        radius=PLATFORM_R * K_XY,
        depth=PLATFORM_H * K_Z,
        location=(x * K_XY, y * K_XY, PLATFORM_H * K_Z / 2),
        rotation=(0, 0, 0),
    )
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


# ------------------------------------------------------------
# 장식/건축 요소
# ------------------------------------------------------------
def watch_tower(prefix, cx, cy, base_z, mats):
    """
    관문 좌우의 소형 망루.
    흙벽 + 목재 기둥 + 처마 + 지붕.
    """
    m_roof, m_wall, m_wood = mats

    body_w = 0.36
    body_h = 0.38

    box(
        f"{prefix}_body",
        body_w,
        body_w,
        body_h,
        cx,
        cy,
        base_z + body_h / 2,
        m_wall,
    )

    for sx in (-1, 1):
        for sy in (-1, 1):
            box(
                f"{prefix}_post_{sx}_{sy}",
                0.045,
                0.045,
                body_h + POST_RISE,
                cx + sx * (body_w / 2 - 0.02),
                cy + sy * (body_w / 2 - 0.02),
                base_z + (body_h + POST_RISE) / 2,
                m_wood,
            )

    roof_z = base_z + body_h

    pyramid(
        f"{prefix}_eave",
        body_w * 1.30,
        body_w * 0.72,
        0.06,
        cx,
        cy,
        roof_z + 0.03,
        m_roof,
    )

    pyramid(
        f"{prefix}_roof",
        body_w * 0.95,
        0.02,
        0.16,
        cx,
        cy,
        roof_z + 0.06 + 0.08,
        m_roof,
    )

    box(
        f"{prefix}_finial",
        0.035,
        0.035,
        0.055,
        cx,
        cy,
        roof_z + 0.06 + 0.16 + 0.0275,
        m_wood,
    )


def gate_house(prefix, cx, cy, base_z, mats):
    """
    중앙 문루.
    실제 통로는 문짝/기둥 조합으로 표현.
    """
    m_roof, m_wall, m_wood = mats

    # 좌우 문설주
    pillar_w = 0.16
    pillar_d = 0.22
    pillar_h = 0.40
    gap = 0.20

    for side in (-1, 1):
        x = cx + side * (gap / 2 + pillar_w / 2)
        box(
            f"{prefix}_pillar_{side}",
            pillar_w,
            pillar_d,
            pillar_h,
            x,
            cy,
            base_z + pillar_h / 2,
            m_wall,
        )

        box(
            f"{prefix}_wood_post_{side}",
            0.045,
            pillar_d * 0.85,
            pillar_h + POST_RISE,
            x,
            cy,
            base_z + (pillar_h + POST_RISE) / 2,
            m_wood,
        )

    # 문짝 2개
    door_h = 0.27
    door_w = gap / 2
    door_d = 0.055

    for side in (-1, 1):
        x = cx + side * door_w / 2
        box(
            f"{prefix}_door_{side}",
            door_w * 0.92,
            door_d,
            door_h,
            x,
            cy - 0.02,
            base_z + door_h / 2,
            m_wood,
        )

    # 상부 문루 몸체
    upper_h = 0.22
    upper_w = 0.62
    upper_d = 0.30
    upper_z = base_z + pillar_h

    box(
        f"{prefix}_upper",
        upper_w,
        upper_d,
        upper_h,
        cx,
        cy,
        upper_z + upper_h / 2,
        m_wall,
    )

    # 상부 목재 기둥
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(
                f"{prefix}_upper_post_{sx}_{sy}",
                0.04,
                0.04,
                upper_h + POST_RISE,
                cx + sx * (upper_w / 2 - 0.035),
                cy + sy * (upper_d / 2 - 0.035),
                upper_z + (upper_h + POST_RISE) / 2,
                m_wood,
            )

    roof_z = upper_z + upper_h

    # 문루 처마
    pyramid(
        f"{prefix}_eave",
        0.46,
        0.25,
        0.065,
        cx,
        cy,
        roof_z + 0.0325,
        m_roof,
        rot_z=math.radians(45),
    )

    # 문루 지붕
    pyramid(
        f"{prefix}_roof",
        0.34,
        0.02,
        0.17,
        cx,
        cy,
        roof_z + 0.065 + 0.085,
        m_roof,
        rot_z=math.radians(45),
    )

    # 지붕 꼭대기
    box(
        f"{prefix}_finial",
        0.04,
        0.04,
        0.06,
        cx,
        cy,
        roof_z + 0.065 + 0.17 + 0.03,
        m_wood,
    )


def side_wall(prefix, cx, cy, base_z, sx, mats):
    """
    관문 좌우를 타일 끝까지 연결하는 성벽.
    sx=-1: 좌측 / sx=1: 우측
    """
    _, m_wall, _ = mats

    wall_h = 0.27
    wall_t = 0.11
    wall_w = 0.32

    x = cx + sx * 0.64

    box(
        f"{prefix}_wall",
        wall_w,
        wall_t,
        wall_h,
        x,
        cy,
        base_z + wall_h / 2,
        m_wall,
    )

    # 성가퀴
    for dx in (-0.10, 0.0, 0.10):
        box(
            f"{prefix}_merlon_{dx}",
            0.065,
            wall_t * 0.85,
            0.065,
            x + dx,
            cy,
            base_z + wall_h + 0.0325,
            m_wall,
        )


def road_stones(m_stone, base_z):
    """
    관문 앞뒤로 짧은 석재 통로.
    게임 카메라에서 관문 방향이 쉽게 읽히도록 구성.
    """
    for i, y in enumerate((-0.58, -0.38, -0.18, 0.18, 0.38, 0.58)):
        box(
            f"road_{i}",
            0.24,
            0.15,
            0.025,
            0.0,
            y,
            base_z + 0.0125,
            m_stone,
        )


# ------------------------------------------------------------
# 관문 생성
# ------------------------------------------------------------
def build_gate():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # 기존 성과 동일한 색상 계열
    m_roof = make_mat("roof", (0.06, 0.09, 0.14))
    m_wall = make_mat("wall", (0.55, 0.40, 0.22))
    m_wood = make_mat("wood", (0.42, 0.18, 0.12))
    m_stone = make_mat("stone", (0.52, 0.52, 0.50))

    mats = (m_roof, m_wall, m_wood)

    # 육각 기단
    hex_platform("platform", 0.0, 0.0, m_stone)

    base_z = PLATFORM_H

    # 중앙 관문
    gate_house("gate", 0.0, 0.0, base_z, mats)

    # 좌우 망루
    watch_tower("tower_left", -0.44, 0.0, base_z, mats)
    watch_tower("tower_right", 0.44, 0.0, base_z, mats)

    # 외곽 연결 성벽
    side_wall("left", 0.0, 0.0, base_z, -1, mats)
    side_wall("right", 0.0, 0.0, base_z, 1, mats)

    # 통로 표시
    road_stones(m_stone, base_z)

    # --------------------------------------------------------
    # 출력
    # --------------------------------------------------------
    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, OUT_FILE)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(
        filepath=out,
        export_format="GLB",
        use_selection=True,
    )

    print("EXPORTED:", out)


if __name__ == "__main__":
    build_gate()
