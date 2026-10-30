import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAP = json.loads((ROOT / 'SanguoSLG.Game/assets/maps/east-asia-user-preset.json').read_text(encoding='utf-8'))
GENERALS = json.loads((ROOT / 'data/generals.json').read_text(encoding='utf-8'))
REVISION = '5ef331b804d4b5fb64a17eb8d6fd4e8e3111078d4c92eb09f6ffe67ebe7183a2'
SITES = {site['Name']: site for site in MAP['Sites'] if site['Kind'] in ('city', 'port', 'gate')}
HEROES = {general['name']: general for general in GENERALS}
OUT = ROOT / 'SanguoSLG.Game/assets/scenarios'

SCENARIOS = [
    ('historical-184', '184년, 황건적의 횡포', 184, 3, [('황건군', '장각', '거록'), ('후한 조정', '하진', '낙양')]),
    ('historical-190', '190년, 동탁의 야망', 190, 4, [('동탁', '동탁', '장안'), ('원소', '원소', '남피'), ('원술', '원술', '완'), ('조조', '조조', '진류'), ('공손찬', '공손찬', '북평'), ('유표', '유표', '양양'), ('도겸', '도겸', '하비'), ('공융', '공융', '북해'), ('마등', '마등', '무위'), ('한수', '한수', '금성'), ('유언', '유언', '성도'), ('유비', '유비', '제북')]),
    ('historical-194', '194년, 영웅호걸', 194, 12, [('이각', '이각', '장안'), ('조조', '조조', '진류'), ('여포', '여포', '복양'), ('원소', '원소', '업'), ('원술', '원술', '수춘'), ('공손찬', '공손찬', '북평'), ('유비', '유비', '하비'), ('공융', '공융', '북해'), ('유표', '유표', '양양'), ('유장', '유장', '성도'), ('마등', '마등', '무위'), ('한수', '한수', '금성'), ('유요', '유요', '오'), ('손책', '손책', '여강')]),
    ('historical-208', '208년, 적벽대전', 208, 10, [('조조', '조조', '업'), ('손권', '손권', '예장'), ('유비', '유비', '강하'), ('유장', '유장', '성도'), ('장로', '장로', '한중'), ('한수', '한수', '금성'), ('마초', '마초', '무위'), ('공손강', '공손강', '양평'), ('사섭', '사섭', '교지')]),
    ('historical-214', '214년, 삼국시대', 214, 8, [('조조', '조조', '업'), ('손권', '손권', '건업'), ('유비', '유비', '성도'), ('장로', '장로', '한중'), ('한수', '한수', '금성'), ('공손강', '공손강', '양평'), ('사섭', '사섭', '교지')]),
    ('fictional-revival', '가상, 영웅부활', 220, 1, [('조조', '조조', '허창'), ('유비', '유비', '성도'), ('손권', '손권', '건업'), ('동탁', '동탁', '장안'), ('원소', '원소', '업'), ('원술', '원술', '수춘'), ('여포', '여포', '복양'), ('마초', '마초', '무위'), ('장각', '장각', '거록'), ('공손찬', '공손찬', '북평'), ('유표', '유표', '양양'), ('사섭', '사섭', '교지'), ('공손강', '공손강', '양평'), ('장로', '장로', '한중')]),
]

FOLLOWERS = {
    '장각': ['장보', '장량'], '하진': ['황보숭', '주준', '노식', '종요'],
    '동탁': ['화웅', '이각', '곽사', '여포', '장료'], '이각': ['곽사', '장료'],
    '조조': ['하후돈', '하후연', '조인', '조홍', '악진', '우금', '이전', '순욱', '순유', '정욱', '전위', '허저', '서황', '장합', '장료', '곽가'],
    '유비': ['관우', '장비', '조운', '제갈량', '황충', '위연', '방통', '마량', '법정'],
    '손권': ['주유', '노숙', '여몽', '육손', '감녕', '황개', '한당', '주태', '능통', '태사자'],
    '손책': ['주유', '황개', '한당'],
    '원소': ['안량', '문추', '전풍', '저수', '심배'], '원술': ['기령'],
    '여포': ['고순', '진궁', '장료'], '유표': ['황조', '문빙'],
    '유장': ['장임', '엄안'], '유언': ['유장', '장임', '엄안'],
    '공손강': ['공손공', '공손연'], '사섭': ['감례', '환린'],
}

