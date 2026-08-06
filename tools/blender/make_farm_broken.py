# 부서진 밭 생성 → GLB 익스포트
# 실행: blender --background --python make_farm_broken.py
#
# 사용자 정의(2026-08-06): "밭은 기존 에셋에서 큰 네모를 4조각으로 만들고 배치한다."
# 밭(building-farm.glb)은 타일 기단과 작물이 한 덩어리인 단일 메시라 런타임에 쪼갤 수 없다.
# 그래서 여기서 높이로 기단/작물을 분리한 뒤, 작물 판을 4분면으로 잘라 어긋나게 배치한다.
# 킷 원본 메시를 그대로 자르므로 재질·UV(= colormap 텍스처)가 유지된다.
import math

import bmesh
import bpy
from mathutils import Vector

SRC = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\building-farm.glb"
OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\farm-broken.glb"

SPLIT_Z = 0.205   # 타일 윗면(0.2) 바로 위 — 이보다 위는 작물, 아래는 기단
SPREAD = 0.045    # 조각을 바깥으로 벌리는 거리
TWIST_DEG = 9.0   # 조각을 타일 중심 기준으로 비트는 각도

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

source = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.ops.object.select_all(action="DESELECT")
source.select_set(True)
bpy.context.view_layer.objects.active = source
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def keep_faces(obj, predicate):
    """predicate(중심점)이 참인 면만 남긴다."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = [f for f in bm.faces if not predicate(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def clip_half(obj, normal, keep_positive):
    """평면(원점, normal)으로 자르고 한쪽만 남긴다."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.bisect_plane(
        bm,
        geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
        dist=1e-5,
        plane_co=Vector((0.0, 0.0, 0.0)),
        plane_no=Vector(normal),
        clear_outer=not keep_positive,
        clear_inner=keep_positive,
    )
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def duplicate(obj, name):
    copy = obj.copy()
    copy.data = obj.data.copy()
    copy.name = name
    bpy.context.collection.objects.link(copy)
    return copy


# ── 기단(원본)과 작물 판(사본)으로 분리 ──
crops = duplicate(source, "farm_crops")
source.name = "farm_base"
keep_faces(source, lambda c: c.z <= SPLIT_Z)
keep_faces(crops, lambda c: c.z > SPLIT_Z)

# ── 작물 판을 4분면으로 잘라 어긋나게 배치 ──
quadrants = [(1, 1), (1, -1), (-1, 1), (-1, -1)]
for index, (sx, sy) in enumerate(quadrants):
    piece = duplicate(crops, f"farm_piece_{index}")
    clip_half(piece, (1.0, 0.0, 0.0), keep_positive=sx > 0)
    clip_half(piece, (0.0, 1.0, 0.0), keep_positive=sy > 0)

    # 바깥으로 밀고 중심 기준으로 살짝 비튼다 — 갈라져 어긋난 밭
    piece.location = (sx * SPREAD, sy * SPREAD, -0.004 * (index % 2))
    piece.rotation_euler = (0.0, 0.0, math.radians(TWIST_DEG * (1 if index % 2 == 0 else -1)))

bpy.data.objects.remove(crops, do_unlink=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)

for o in bpy.data.objects:
    if o.type == "MESH":
        d = o.dimensions
        print(f"PIECE {o.name} verts={len(o.data.vertices)} dim=({d.x:.3f},{d.y:.3f},{d.z:.3f})")
print("EXPORTED:", OUT)
