# 효과 소품 — 물음표(Question mark, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_prop_question.py
#
# design-effect.md #12 `Confusion`용. XZ 평면에 눕힌 2D 형태(정면 -Y).
# 곡선에 베벨을 줘 저폴리 튜브 "?"를 만들고 아래 점은 작은 구. 색은 효과에서
# 언셰이드 재질로 덮으므로 여기 재질은 자리표시다.
import bpy
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

M_MARK = ic.make_mat("qmark", (1.0, 0.85, 0.22))

# ── "?" 갈고리+기둥: 열린 베지어 곡선(닫히면 O가 된다) ──
pts = [
    (-0.030, 0, 0.030),
    (-0.045, 0, 0.065),
    (-0.028, 0, 0.092),
    (0.005, 0, 0.098),
    (0.038, 0, 0.078),
    (0.040, 0, 0.045),
    (0.010, 0, 0.028),
    (0.004, 0, 0.005),
    (0.004, 0, -0.028),
]

curve = bpy.data.curves.new("qmark_curve", type="CURVE")
curve.dimensions = "3D"
curve.resolution_u = 3
curve.bevel_depth = 0.011
curve.bevel_resolution = 1        # 저폴리 튜브 단면
spline = curve.splines.new("BEZIER")
spline.bezier_points.add(len(pts) - 1)
for bp, (x, y, z) in zip(spline.bezier_points, pts):
    bp.co = (x, y, z)
    bp.handle_left_type = "AUTO"
    bp.handle_right_type = "AUTO"

mark = bpy.data.objects.new("qmark", curve)
bpy.context.collection.objects.link(mark)
bpy.context.view_layer.objects.active = mark
mark.select_set(True)
bpy.ops.object.convert(target="MESH")
mark.data.materials.append(M_MARK)
ic.shade_smooth(mark)

# ── 아래 점 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=0.015,
                                     location=(0.004, 0, -0.062))
dot = bpy.context.object
dot.name = "qdot"
dot.data.materials.append(M_MARK)
ic.shade_smooth(dot)
ic.parent_to(dot, mark)

ic.export("prop-question.glb")
