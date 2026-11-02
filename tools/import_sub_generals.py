import hashlib
import json
import re
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'doc/plan-scenario-sub-generals.md'
SOURCE = ROOT / 'images-sub-hero'
PORTRAITS = ROOT / 'SanguoSLG.Game/assets/portraits'
GENERAL_FILE = ROOT / 'data/generals.json'
UNLOCK_FILE = ROOT / 'data/hero_unlocks.json'
META_FILE = ROOT / 'data/general-portraits.json'

CIVIL = set('가규 가신 간옹 경기 괴량 국연 내민 노육 동부 동소 두경 두기 두습 루규 맹건 맹광 미축 부간 부손 사마랑 사마부 사마지 사손서 사원 손건 손소 손자 송충 순상 순심 시의 양부 양송 양표 양홍 엄준 여범 여예 오질 왕랑 왕상 왕수 요립 유괴 유기 유복 유순 유파 유화 유훈 윤묵 응소 이적 장송 장예 전주 정비 정의 정태 조식 조욱 종예 주소 중장통 진군 진림 최림 최염 향랑 혜구 황완'.split())
CAVALRY = set('견초 누반 단경 단외 동승 무안국 성공영 성의 순우경 양추 양흥 엄강 여광 여위황 염항 오란 원희 유봉 이감 장순 장연 장패 장횡 전예 조순 조창 주령 철리길 하후덕 하후은 후선 후성'.split())
ARCHER = set('여대 손교 손환 전종 조휴 주창'.split())
REGION = {'조조': 'yuzhou', '유비': 'yizhou', '손권': 'yangzhou', '동탁': 'sili',
          '원소': 'jizhou', '원술': 'yangzhou', '공손찬': 'youzhou', '공손강': 'youzhou',
          '유표': 'jingzhou', '유언': 'yizhou', '유장': 'yizhou', '마등': 'liangzhou',
          '마초': 'liangzhou', '한수': 'liangzhou', '장로': 'yizhou', '도겸': 'xuzhou',
          '공융': 'qingzhou', '여포': 'yanzhou', '장각': 'jizhou', '사섭': 'jiaozhou'}


def score(name, salt):
    return int.from_bytes(hashlib.sha256(f'{name}:{salt}'.encode()).digest()[:4], 'big')


def append_records(path, entries):
    if not entries:
        return
    text = path.read_text(encoding='utf-8')
    tail = text.rstrip()
    assert tail.endswith(']')
    prefix = tail[:-1].rstrip()
    comma = ',' if not prefix.endswith('[') else ''
    rendered = ',\n'.join(json.dumps(x, ensure_ascii=False, indent=2) for x in entries)
    path.write_text(prefix + comma + '\n' + rendered + '\n]\n', encoding='utf-8')


def main():
    plan = PLAN.read_text(encoding='utf-8')
    table = plan.split('## 중복 제거 작업 목록', 1)[1].split('## 184년', 1)[0]
    rows = re.findall(r'^\|\s*\d+\s*\|\s*([^|(]+?)(?:\s*\([^)]*\))?\s*\|\s*([^|]+)\|', table, re.M)
    pictures = {p.stem: p for p in SOURCE.glob('*.png')}
    generals = json.loads(GENERAL_FILE.read_text(encoding='utf-8'))
    known = {g['name'] for g in generals}
    ruler_ids = {g['name']: g['id'] for g in generals}
    ids = {g['id'] for g in generals}
    unlocks = json.loads(UNLOCK_FILE.read_text(encoding='utf-8'))
    unlock_ids = {x['general'] for x in unlocks}
    portraits = json.loads(META_FILE.read_text(encoding='utf-8'))
    portrait_ids = {x['general_id'] for x in portraits}
    first_year = {}
    for year, section in [(184, '184년'), (190, '190년'), (194, '194년'),
                          (208, '208년'), (214, '214년')]:
        body = plan.split('## ' + section, 1)[1].split('\n## ', 1)[0]
        for name in pictures:
            if name in body:
                first_year[name] = min(year, first_year.get(name, year))
    next_id = max(ids) + 1
    new_generals, new_unlocks, new_meta = [], [], []
    for raw_name, raw_factions in rows:
        name = raw_name.strip()
        if name not in pictures or name in known:
            continue
        faction = raw_factions.strip().split(',')[0].strip()
        role = '문관' if name in CIVIL else '기병장' if name in CAVALRY else '궁장' if name in ARCHER else '무장'
        base = {'문관': (37, 65, 68), '기병장': (68, 42, 38),
                '궁장': (61, 49, 41), '무장': (60, 45, 43)}[role]
        might, intellect, politics = [min(79, max(25, v + score(name, i) % 19 - 9))
                                       for i, v in enumerate(base)]
        main_type = 'supply' if role == '문관' else 'cavalry' if role == '기병장' else 'archer' if role == '궁장' else 'infantry'
        aptitudes = {kind: 'C' for kind in ('infantry', 'archer', 'cavalry', 'elephant', 'siege', 'naval', 'supply', 'defense')}
        aptitudes[main_type] = 'A' if score(name, 4) % 4 == 0 else 'B'
        aptitudes['defense' if role != '문관' else 'siege'] = 'B' if score(name, 5) % 3 == 0 else 'C'
        active = None if role == '문관' or might <= 75 else (
            'brace' if score(name, 6) % 3 == 0 else 'heavy_blow')
        passive = ('steadfast_guard' if role == '문관' else 'charge_drill' if role == '기병장' else 'drilled')
        admin = ('tuntian' if role == '문관' and score(name, 7) % 2 else 'pacifier' if role == '문관' else 'drillmaster')
        general = {
            'id': next_id, 'name': name,
            'birth': first_year.get(name, 208) - 18 - score(name, 8) % 11,
            'region': REGION.get(faction, 'sili'),
            'desc': f'{faction} 측의 {role}. 시나리오 보조 장수.',
            'aptitudes': aptitudes, 'might': might, 'intellect': intellect, 'politics': politics,
            'battle_active': active,
            'battle_passives': [{'code': passive, 'tier': 1}],
            'admin_passives': [{'code': admin, 'tier': 1}]
        }
        new_generals.append(general)
        if next_id not in unlock_ids:
            new_unlocks.append({'general': next_id, 'type': 'faction',
                                'recruit_point_cost': 65 + score(name, 9) % 86,
                                'title': name, 'desc': '시나리오 보조 장수 영입',
                                'required_general': ruler_ids.get(faction)})
        if next_id not in portrait_ids:
            new_meta.append({'general_id': next_id,
                             'portrait_path': f'SanguoSLG.Game/assets/portraits/{next_id}.png',
                             'face_center_x': 0.5, 'face_center_y': 0.35, 'face_zoom': 1})
        shutil.copyfile(pictures[name], PORTRAITS / f'{next_id}.png')
        known.add(name)
        next_id += 1
    append_records(GENERAL_FILE, new_generals)
    append_records(UNLOCK_FILE, new_unlocks)
    append_records(META_FILE, new_meta)
    print(f'added={len(new_generals)} ids={new_generals[0]["id"] if new_generals else "-"}..{next_id-1}')


if __name__ == '__main__':
    main()