SCENARIO_ADDITIONS = {
    'historical-190': {'동탁': ['가후', '초선'], '원소': ['곽도'], '조조': ['만총'],
                       '공손찬': ['조운'], '유표': ['황충'], '공융': ['태사자'],
                       '마등': ['마초', '방덕']},
    'historical-194': {'조조': ['만총'], '공손찬': ['조운'], '유비': ['진도'],
                       '마등': ['마초', '방덕', '마대'], '유요': ['태사자'], '손책': ['정보']},
    'historical-208': {'조조': ['사마의', '조비', '조진', '조휴', '가후', '유엽', '만총', '학소', '서서'],
                       '손권': ['정보', '장소', '제갈근', '서성', '주환', '반장', '하제'],
                       '유비': ['관평', '진도'], '유장': ['이엄', '오의'],
                       '마초': ['방덕', '마대'], '공손강': ['공손연']},
    'historical-214': {'조조': ['사마의', '조비', '조진', '조휴', '가후', '유엽', '만총', '학소', '왕쌍'],
                       '손권': ['정보', '장소', '제갈근', '서성', '주환', '반장', '하제', '정봉'],
                       '유비': ['마대', '관평', '이엄', '진도', '오의', '요화', '마속'],
                       '장로': ['방덕'], '공손강': ['공손연']},
}

SCENARIO_EXCLUSIONS = {
    'historical-194': {'유비': {'조운'}},
    'historical-208': {'유비': {'서서'}},
    'historical-214': {'손권': {'주유'}},
}

TERRITORIES = {
    'historical-184': {'후한 조정': '허창 하내 홍농 장안 남피 업 양양 성도 건업 하비 북해 무위 한중 낭야 광릉 진류 완', '황건군': '거록 평원 중산'},
    'historical-190': {'동탁': '장안 낙양 홍농 하내 안정 천수', '원소': '남피 업 중산 거록', '원술': '완 여남 수춘', '조조': '진류 복양 허창', '공손찬': '북평 계 양평', '유표': '양양 강릉 장사 무릉', '도겸': '하비 낭야 광릉 소패', '공융': '북해 평원', '마등': '무위 천수', '한수': '금성 안정', '유언': '성도 자동 영안 강주', '유비': '제북'},
    'historical-194': {'이각': '장안 낙양 홍농', '조조': '진류 허창 여남 하내', '여포': '복양 소패', '원소': '업 남피 중산 거록', '원술': '수춘 완', '공손찬': '북평 계 양평', '유비': '하비 낭야 광릉', '공융': '북해 평원', '유표': '양양 강릉 장사 무릉', '유장': '성도 자동 영안 강주', '마등': '무위 천수', '한수': '금성 안정', '유요': '오 회계', '손책': '여강 건업'},
    'historical-208': {'조조': '업 허창 장안 낙양 남피 진류 복양 하비 수춘 북해 완 양양 한성 계 중산 거록 감릉 진양 상당 평원 낭야 광릉 제북 소패 초 여남 하내 홍농', '손권': '예장 건업 오 회계 장사 여강 건안', '유비': '강하 강릉 신야 무릉 계양 영릉', '유장': '성도 자동 영안 강주 장가 월수 건녕 영창', '장로': '한중 상용 무도', '한수': '금성 안정', '마초': '무위 천수', '공손강': '양평 북평', '사섭': '교지 남해 합포'},
    'historical-214': {'조조': '업 허창 장안 낙양 남피 진류 복양 하비 수춘 북해 완 양양 한성 천수 계 중산 거록 감릉 진양 상당 평원 낭야 광릉 제북 소패 초 여남 하내 홍농', '손권': '건업 예장 오 회계 장사 여강 건안', '유비': '성도 자동 영안 강주 강릉 강하 신야 장가 월수 건녕 영창 무릉 계양 영릉', '장로': '한중 상용 무도', '한수': '금성 안정 무위', '공손강': '양평 북평', '사섭': '교지 남해 합포'},
}

PORT_PARENT = {'계항': '계', '북해항': '북해', '광릉항': '광릉', '하구항': '강하', '강하항': '강하',
               '강릉항': '강릉', '건업항': '건업', '회계항': '회계', '남해항': '남해', '합포항': '교지'}

