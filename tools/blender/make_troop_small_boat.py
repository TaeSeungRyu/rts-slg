# 병종 8 — 소선(SmallBoat, 저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python-exit-code 1 --python make_troop_small_boat.py
#
# doc/design-unit.md: [대하, 속도 2, 탐지 3, 사거리 1/1/1] 중국식 작은 배.
# 내부에 사람 없음(사용자 확정). 이동 시 물보라(런타임 파티클)와 돛 천이 움직인다.
#
# 부위 노드 규약(선박 공통 — 배 6종이 재사용할 기반):
#   body (부모=선체) ← 전부 자식. 이동 중 롤·피치로 흔들린다
#   mast ← sail : 돛(원점=돛대 축). 런타임에 돛대 축으로 흔들리고 펄럭인다
#   sail 판별 노드가 선박 규약 마커다
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)

m = ic.Mats()
M_HULL = ic.make_mat("hull", (0.38, 0.20, 0.08))
M_DECK = ic.make_mat("deck", (0.52, 0.30, 0.11))
M_SAIL = ic.make_mat("sail", (0.80, 0.68, 0.42), roughness=0.9)
M_BATTEN = ic.make_mat("batten", (0.35, 0.24, 0.14))

# ── 선체(부모): 몸통 상자. 돛이 밑에서 회전하므로 스케일을 굽는다 ──
body = ic.box("body", 0.095, 0.230, 0.042, 0, 0, 0.030, M_HULL)
ic.bake_scale(body)

# 이물·고물(뾰족한 앞뒤): 회전한 상자 — 항구 나룻배와 같은 수법
for endsign, tag in ((-1, "bow"), (1, "stern")):
    for s in (-1, 1):
        wedge = ic.box(f"{tag}_{'l' if s < 0 else 'r'}", 0.062, 0.050, 0.040,
                       s * 0.020, endsign * 0.128, 0.032, M_HULL,
                       rot_z=endsign * s * math.radians(30))
        ic.parent_to(wedge, body)

# 갑판(안쪽 밝은 판) + 옆 난간
deck = ic.box("deck", 0.070, 0.190, 0.010, 0, 0, 0.052, M_DECK)
ic.parent_to(deck, body)
for s in (-1, 1):
    rail = ic.box(f"gunwale_{'l' if s < 0 else 'r'}", 0.010, 0.215, 0.016,
                  s * 0.046, 0, 0.058, M_DECK)
    ic.parent_to(rail, body)

# 고물 키(방향타)
rudder = ic.box("rudder", 0.008, 0.030, 0.045, 0, 0.150, 0.020, M_DECK,
                rot_x=math.radians(-14))
ic.parent_to(rudder, body)

# ── 돛대 + 정크 돛(가로 배튼) + 세력색 기 ──
mast = ic.cylinder("mast", 0.007, 0.250, 0, 0.020, 0.175, M_BATTEN, verts=6)
ic.parent_to(mast, body)

# 돛: 원점이 돛대 축에 오도록 돛대 위치에 만든다 — 런타임 rotation.y가 돛대 축 회전이 된다
sail = ic.box("sail", 0.120, 0.005, 0.150, 0, 0.020, 0.195, M_SAIL)
for i in range(4):
    batten = ic.box(f"sail_batten_{i}", 0.126, 0.007, 0.007, 0, 0.020, 0.135 + i * 0.040,
                    M_BATTEN)
    ic.parent_to(batten, sail)
flag = ic.box("sail_flag", 0.036, 0.004, 0.020, 0.024, 0.020, 0.310, m.red)
ic.parent_to(flag, sail)
ic.parent_to(sail, mast)

ic.export("troop-small-boat.glb")
