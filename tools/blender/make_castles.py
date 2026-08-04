# 성 3종(작은성/중간성/큰성, 동양풍 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_castles.py
#
# 컨셉(사용자 정의, doc/design-terrain.md):
# - 작은성: 성벽 + 가운데 2단 동양풍 건물 1개. 건물-성벽 여백이 잘 보여야 함
# - 중간성: 성벽 + 가운데 3단 건물 1개 + 12시/4시/8시에 1단 건물. 여백 유지
# - 큰성:   성벽 + 가운데 4단 건물 1개 + 나머지 영역에 3단 1개/2단 1개/1단 2개. 여백 유지
import bpy
import math

OUT_DIR = r"D:\dev\window\slg\SanguoSLG.Game\assets\models"


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


def box(name, sx, sy, sz, x, y, z, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(mat)
    return o


def pyramid(name, r_bottom, r_top, h, x, y, z, mat):
    # 사각 지붕(45° 정렬로 벽과 면 맞춤). 모서리를 덮으려면 r ≥ 벽폭.
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r_bottom, radius2=r_top, depth=h,
        location=(x, y, z), rotation=(0, 0, math.radians(45)))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def tower(mats, prefix, cx, cy, base_z, tiers, base_w):
    """N단 동양풍 탑: 단마다 흙벽+모서리 기둥+처마 링, 꼭대기는 겹지붕+용마루."""
    m_roof, m_wall, m_wood = mats
    z = base_z
    w = base_w
    for t in range(tiers):
        th = 0.24 if t == 0 else 0.19
        box(f"{prefix}_t{t}", w, w, th, cx, cy, z + th / 2, m_wall)
        for sx in (-1, 1):
            for sy in (-1, 1):
                box(f"{prefix}_p{t}_{sx}_{sy}", 0.045, 0.045, th,
                    cx + sx * (w / 2 - 0.02), cy + sy * (w / 2 - 0.02), z + th / 2, m_wood)
        z += th
        if t < tiers - 1:
            pyramid(f"{prefix}_eave{t}", w * 1.22, w * 0.66, 0.055, cx, cy, z + 0.0275, m_roof)
            z += 0.055
            w *= 0.80
        else:
            pyramid(f"{prefix}_topeave", w * 1.25, w * 0.72, 0.055, cx, cy, z + 0.0275, m_roof)
            top_h = 0.13 + 0.02 * tiers
            pyramid(f"{prefix}_roof", w * 0.92, 0.02, top_h, cx, cy, z + 0.055 + top_h / 2, m_roof)
            box(f"{prefix}_fin", 0.04, 0.04, 0.06, cx, cy, z + 0.055 + top_h + 0.03, m_wood)


def clock_pos(hour, radius):
    """시계 방향 위치(12시=+Y=북, 성문은 -Y=남)."""
    theta = math.radians(hour * 30.0)
    return math.sin(theta) * radius, math.cos(theta) * radius


def build_castle(filename, half, center_tiers, subs):
    """subs: [(시계시각, 단수, 벽폭)]"""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_roof = make_mat("roof", (0.06, 0.09, 0.14))
    m_wall = make_mat("wall", (0.55, 0.40, 0.22))
    m_wood = make_mat("wood", (0.42, 0.18, 0.12))
    m_stone = make_mat("stone", (0.52, 0.52, 0.50))
    mats = (m_roof, m_wall, m_wood)

    # 석축 기단 + 마당(여백이 보이는 바닥)
    size = half * 2 + 0.10
    box("base", size, size, 0.14, 0, 0, 0.07, m_stone)

    # 성벽(흙담) — 남벽은 성문 자리를 비움
    wall_h, wall_t = 0.24, 0.09
    wz = 0.14 + wall_h / 2
    box("wall_n", half * 2, wall_t, wall_h, 0, half, wz, m_wall)
    box("wall_e", wall_t, half * 2, wall_h, half, 0, wz, m_wall)
    box("wall_w", wall_t, half * 2, wall_h, -half, 0, wz, m_wall)
    seg = half - 0.22
    box("wall_s1", seg, wall_t, wall_h, -(0.22 + seg / 2), -half, wz, m_wall)
    box("wall_s2", seg, wall_t, wall_h, (0.22 + seg / 2), -half, wz, m_wall)

    # 여장(성가퀴)
    mz = 0.14 + wall_h + 0.03
    count = int((half * 2) / 0.26)
    for i in range(count):
        t = -half + 0.13 + i * 0.26
        box(f"m_n{i}", 0.09, wall_t * 0.8, 0.06, t, half, mz, m_wall)
        box(f"m_e{i}", wall_t * 0.8, 0.09, 0.06, half, t, mz, m_wall)
        box(f"m_w{i}", wall_t * 0.8, 0.09, 0.06, -half, t, mz, m_wall)

    # 남문 문루
    box("gate", 0.30, 0.14, 0.28, 0, -half, 0.14 + 0.14, m_wood)
    pyramid("gate_roof", 0.34, 0.03, 0.13, 0, -half, 0.14 + 0.28 + 0.065, m_roof)

    # 중앙 건물(단수는 컨셉 정의)
    tower(mats, "center", 0, 0.02, 0.14, center_tiers, 0.36)

    # 부속 건물(시계 방향 배치)
    for hour, tiers, w in subs:
        x, y = clock_pos(hour, half * 0.60)
        tower(mats, f"sub{hour}", x, y, 0.14, tiers, w)

    out = OUT_DIR + "\\" + filename
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True)
    print("EXPORTED:", out)


# 작은성: 중앙 2단, 부속 없음 — 마당 여백 강조
build_castle("castle-small.glb", half=0.65, center_tiers=2, subs=[])

# 중간성: 중앙 3단 + 12/4/8시 1단
build_castle("castle-medium.glb", half=0.72, center_tiers=3,
             subs=[(12, 1, 0.20), (4, 1, 0.20), (8, 1, 0.20)])

# 큰성: 중앙 4단 + 12시 3단, 4시 2단, 8시 1단, 2시 1단
build_castle("castle-large.glb", half=0.80, center_tiers=4,
             subs=[(12, 3, 0.24), (4, 2, 0.22), (8, 1, 0.20), (2, 1, 0.20)])
