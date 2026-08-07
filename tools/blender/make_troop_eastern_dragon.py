# 이벤트 유닛 24 — 동양풍 용(EasternDragon, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_eastern_dragon.py
#
# doc/spec-unit.md: [육지, 속도 0, 탐지 2, 사거리 1/1/1] 타일 3개 차지, 항상 1마리.
# 모습(사용자 확정, 2026-08-07): 드래곤볼의 신룡 느낌 — 초록 비늘의 긴 뱀 몸이
# S자로 땅을 타고, 머리 쪽이 높이 솟는다. 사슴뿔·긴 수염·붉은 눈·등갈기·작은 발.
# 몸은 구슬(타원체) 사슬 spine_0..N — 런타임이 마디마다 위상을 어긋내 상하로
# 천천히 물결치게 한다(Serpent 모션). 공격은 머리를 젖혔다 내밀며 화염(거북선과 공유).
# 중립(이벤트) 유닛이라 세력색 red 재질을 쓰지 않는다(눈의 붉은색은 별도 재질).
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_SCALE = ic.make_mat("scale_green", (0.22, 0.54, 0.26))
M_SCALE2 = ic.make_mat("scale_dark", (0.14, 0.38, 0.18))
M_BELLY = ic.make_mat("dragon_belly", (0.85, 0.80, 0.55))
M_ANTLER = ic.make_mat("antler", (0.74, 0.62, 0.44))
M_EYE = ic.make_mat("eye_red", (0.85, 0.14, 0.10), roughness=0.4)


def blob(name, x, y, z, r, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=r,
                                         location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    ic.shade_smooth(o)
    return o


def sx(y):
    return 0.15 * math.sin((y + 0.02) * 3.4)


# ── 몸 중심(부모, 정지 마디) + 물결치는 마디 사슬 spine_0(목)~spine_12(꼬리) ──
body = blob("body", sx(-0.015), -0.015, 0.090, 0.058, M_SCALE)

SEGS = (
    (-0.660, 0.350, 0.046),
    (-0.575, 0.280, 0.051),
    (-0.480, 0.212, 0.055),
    (-0.375, 0.152, 0.057),
    (-0.260, 0.114, 0.058),
    (-0.140, 0.094, 0.058),
    (0.110, 0.090, 0.056),
    (0.235, 0.094, 0.052),
    (0.355, 0.099, 0.047),
    (0.470, 0.100, 0.041),
    (0.580, 0.094, 0.034),
    (0.685, 0.089, 0.027),
    (0.785, 0.084, 0.021),
)
spine = []
for i, (y, z, r) in enumerate(SEGS):
    seg = blob(f"spine_{i}", sx(y), y, z, r, M_SCALE)
    ic.parent_to(seg, body)
    spine.append(seg)

# 등갈기: 앞쪽 마디들 위에 작은 지느러미 판 — 마디 자식이라 물결을 같이 탄다
for i in (0, 1, 2, 3, 4, 5):
    y, z, r = SEGS[i]
    fin = ic.box(f"mane_fin_{i}", 0.009, 0.034, 0.032, sx(y), y + 0.008, z + r + 0.006,
                 M_SCALE2, rot_x=math.radians(-16))
    ic.parent_to(fin, spine[i])

# 꼬리 끝 술
tail_tuft = ic.cone("tail_tuft", 0.024, 0.001, 0.055, sx(0.855), 0.855, 0.082,
                    M_SCALE2, verts=7, rot_x=math.radians(78), smooth=True)
ic.parent_to(tail_tuft, spine[12])

# ── 작은 발 4: 앞발은 spine_3, 뒷발은 spine_7 자식 — 물결을 같이 탄다 ──
for i, tag in ((3, "f"), (7, "b")):
    y, z, r = SEGS[i]
    for s in (-1, 1):
        cx = sx(y) + s * (r + 0.010)
        claw_leg = ic.box(f"claw_{tag}{'l' if s < 0 else 'r'}", 0.016, 0.020, z - 0.010,
                          cx, y, (z - 0.010) / 2 + 0.005, M_SCALE2)
        foot = ic.box(f"claw_{tag}{'l' if s < 0 else 'r'}_foot", 0.022, 0.034, 0.012,
                      cx, y - 0.010, 0.006, M_BELLY)
        ic.parent_to(foot, claw_leg)
        ic.parent_to(claw_leg, spine[i])

# ── 머리(spine_0 자식): 각진 머리 + 주둥이 + 벌린 턱 + 붉은 눈 + 사슴뿔 + 긴 수염 ──
HX = sx(-0.660)
dragon = ic.box("dragon_head", 0.072, 0.080, 0.052, HX, -0.750, 0.402, M_SCALE)
snout = ic.box("dragon_snout", 0.048, 0.052, 0.026, HX, -0.806, 0.392, M_SCALE)
jaw = ic.box("dragon_jaw", 0.042, 0.044, 0.011, HX, -0.800, 0.370, M_SCALE2,
             rot_x=math.radians(14))
for i, ex in enumerate((-0.026, 0.026)):
    eye = ic.box(f"dragon_eye_{i}", 0.010, 0.009, 0.008, HX + ex, -0.780, 0.424, M_EYE)
    ic.parent_to(eye, dragon)
for i, ax in enumerate((-0.026, 0.026)):
    antler = ic.box(f"antler_{i}", 0.010, 0.012, 0.058, HX + ax, -0.716, 0.452, M_ANTLER,
                    rot_x=math.radians(-28))
    branch = ic.box(f"antler_{i}_branch", 0.008, 0.010, 0.034, HX + ax + (0.008 if ax > 0 else -0.008),
                    -0.694, 0.468, M_ANTLER, rot_x=math.radians(-58))
    ic.parent_to(branch, antler)
    ic.parent_to(antler, dragon)
for i, wx in enumerate((-0.030, 0.030)):
    whisker = ic.box(f"whisker_{i}", 0.004, 0.090, 0.004, HX + wx, -0.856, 0.386,
                     M_SCALE2, rot_x=math.radians(-6))
    ic.parent_to(whisker, dragon)
for part in (snout, jaw):
    ic.parent_to(part, dragon)
ic.parent_to(dragon, spine[0])

# 타일 3개 차지에 맞춰 전체 확대 — 균등 스케일로만(축마다 다르면 마디 물결이 전단)
body.scale = (1.15, 1.15, 1.15)

ic.export("troop-eastern-dragon.glb")
