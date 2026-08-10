# 효과 소품 — 회색 해골(Skull, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_prop_skull.py
#
# design-effect.md #5 `Skulls` 파티클에 쓰는 작은 해골. 정면은 -Y.
# 눈구멍은 불리언 대신 어두운 안쪽 구를 박아 넣는다(작은 크기라 같은 인상, 더 견고).
# 재질 2종(회색 뼈 / 어두운 구멍)이라 파티클 페이드가 각 표면 색을 유지하며 사라진다.
import bpy
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

M_BONE = ic.make_mat("skull_bone", (0.64, 0.63, 0.59))
M_HOLE = ic.make_mat("skull_hole", (0.09, 0.09, 0.10))

# ── 두개골: 살짝 눌린 구 ──
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=7, radius=0.040,
                                     location=(0, 0, 0.048))
cranium = bpy.context.object
cranium.name = "cranium"
cranium.scale = (1.0, 1.06, 0.94)
cranium.data.materials.append(M_BONE)
ic.shade_smooth(cranium)
ic.bake_scale(cranium)

# ── 턱: 앞아래로 튀어나온 상자 ──
jaw = ic.box("jaw", 0.044, 0.032, 0.022, 0, -0.010, 0.016, M_BONE)
ic.parent_to(jaw, cranium)

# ── 눈구멍 2 + 코구멍: 앞면(-Y)에 박힌 어두운 구/상자 ──
for i, ex in enumerate((-0.015, 0.015)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.011,
                                         location=(ex, -0.031, 0.052))
    eye = bpy.context.object
    eye.name = f"eye_{i}"
    eye.scale = (1.0, 0.7, 1.1)
    eye.data.materials.append(M_HOLE)
    ic.bake_scale(eye)
    ic.parent_to(eye, cranium)

nose = ic.box("nose", 0.006, 0.008, 0.012, 0, -0.034, 0.034, M_HOLE)
ic.parent_to(nose, cranium)

ic.export("prop-skull.glb")
