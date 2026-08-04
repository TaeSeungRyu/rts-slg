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

# 발자국 스케일: 모델을 월드 실치수로 낸다(게임 런타임 스케일 없음).
# 헥사 이웃 간격 = sqrt(3) ≈ 1.732 월드 단위. XY는 발자국 폭, Z는 완만하게.
K_XY = 1.0
K_Z = 1.0


def make_mat(name, color, roughness=0.85, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


def box(name, sx, sy, sz, x, y, z, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x * K_XY, y * K_XY, z * K_Z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx * K_XY, sy * K_XY, sz * K_Z)
    o.data.materials.append(mat)
    return o


def pyramid(name, r_bottom, r_top, h, x, y, z, mat):
    # 사각 지붕(45° 정렬로 벽과 면 맞춤). 모서리를 덮으려면 r ≥ 벽폭.
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r_bottom * K_XY, radius2=r_top * K_XY, depth=h * K_Z,
        location=(x * K_XY, y * K_XY, z * K_Z), rotation=(0, 0, math.radians(45)))
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


def build_castle(filename, half, center_tiers, subs, gates=("S",), k_xy=1.0, k_z=1.0):
    """subs: [(시계시각, 단수, 벽폭)], gates: 성문 방향, k_xy/k_z: 발자국 스케일."""
    global K_XY, K_Z
    K_XY, K_Z = k_xy, k_z
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_roof = make_mat("roof", (0.06, 0.09, 0.14))
    m_wall = make_mat("wall", (0.55, 0.40, 0.22))
    m_wood = make_mat("wood", (0.42, 0.18, 0.12))
    m_stone = make_mat("stone", (0.52, 0.52, 0.50))
    mats = (m_roof, m_wall, m_wood)

    # 석축 기단 + 마당(여백이 보이는 바닥)
    size = half * 2 + 0.10
    box("base", size, size, 0.14, 0, 0, 0.07, m_stone)

    # 성벽(흙담) — gates에 지정된 방향은 성문 자리를 비우고 문루를 세운다
    wall_h, wall_t = 0.24, 0.09
    wz = 0.14 + wall_h / 2
    mz = 0.14 + wall_h + 0.03
    count = int((half * 2) / 0.26)
    directions = {"N": (0, 1), "S": (0, -1), "E": (1, 0), "W": (-1, 0)}

    for side, (dx, dy) in directions.items():
        along_x = dy != 0  # 남/북 벽은 x축 방향으로 길다
        cx, cy = dx * half, dy * half

        if side in gates:
            # 벽 두 조각(가운데 성문 개구부) + 문루(작은 기와)
            seg = half - 0.22
            off = 0.22 + seg / 2
            if along_x:
                box(f"wall_{side}1", seg, wall_t, wall_h, -off, cy, wz, m_wall)
                box(f"wall_{side}2", seg, wall_t, wall_h, off, cy, wz, m_wall)
                box(f"gate_{side}", 0.30, 0.14, 0.28, 0, cy, 0.14 + 0.14, m_wood)
            else:
                box(f"wall_{side}1", wall_t, seg, wall_h, cx, -off, wz, m_wall)
                box(f"wall_{side}2", wall_t, seg, wall_h, cx, off, wz, m_wall)
                box(f"gate_{side}", 0.14, 0.30, 0.28, cx, 0, 0.14 + 0.14, m_wood)
            pyramid(f"gate_roof_{side}", 0.24, 0.03, 0.09, cx, cy, 0.14 + 0.28 + 0.045, m_roof)
        else:
            if along_x:
                box(f"wall_{side}", half * 2, wall_t, wall_h, 0, cy, wz, m_wall)
            else:
                box(f"wall_{side}", wall_t, half * 2, wall_h, cx, 0, wz, m_wall)

        # 여장(성가퀴) — 성문 근처는 비움
        for i in range(count):
            t = -half + 0.13 + i * 0.26
            if side in gates and abs(t) < 0.30:
                continue
            if along_x:
                box(f"m_{side}{i}", 0.09, wall_t * 0.8, 0.06, t, cy, mz, m_wall)
            else:
                box(f"m_{side}{i}", wall_t * 0.8, 0.09, 0.06, cx, t, mz, m_wall)

    # 중앙 건물(단수는 컨셉 정의)
    tower(mats, "center", 0, 0.02, 0.14, center_tiers, 0.36)

    # 부속 건물(시계 방향 배치)
    for hour, tiers, w in subs:
        x, y = clock_pos(hour, half * 0.52)
        tower(mats, f"sub{hour}", x, y, 0.14, tiers, w)

    out = OUT_DIR + "\\" + filename
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True)
    print("EXPORTED:", out)


# 발자국 실치수 (헥사 이웃 간격 1.732):
# 작은성 1타일(폭 ~1.1), 중간성 3타일 삼각(~2.2), 큰성 5타일 꽃잎(~2.9)

# 작은성: 중앙 2단, 부속 없음, 남문 — 마당 여백 강조
build_castle("castle-small.glb", half=0.65, center_tiers=2, subs=[], gates=("S",),
             k_xy=0.80, k_z=0.85)

# 중간성: 중앙 3단 + 12/4/8시 1단, 성문 앞뒤(남·북)
build_castle("castle-medium.glb", half=0.72, center_tiers=3,
             subs=[(12, 1, 0.20), (4, 1, 0.20), (8, 1, 0.20)], gates=("S", "N"),
             k_xy=1.25, k_z=1.05)

# 큰성: 중앙 4단 + 12시 3단, 4시 2단, 8시 1단, 2시 1단, 성문 4방
build_castle("castle-large.glb", half=0.80, center_tiers=4,
             subs=[(12, 3, 0.24), (4, 2, 0.22), (8, 1, 0.20), (2, 1, 0.20)],
             gates=("S", "N", "E", "W"),
             k_xy=1.44, k_z=1.12)
