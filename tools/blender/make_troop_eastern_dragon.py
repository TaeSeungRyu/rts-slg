# 이벤트 유닛 24 — 동양풍 용(EasternDragon, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_eastern_dragon.py
#
# doc/spec-unit.md: [육지, 속도 0, 탐지 2, 사거리 1/1/1] 타일 3개 차지, 항상 1마리.
# 3칸은 중간성과 같은 삼각형 클러스터다(직선 아님) — 그래서 몸을 늘어뜨리지 않고
# 뱀이 똬리를 틀 듯 나선으로 감아 클러스터 중앙에 앉힌다(2026-08-07 사용자 확정).
# 모습은 드래곤볼 신룡 느낌: 초록 비늘 몸이 공중에 살짝 뜬 채 감기고, 중심에서
# 목이 솟아 머리가 된다. 사슴뿔·긴 수염·붉은 눈·등갈기 + 주위를 도는 구름(cloud_ring).
# 몸 마디 spine_0(꼬리)~N(목 끝)은 런타임이 위상을 어긋내 상하로 물결치게 한다.
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
M_CLOUD = ic.make_mat("cloud", (0.93, 0.94, 0.96), roughness=1.0)


def blob(name, x, y, z, r, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=r,
                                         location=(x, y, z))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    ic.shade_smooth(o)
    return o


# ── 몸: 똬리(나선) 26마디 + 중심에서 솟는 목 6마디. 전부 공중에 살짝 떠 있다 ──
N_COIL = 26
N_NECK = 6
TURNS = 1.7
ANG0 = math.radians(160)

positions = []
for i in range(N_COIL):
    t = i / (N_COIL - 1)
    ang = ANG0 + t * TURNS * math.tau
    radius = 0.40 - 0.24 * t
    z = 0.075 + 0.165 * t
    r = 0.022 + (0.056 - 0.022) * min(t * 2.4, 1.0)
    positions.append((radius * math.sin(ang), radius * math.cos(ang), z, r))
end_ang = ANG0 + TURNS * math.tau
for j in range(N_NECK):
    u = (j + 1) / N_NECK
    ang = end_ang + u * 0.8
    radius = 0.16 * (1 - 0.88 * u)
    z = 0.240 + 0.295 * u
    r = 0.055 - 0.009 * u
    positions.append((radius * math.sin(ang), radius * math.cos(ang), z, r))

body = blob("body", 0, 0, 0.02, 0.015, M_SCALE2)
spine = []
for i, (x, y, z, r) in enumerate(positions):
    seg = blob(f"spine_{i}", x, y, z, r, M_SCALE)
    ic.parent_to(seg, body)
    spine.append(seg)

# 꼬리 끝 술(spine_0이 꼬리)
tx, ty, tz, _ = positions[0]
tail_tuft = ic.cone("tail_tuft", 0.022, 0.001, 0.055, tx, ty + 0.045, tz + 0.010,
                    M_SCALE2, verts=7, rot_x=math.radians(70), smooth=True)
ic.parent_to(tail_tuft, spine[0])

# 등갈기: 목 마디들 위 지느러미 — 마디 자식이라 물결을 같이 탄다
for i in range(N_COIL, N_COIL + N_NECK):
    x, y, z, r = positions[i]
    fin = ic.box(f"mane_fin_{i}", 0.009, 0.030, 0.030, x, y + 0.010, z + r + 0.004,
                 M_SCALE2, rot_x=math.radians(-14))
    ic.parent_to(fin, spine[i])

# 작은 팔 2: 목 뿌리 마디 양옆 — 신룡의 짧은 앞발
bx, by, bz, br = positions[N_COIL]
for s in (-1, 1):
    arm = ic.box(f"dragon_arm_{'l' if s < 0 else 'r'}", 0.014, 0.018, 0.055,
                 bx + s * (br + 0.008), by, bz - 0.040, M_SCALE2,
                 rot_x=math.radians(-18))
    claw = ic.box(f"dragon_claw_{'l' if s < 0 else 'r'}", 0.020, 0.028, 0.010,
                  bx + s * (br + 0.008), by - 0.008, bz - 0.070, M_BELLY)
    ic.parent_to(claw, arm)
    ic.parent_to(arm, spine[N_COIL])

# ── 머리(목 끝 마디 자식): 각진 머리 + 주둥이 + 벌린 턱 + 붉은 눈 + 사슴뿔 + 긴 수염 ──
hx, hy, hz, _ = positions[-1]
HY = hy - 0.055
HZ = hz + 0.052
dragon = ic.box("dragon_head", 0.072, 0.080, 0.052, hx, HY, HZ, M_SCALE)
snout = ic.box("dragon_snout", 0.048, 0.052, 0.026, hx, HY - 0.056, HZ - 0.010, M_SCALE)
jaw = ic.box("dragon_jaw", 0.042, 0.044, 0.011, hx, HY - 0.050, HZ - 0.032, M_SCALE2,
             rot_x=math.radians(14))
for i, ex in enumerate((-0.026, 0.026)):
    eye = ic.box(f"dragon_eye_{i}", 0.010, 0.009, 0.008, hx + ex, HY - 0.030, HZ + 0.022, M_EYE)
    ic.parent_to(eye, dragon)
for i, ax in enumerate((-0.026, 0.026)):
    antler = ic.box(f"antler_{i}", 0.010, 0.012, 0.058, hx + ax, HY + 0.034, HZ + 0.050,
                    M_ANTLER, rot_x=math.radians(-28))
    branch = ic.box(f"antler_{i}_branch", 0.008, 0.010, 0.034,
                    hx + ax + (0.008 if ax > 0 else -0.008), HY + 0.056, HZ + 0.066,
                    M_ANTLER, rot_x=math.radians(-58))
    ic.parent_to(branch, antler)
    ic.parent_to(antler, dragon)
for i, wx in enumerate((-0.030, 0.030)):
    whisker = ic.box(f"whisker_{i}", 0.004, 0.090, 0.004, hx + wx, HY - 0.106, HZ - 0.016,
                     M_SCALE2, rot_x=math.radians(-6))
    ic.parent_to(whisker, dragon)
for part in (snout, jaw):
    ic.parent_to(part, dragon)
ic.parent_to(dragon, spine[-1])

# ── 구름 고리: 용 주위를 도는 구름 뭉치 4 — 런타임이 cloud_ring을 천천히 회전시킨다 ──
bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
ring = bpy.context.object
ring.name = "cloud_ring"
CLOUDS = ((0, 0.56, 0.26, 1.00), (95, 0.60, 0.40, 0.80),
          (198, 0.54, 0.33, 1.15), (300, 0.62, 0.22, 0.90))
for i, (deg, radius, z, s) in enumerate(CLOUDS):
    ang = math.radians(deg)
    cx, cy = radius * math.sin(ang), radius * math.cos(ang)
    puff0 = blob(f"cloud{i}_a", cx, cy, z, 0.062 * s, M_CLOUD)
    puff1 = blob(f"cloud{i}_b", cx + 0.055 * s, cy + 0.020 * s, z - 0.012, 0.045 * s, M_CLOUD)
    puff2 = blob(f"cloud{i}_c", cx - 0.050 * s, cy - 0.015 * s, z - 0.008, 0.040 * s, M_CLOUD)
    for part in (puff1, puff2):
        ic.parent_to(part, puff0)
    ic.parent_to(puff0, ring)
ic.parent_to(ring, body)

# 타일 3칸 클러스터에 맞춰 확대 — 균등 스케일로만(축마다 다르면 마디 물결이 전단)
body.scale = (1.25, 1.25, 1.25)

ic.export("troop-eastern-dragon.glb")
