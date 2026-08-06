# 성 3종(작은성/중간성/큰성, 동양풍 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_castles.py
#
# 컨셉(사용자 정의, doc/design-terrain.md):
# - 성은 육각 타일 모양에 정확히 들어간다: 발자국 각 타일에 육각 기단,
#   성벽은 클러스터의 "바깥 경계 모서리"만 따라 두른다(내부 경계는 트임 → 한 덩어리 성)
# - 작은성: 육각 1개, 중앙 2단 건물
# - 중간성: 육각 3개(12시·4시·8시로 붙음), 중앙(공유 꼭짓점) 3단 + 각 타일에 1단
# - 큰성:   육각 5개(위 2, 아래 3), 중앙 4단 + 3단 1·2단 1·1단 2
# - 좌표: Blender XY 지면, +Y=북(12시). glTF 익스포트가 Godot 좌표로 변환.
import bpy
import math

OUT_DIR = r"D:\dev\window\slg\SanguoSLG.Game\assets\models"

HEX_R = 1.0                      # 타일 반경(월드) — 이웃 간격 sqrt(3)
APOTHEM = math.sqrt(3.0) / 2.0   # 변 중심까지 거리
PLATFORM_H = 0.12
WALL_H, WALL_T = 0.24, 0.09

# 실측(grass.glb): 타일 이웃 간격 1.0, 윗면 육각 반경 0.5774(간격/√3), 윗면 z 0.2.
# 스크립트 내부 좌표는 "단위 육각"(간격 1.732, 반경 1.0)으로 유지하고
# 출력 시 XY를 0.5774배(타일 1:1), Z를 0.72배(완만한 높이)로 변환한다.
K_XY = 0.5774
K_Z = 0.72
PLATFORM_R = HEX_R * 0.995

# 기둥 윗면이 몸체 윗면과 같은 평면이면 둘 다 위를 향해 z-파이팅한다(후면 컬링으로 안 없어짐).
# 이만큼 높여 윗면을 위쪽 처마 속에 묻는다.
POST_RISE = 0.006

# 발자국 육각 중심(클러스터 중심 기준, Blender XY, +Y=북)
FOOTPRINTS = {
    "small": [(0.0, 0.0)],
    # 12시, 4시, 8시 — 공유 꼭짓점이 원점
    "medium": [(0.0, 1.0), (APOTHEM, -0.5), (-APOTHEM, -0.5)],
    # 위 2, 아래 3 — 중심이 원점
    "large": [(-APOTHEM, 0.9), (APOTHEM, 0.9),
              (-2 * APOTHEM, -0.6), (0.0, -0.6), (2 * APOTHEM, -0.6)],
}


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
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x * K_XY, y * K_XY, z * K_Z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx * K_XY, sy * K_XY, sz * K_Z)
    o.data.materials.append(mat)
    return o