def generate(spec):
    identifier, title, year, month, rulers = spec
    assert len({home for _, _, home in rulers}) == len(rulers)
    colors = ['#bd4139', '#2878b7', '#4f9b62', '#bc8840', '#965fb5', '#4a9895', '#b77280', '#8b9851', '#7877aa', '#ab6743', '#5995aa', '#88765b', '#b6a36e', '#786f8f']
    factions = [{'Id': index, 'Name': name, 'RulerGeneralId': HEROES[ruler]['id'], 'Color': colors[(index - 1) % len(colors)], 'Playable': True} for index, (name, ruler, _) in enumerate(rulers, 1)]
    homes = {SITES[home]['Id']: index for index, (_, _, home) in enumerate(rulers, 1)}
    territory = dict(homes)
    for index, (name, _, _) in enumerate(rulers, 1):
        for city_name in TERRITORIES.get(identifier, {}).get(name, '').split():
            if city_name in SITES:
                territory.setdefault(SITES[city_name]['Id'], index)
    if '다리강가' in SITES:
        territory.pop(SITES['다리강가']['Id'], None)
    for port, parent in PORT_PARENT.items():
        if port in SITES and parent in SITES and SITES[parent]['Id'] in territory:
            territory[SITES[port]['Id']] = territory[SITES[parent]['Id']]
    cities = []
    for site in MAP['Sites']:
        if site['Kind'] not in ('city', 'port', 'gate'):
            continue
        owner = territory.get(site['Id'], 0)
        wall = (3000 if site['Size'] == 'medium' else 2000) if site['Kind'] == 'gate' else (
            600 if site['Kind'] == 'port' else {'small': 600, 'medium': 1200, 'large': 2000}[site['Size']])
        cities.append({'SiteId': site['Id'], 'OwnerFactionId': owner, 'Gold': 2400 if owner else 0,
                       'Provisions': 18000 if owner else 0, 'Wall': wall,
                       'Security': 80 if owner else 70,
                       'Garrisons': [{'TroopCode': 'swordsman', 'Troops': 10000}, {'TroopCode': 'archer', 'Troops': 6000}] if owner else [],
                       'Ships': [{'ShipCode': 'small_boat', 'Count': 2}] if owner and site['Kind'] == 'port' else []})
    postings = []
    assigned = set()
    ruler_ids = {HEROES[ruler]['id'] for _, ruler, _ in rulers}
    for index, (_, ruler, home) in enumerate(rulers, 1):
        candidates = [ruler] + FOLLOWERS.get(ruler, [])
        if identifier == 'historical-190' and ruler == '유비':
            candidates = ['유비', '관우', '장비']
        if identifier == 'historical-214' and ruler == '유비':
            candidates = [name for name in candidates if name != '방통'] + ['마초', '엄안']
        if ruler == '하진':
            candidates += ['조조', '유비', '손견']
        candidates = [name for name in candidates
                      if name not in SCENARIO_EXCLUSIONS.get(identifier, {}).get(ruler, set())]
        candidates += SCENARIO_ADDITIONS.get(identifier, {}).get(ruler, [])
        for name in candidates:
            general = HEROES.get(name)
            if not general or general['id'] in assigned or (name != ruler and general['id'] in ruler_ids) or (identifier != 'fictional-revival' and general['birth'] > year - 14):
                continue
            posting_city = home
            if identifier == 'historical-214' and ruler == '유비':
                posting_city = {'관우': '강릉', '마량': '강릉', '장비': '영안',
                                '황충': '자동', '위연': '자동', '엄안': '강주'}.get(name, home)
            postings.append({'GeneralId': general['id'], 'FactionId': index, 'SiteId': SITES[posting_city]['Id']})
            assigned.add(general['id'])
    for faction in factions:
        if identifier == 'historical-214' and faction['Name'] == '유비':
            continue
        owned = [city['SiteId'] for city in cities if city['OwnerFactionId'] == faction['Id']]
        ruler_posting = next(post for post in postings if post['GeneralId'] == faction['RulerGeneralId'])
        destinations = [site_id for site_id in owned if site_id != ruler_posting['SiteId']]
        followers = [post for post in postings if post['FactionId'] == faction['Id']
                     and post['GeneralId'] != faction['RulerGeneralId']]
        for number, post in enumerate(followers):
            post['SiteId'] = (destinations + [ruler_posting['SiteId']])[number % len(owned)]
    assert all(any(p['GeneralId'] == f['RulerGeneralId'] for p in postings) for f in factions), title
    return {'Version': 1, 'Id': identifier, 'Title': title,
            'Description': '유비의 익주 평정과 삼국 대립' if year == 214 else title,
            'StartYear': year, 'StartMonth': month, 'StartDay': 1,
            'MapId': 'default-east-asia', 'MapRevision': REVISION,
            'Factions': factions, 'Cities': cities, 'Generals': postings}

if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for spec in SCENARIOS:
        draft = generate(spec)
        (OUT / (draft['Id'] + '.json')).write_text(json.dumps(draft, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(draft['Id'], len(draft['Factions']), len(draft['Generals']))
