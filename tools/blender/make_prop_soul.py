# 효과 소품 — 영혼(soul, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_prop_soul.py
#
# design-effect.md #13 `SoulRise`용. 정면 -Y로 납작한 유령 실루엣 —
# 머리(구) + 아래로 갈수록 좁아지는 몸통 + 팔 돌기 2개 + 옆으로 흐르는 꼬리.
# 색은 효과에서 반투명 언셰이드 재질로 덮으므로 여기 재질은 자리표시다.
import bpy
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

M_SOUL = ic.make_mat("soul", (0.75, 0.90, 1.00))

body = ic.cone("soul", 0.014, 0.050, 0.115, 0, 0, 0.052, M_SOUL, verts=10, smooth=True)

bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=7, radius=0.048,
                                     location=(0, 0, 0.118))
head = bpy.context.object
head.data.materials.append(M_SOUL)
ic.shade_smooth(head)

arms = []
for sx in (-1, 1):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.016,
                                         location=(sx * 0.048, 0, 0.086))
    arm = bpy.context.object
    arm.data.materials.append(M_SOUL)
    ic.shade_smooth(arm)
    arms.append(arm)

tail = ic.cone("soul_tail", 0.004, 0.013, 0.052, 0.020, 0, 0.006, M_SOUL, verts=8, smooth=True)
tail.rotation_euler = (0.0, 0.55, 0.0)

bpy.ops.object.select_all(action="DESELECT")
for o in (body, head, tail, *arms):
    o.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()

# 정면(-Y)으로 납작하게 — 요-빌보드로 카메라를 향하는 실루엣이라 두께가 얇아야 읽힌다
body.scale = (1.0, 0.62, 1.0)
ic.bake_scale(body)

# 워크트리에서도 자기 저장소의 assets로 내보내도록 경로를 스크립트 기준으로 잡는다
out = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "..", "SanguoSLG.Game", "assets", "models",
                                    "prop-soul.glb"))
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True)
print("EXPORTED:", out)
