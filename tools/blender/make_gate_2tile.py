# 삼국지풍 저폴리 2칸 대형 관문 생성 → GLB 익스포트
# 실행:
# blender --background --python make_gate_2tile.py
#
# 컨셉:
# - 육각 타일 2칸을 좌우로 이어 사용
# - 중앙 대형 문루 + 좌우 대형 망루 + 연결 성벽
# - 기존 1칸 관문/성 에셋과 동일한 재질 및 스케일 규칙
# - GLB 출력

import bpy
import math
import os

OUT_DIR = r"D:\LOCAL-WORK-STATION\rts-slg\SanguoSLG.Game\assets\models"
OUT_FILE = "gate-2tile.glb"

HEX_R = 1.0
APOTHEM = math.sqrt(3.0) / 2.0
PLATFORM_H = 0.12

K_XY = 0.5774
K_Z = 0.72

PLATFORM_R = HEX_R * 0.995
POST_RISE = 0.006


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


def pyramid(name, r_bottom, r_top, h, x, y, z, mat, rot_z=math.radians(45)):
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


def watch_tower(prefix, cx, cy, base_z, mats, scale=1.0):
    m_roof, m_wall, m_wood = mats

    body_w = 0.44 * scale
    body_h = 0.46 * scale

    box(prefix + "_body", body_w, body_w, body_h,
        cx, cy, base_z + body_h / 2, m_wall)

    for sx in (-1, 1):
        for sy in (-1, 1):
            box(
                f"{prefix}_post_{sx}_{sy}",
                0.05 * scale, 0.05 * scale, body_h + POST_RISE,
                cx + sx * (body_w / 2 - 0.025),
                cy + sy * (body_w / 2 - 0.025),
                base_z + (body_h + POST_RISE) / 2,
                m_wood,
            )

    roof_z = base_z + body_h

    pyramid(prefix + "_eave",
            body_w * 1.34, body_w * 0.73, 0.07 * scale,
            cx, cy, roof_z + 0.035 * scale, m_roof)

    pyramid(prefix + "_roof",
            body_w * 0.98, 0.02, 0.19 * scale,
            cx, cy, roof_z + 0.07 * scale + 0.095 * scale, m_roof)

    box(prefix + "_finial",
        0.04 * scale, 0.04 * scale, 0.06 * scale,
        cx, cy,
        roof_z + 0.07 * scale + 0.19 * scale + 0.03 * scale,
        m_wood)


def gate_house(prefix, cx, cy, base_z, mats):
    m_roof, m_wall, m_wood = mats

    pillar_w = 0.22
    pillar_d = 0.28
    pillar_h = 0.52
    gap = 0.34

    for side in (-1, 1):
        x = cx + side * (gap / 2 + pillar_w / 2)

        box(f"{prefix}_pillar_{side}",
            pillar_w, pillar_d, pillar_h,
            x, cy, base_z + pillar_h / 2, m_wall)

        box(f"{prefix}_wood_post_{side}",
            0.05, pillar_d * 0.88, pillar_h + POST_RISE,
            x, cy, base_z + (pillar_h + POST_RISE) / 2, m_wood)

    # 중앙 문짝
    door_h = 0.36
    door_w = gap / 2
    door_d = 0.06

    for side in (-1, 1):
        x = cx + side * door_w / 2
        box(
            f"{prefix}_door_{side}",
            door_w * 0.92, door_d, door_h,
            x, cy - 0.025, base_z + door_h / 2, m_wood
        )

    # 대형 상부 문루
    upper_h = 0.28
    upper_w = 0.90
    upper_d = 0.38
    upper_z = base_z + pillar_h

    box(prefix + "_upper",
        upper_w, upper_d, upper_h,
        cx, cy, upper_z + upper_h / 2, m_wall)

    for sx in (-1, 1):
        for sy in (-1, 1):
            box(
                f"{prefix}_upper_post_{sx}_{sy}",
                0.045, 0.045, upper_h + POST_RISE,
                cx + sx * (upper_w / 2 - 0.04),
                cy + sy * (upper_d / 2 - 0.04),
                upper_z + (upper_h + POST_RISE) / 2,
                m_wood,
            )

    roof_z = upper_z + upper_h

    pyramid(prefix + "_eave",
            0.62, 0.34, 0.075,
            cx, cy, roof_z + 0.0375, m_roof)

    pyramid(prefix + "_roof",
            0.46, 0.02, 0.22,
            cx, cy, roof_z + 0.075 + 0.11, m_roof)

    box(prefix + "_finial",
        0.045, 0.045, 0.07,
        cx, cy, roof_z + 0.075 + 0.22 + 0.035, m_wood)


def connector_wall(prefix, x1, x2, cy, base_z, mats):
    _, m_wall, _ = mats

    cx = (x1 + x2) / 2
    width = abs(x2 - x1)

    wall_h = 0.31
    wall_t = 0.12

    box(prefix + "_wall",
        width, wall_t, wall_h,
        cx, cy, base_z + wall_h / 2, m_wall)

    merlon_count = max(2, int(width / 0.16))
    if merlon_count == 1:
        xs = [cx]
    else:
        step = width / (merlon_count + 1)
        xs = [min(x1, x2) + step * (i + 1) for i in range(merlon_count)]

    for i, x in enumerate(xs):
        box(
            f"{prefix}_merlon_{i}",
            0.075, wall_t * 0.82, 0.07,
            x, cy, base_z + wall_h + 0.035, m_wall
        )


def road_stones(m_stone, base_z):
    # 타일 두 칸의 중앙을 관통하는 짧은 도로
    for i, y in enumerate((-0.72, -0.52, -0.32, -0.12, 0.12, 0.32, 0.52, 0.72)):
        box(
            f"road_{i}",
            0.32, 0.15, 0.025,
            0.0, y, base_z + 0.0125, m_stone
        )


def build_gate_2tile():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    m_roof = make_mat("roof", (0.06, 0.09, 0.14))
    m_wall = make_mat("wall", (0.55, 0.40, 0.22))
    m_wood = make_mat("wood", (0.42, 0.18, 0.12))
    m_stone = make_mat("stone", (0.52, 0.52, 0.50))
    mats = (m_roof, m_wall, m_wood)

    # 육각 타일 2칸
    # 같은 행에서 좌우로 붙는 두 타일의 중심 간격은 2*APOTHEM
    left_hex_x = -APOTHEM
    right_hex_x = APOTHEM

    hex_platform("platform_left", left_hex_x, 0.0, m_stone)
    hex_platform("platform_right", right_hex_x, 0.0, m_stone)

    base_z = PLATFORM_H

    # 중앙 대형 관문
    gate_house("gate", 0.0, 0.0, base_z, mats)

    # 양끝 망루
    left_tower_x = -1.18
    right_tower_x = 1.18

    watch_tower("tower_left", left_tower_x, 0.0, base_z, mats, scale=1.05)
    watch_tower("tower_right", right_tower_x, 0.0, base_z, mats, scale=1.05)

    # 문루와 망루 사이 성벽
    connector_wall("wall_left", -0.92, -0.46, 0.0, base_z, mats)
    connector_wall("wall_right", 0.46, 0.92, 0.0, base_z, mats)

    # 관문 앞뒤 통로
    road_stones(m_stone, base_z)

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
    build_gate_2tile()
