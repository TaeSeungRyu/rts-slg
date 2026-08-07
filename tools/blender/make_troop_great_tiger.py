# 이벤트 유닛 22 — 대호(GreatTiger, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_great_tiger.py
#
# doc/spec-unit.md: [육지, 속도 3, 탐지 2, 사거리 1/1/1] 대호 — 편대 없이 항상 1마리.
# 사족보행은 기병 다리 규약(leg_fl/hoof_fl…)을 그대로 써서 갤럽을 재사용한다 —
# 발 이름이 hoof인 것은 다리 끝(Tip) 규약 이름이라서다. rider가 없으므로
# 컨트롤러가 짐승(_beast)으로 판별해 덮치기 공격을 쓴다.
# 중립(이벤트) 유닛이라 세력색 red 재질을 쓰지 않는다.
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_FUR = ic.make_mat("fur", (0.78, 0.42, 0.12))
M_STRIPE = ic.make_mat("stripe", (0.10, 0.08, 0.06))
M_BELLY = ic.make_mat("belly", (0.90, 0.86, 0.78))

# ── 몸통: 낮게 깔린 타원체. 자식이 회전하므로 스케일 굽기 ──
HIP_Z = 0.095
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=1.0,
                                     location=(0, 0.006, 0.108))
body = bpy.context.object
body.name = "body"
body.scale = (0.052, 0.118, 0.046)
body.data.materials.append(M_FUR)
ic.shade_smooth(body)
ic.bake_scale(body)

# 줄무늬: 타원체 표면을 따라 마디를 10도 간격으로 촘촘히 겹쳐 끊기지 않는
# 곡선 띠를 만든다. 위치·기울기는 단면 타원 식 — 표면에 붙는다.
# 항상 1마리라 무늬는 한 벌 — 최초 5줄 배치를 다듬은 것이다:
# 띠 양 끝이 가늘어지도록(taper) 굵기를 사인 곡선으로 줄인다.
BODY_A, BODY_B, BODY_C = 0.052, 0.118, 0.046
BODY_CY, BODY_CZ = 0.006, 0.108


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    merged = bpy.context.object
    merged.name = name
    return merged


STRIPES = (
    (-0.072, 0.010, 0.014, -100, 78, 0.012),
    (-0.034, -0.008, 0.020, -70, 102, 0.014),
    (0.008, 0.014, -0.010, -105, 60, 0.013),
    (0.048, -0.012, 0.016, -60, 95, 0.012),
    (0.084, 0.004, 0.010, -48, 48, 0.011),
)
segs = []
for i, (y0, slant, bow, deg0, deg1, w) in enumerate(STRIPES):
    for j, deg in enumerate(range(deg0, deg1 + 1, 10)):
        th = math.radians(deg)
        t = deg / 90.0
        f = (deg - deg0) / (deg1 - deg0)
        sy = y0 + slant * t + bow * t * t
        s = math.sqrt(max(1.0 - (sy / BODY_B) ** 2, 0.0))
        sx = BODY_A * s * 0.99 * math.sin(th)
        sz = BODY_CZ + BODY_C * s * 0.99 * math.cos(th)
        taper = 0.55 + 0.45 * math.sin(math.pi * f)
        segs.append(ic.box(f"s{i}_{j}", 0.015, w * taper, 0.007, sx, BODY_CY + sy, sz,
                           M_STRIPE, rot_y=th))
stripes = join(segs, "stripes")
ic.parent_to(stripes, body)

# 가슴 흰 털
chest = ic.box("chest_fur", 0.034, 0.014, 0.034, 0, -0.100, 0.096, M_BELLY)
ic.parent_to(chest, body)

# ── 다리 4(피벗=고관절) + 발. 발 이름은 다리 끝 규약(hoof_*)을 따른다 ──
for tag, lx, ly in (("fl", -0.028, -0.072), ("fr", 0.028, -0.072),
                    ("bl", -0.028, 0.072), ("br", 0.028, 0.072)):
    leg = ic.box(f"leg_{tag}", 0.024, 0.027, HIP_Z, lx, ly, HIP_Z, M_FUR,
                 origin_shift=(0, 0, -HIP_Z / 2))
    paw = ic.box(f"hoof_{tag}", 0.028, 0.034, 0.015, lx, ly - 0.004, 0.008, M_BELLY)
    ic.parent_to(paw, leg)
    ic.parent_to(leg, body)

# ── 머리: 둥근 상자 + 흰 주둥이 + 코 + 귀 2 + 뺨 줄무늬 ──
head = ic.box("head", 0.048, 0.052, 0.044, 0, -0.138, 0.138, M_FUR)
muzzle = ic.box("muzzle", 0.028, 0.022, 0.022, 0, -0.172, 0.128, M_BELLY)
nose = ic.box("nose", 0.012, 0.008, 0.008, 0, -0.185, 0.138, M_STRIPE)
for i, ex in enumerate((-0.016, 0.016)):
    ear = ic.box(f"ear_{i}", 0.013, 0.010, 0.016, ex, -0.128, 0.166, M_FUR)
    ic.parent_to(ear, head)
for i, cx in enumerate((-0.025, 0.025)):
    cheek = ic.box(f"cheek_stripe_{i}", 0.004, 0.030, 0.012, cx, -0.148, 0.134, M_STRIPE)
    ic.parent_to(cheek, head)
for part in (muzzle, nose):
    ic.parent_to(part, head)
ic.parent_to(head, body)

# ── 꼬리: 위로 휘어 올라가는 두 마디 + 검은 끝 ──
tail1 = ic.box("tail1", 0.014, 0.016, 0.062, 0, 0.128, 0.140, M_FUR,
               rot_x=math.radians(-38))
tail2 = ic.box("tail2", 0.012, 0.014, 0.040, 0, 0.152, 0.184, M_FUR,
               rot_x=math.radians(-14))
tail_tip = ic.box("tail_tip", 0.013, 0.015, 0.014, 0, 0.157, 0.210, M_STRIPE,
                  rot_x=math.radians(-14))
ic.parent_to(tail_tip, tail2)
ic.parent_to(tail2, tail1)
ic.parent_to(tail1, body)

# 대호(大虎)라 보통 호랑이보다 크게 — 균등 스케일로만(축마다 다르면 다리 회전이 전단)
body.scale = (1.3, 1.3, 1.3)

ic.export("troop-great-tiger.glb")
