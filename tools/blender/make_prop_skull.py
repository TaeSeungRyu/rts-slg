# 효과 소품 — 해골(Skull, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_prop_skull.py
#
# design-effect.md #5 `Skulls` 파티클용. 정면은 -Y.
# 몸통(두개골·턱)은 짙은 회색, 눈·입은 흰색으로 대비를 줘 작은 크기에서도 해골로 읽힌다.
# 파티클은 색을 안 건드리고 크기 커브로 나타났다 사라지므로, 여기 재질 색이 그대로 보인다.
import bpy
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

M_BONE = ic.make_mat("skull_bone", (0.28, 0.28, 0.31))
M_FEATURE = ic.make_mat("skull_feature", (0.94, 0.94, 0.96))

# ── 두개골: 살짝 눌린 구(더 크게) ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=7, radius=0.052,
                                     location=(0, 0, 0.062))
cranium = bpy.context.object
cranium.name = "cranium"
cranium.scale = (1.0, 1.06, 0.94)
cranium.data.materials.append(M_BONE)
ic.shade_smooth(cranium)
ic.bake_scale(cranium)

# ── 턱 ──
jaw = ic.box("jaw", 0.058, 0.042, 0.030, 0, -0.012, 0.020, M_BONE)
ic.parent_to(jaw, cranium)

# ── 눈 2(흰색, 앞면 -Y) ──
for i, ex in enumerate((-0.019, 0.019)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=0.015,
                                         location=(ex, -0.040, 0.068))
    eye = bpy.context.object
    eye.name = f"eye_{i}"
    eye.scale = (1.0, 0.7, 1.05)
    eye.data.materials.append(M_FEATURE)
    ic.bake_scale(eye)
    ic.parent_to(eye, cranium)

# ── 입(흰색, 이빨 칸을 낸 가로 박스 3토막) ──
for i, mx in enumerate((-0.016, 0.0, 0.016)):
    tooth = ic.box(f"mouth_{i}", 0.012, 0.010, 0.016, mx, -0.030, 0.024, M_FEATURE)
    ic.parent_to(tooth, cranium)

ic.export("prop-skull.glb")