def pyramid(name, r_bottom, r_top, h, x, y, z, mat, rot_z=math.radians(45)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=4, radius1=r_bottom * K_XY, radius2=r_top * K_XY, depth=h * K_Z,
        location=(x * K_XY, y * K_XY, z * K_Z), rotation=(0, 0, rot_z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


def hex_platform(name, x, y, mat):
    # 꼭짓점이 남북(±Y)을 향하는 육각 기단(타일과 같은 방향·같은 크기)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=PLATFORM_R * K_XY, depth=PLATFORM_H * K_Z,
        location=(x * K_XY, y * K_XY, PLATFORM_H * K_Z / 2), rotation=(0, 0, math.radians(0)))
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
                box(f"{prefix}_p{t}_{sx}_{sy}", 0.045, 0.045, th + POST_RISE,
                    cx + sx * (w / 2 - 0.02), cy + sy * (w / 2 - 0.02), z + (th + POST_RISE) / 2, m_wood)
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


def boundary_edges(centers):
    """클러스터 바깥 경계 모서리 목록: (변 중심 x, y, 바깥 법선 각도)."""
    spacing = 2 * APOTHEM
    edges = []
    for (cx, cy) in centers:
        for k in range(6):
            theta = math.radians(60 * k)
            nx, ny = cx + spacing * math.cos(theta), cy + spacing * math.sin(theta)
            inside = any(abs(nx - ox) < 0.2 and abs(ny - oy) < 0.2 for (ox, oy) in centers)
            if not inside:
                edges.append((cx + APOTHEM * math.cos(theta), cy + APOTHEM * math.sin(theta), theta))
    return edges


def build_castle(filename, kind, buildings, gate_dirs):
    """buildings: [(x, y, 단수, 벽폭)], gate_dirs: 성문 방향 벡터 목록."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_roof = make_mat("roof", (0.06, 0.09, 0.14))
    m_wall = make_mat("wall", (0.55, 0.40, 0.22))
    m_wood = make_mat("wood", (0.42, 0.18, 0.12))
    m_stone = make_mat("stone", (0.52, 0.52, 0.50))
    mats = (m_roof, m_wall, m_wood)

    centers = FOOTPRINTS[kind]
    for i, (x, y) in enumerate(centers):
        hex_platform(f"platform{i}", x, y, m_stone)

    # 성문이 놓일 경계 모서리 선택(방향 벡터와 가장 정렬된 변)
    edges = boundary_edges(centers)
    gate_edges = set()
    for (gx, gy) in gate_dirs:
        best = max(range(len(edges)), key=lambda i: edges[i][0] * gx + edges[i][1] * gy)
        gate_edges.add(best)

    wz = PLATFORM_H + WALL_H / 2
    mz = PLATFORM_H + WALL_H + 0.03
    for i, (ex, ey, theta) in enumerate(edges):
        along = theta + math.pi / 2
        ax, ay = math.cos(along), math.sin(along)
        if i in gate_edges:
            # 성문: 짧은 벽 2조각 + 문루(작은 기와, 변 방향 정렬)
            for s in (-1, 1):
                box(f"wall{i}_{s}", 0.34, WALL_T, WALL_H,
                    ex + s * 0.36 * ax, ey + s * 0.36 * ay, wz, m_wall, rot_z=along)
            box(f"gate{i}", 0.30, 0.14, 0.28, ex, ey, PLATFORM_H + 0.14, m_wood, rot_z=along)
            pyramid(f"gate_roof{i}", 0.24, 0.03, 0.09, ex, ey,
                    PLATFORM_H + 0.28 + 0.045, m_roof, rot_z=along + math.radians(45))
        else:
            box(f"wall{i}", 1.06, WALL_T, WALL_H, ex, ey, wz, m_wall, rot_z=along)
            for t in (-0.33, 0.0, 0.33):
                box(f"merlon{i}_{t}", 0.10, WALL_T * 0.8, 0.06,
                    ex + t * ax, ey + t * ay, mz, m_wall, rot_z=along)

    for j, (bx, by, tiers, w) in enumerate(buildings):
        tower(mats, f"b{j}", bx, by, PLATFORM_H, tiers, w)

    out = OUT_DIR + "\\" + filename
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True)
    print("EXPORTED:", out)


# 작은성: 중앙 2단. 성문은 남동쪽(육각엔 정남 변이 없음)
build_castle("castle-small.glb", "small",
             buildings=[(0.0, 0.0, 2, 0.50)],
             gate_dirs=[(0.3, -1.0)])

# 중간성: 공유 꼭짓점(원점)에 3단 + 각 타일 중심에 1단, 성문 앞뒤
A = APOTHEM
build_castle("castle-medium.glb", "medium",
             buildings=[(0.0, 0.0, 3, 0.50),
                        (0.0, 1.0, 1, 0.32), (A, -0.5, 1, 0.32), (-A, -0.5, 1, 0.32)],
             gate_dirs=[(0.3, -1.0), (0.3, 1.0)])

# 큰성: 중앙 4단 + 윗줄에 3단·2단, 아랫줄 양끝에 1단 2개, 성문 4방
build_castle("castle-large.glb", "large",
             buildings=[(0.0, 0.0, 4, 0.55),
                        (-A, 0.9, 3, 0.42), (A, 0.9, 2, 0.40),
                        (-2 * A, -0.6, 1, 0.32), (2 * A, -0.6, 1, 0.32)],
             gate_dirs=[(0.0, -1.0), (0.0, 1.0), (1.0, 0.0), (-1.0, 0.0)])
