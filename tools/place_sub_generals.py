import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PLAN = (ROOT / 'doc/plan-scenario-sub-generals.md').read_text(encoding='utf-8')
GENERALS = {item['name']: item for item in json.loads((ROOT / 'data/generals.json').read_text(encoding='utf-8'))}
SITES = {item['Name']: item['Id'] for item in json.loads(
    (ROOT / 'SanguoSLG.Game/assets/maps/east-asia-user-preset.json').read_text(encoding='utf-8'))['Sites']}


def candidates(year):
    heading = '가상, 영웅부활' if year == 'fictional-revival' else f'{year}년'
    section = PLAN.split(f'## {heading}', 1)[1].split('\n## ', 1)[0]
    for row in section.splitlines():
        if not row.startswith('|') or row.startswith('| ---'):
            continue
        cells = [part.strip() for part in row.strip('|').split('|')]
        if len(cells) != 4 or cells[0] == '세력':
            continue
        names = [re.sub(r'\s*\([^)]*\).*', '', part).replace('△', '').strip()
                 for part in cells[3].split('·')]
        yield cells[0], cells[1], names


def main():
    for year in (184, 190, 194, 208, 214, 'fictional-revival'):
        file_id = year if isinstance(year, str) else f'historical-{year}'
        path = ROOT / f'SanguoSLG.Game/assets/scenarios/{file_id}.json'
        scenario = json.loads(path.read_text(encoding='utf-8'))
        faction_ids = {faction['Name']: faction['Id'] for faction in scenario['Factions']}
        if year == 184:
            faction_ids.update({'장각': faction_ids['황건군'], '하진': faction_ids['후한 조정']})
        owners = {site['SiteId']: site['OwnerFactionId'] for site in scenario['Cities']}
        roster = scenario['Generals']
        assigned = {item['GeneralId'] for item in roster}
        city_counts = Counter(item['SiteId'] for item in roster)
        added = 0
        for faction_name, city_name, names in candidates(year):
            site_id = SITES.get(city_name)
            faction_id = faction_ids.get(faction_name)
            if site_id is None or faction_id is None or owners.get(site_id) != faction_id:
                continue
            if any(item['SiteId'] == site_id and item['GeneralId'] >= 306 for item in roster):
                continue
            current = city_counts[site_id]
            budget = 2 if current == 0 else 1 if current <= 2 else 0
            for name in names:
                general = GENERALS.get(name)
                if budget <= 0 or general is None or general['id'] in assigned:
                    continue
                if isinstance(year, int) and general['birth'] > year - 16:
                    continue
                roster.append({'GeneralId': general['id'], 'FactionId': faction_id, 'SiteId': site_id})
                assigned.add(general['id'])
                city_counts[site_id] += 1
                budget -= 1
                added += 1
        path.write_text(json.dumps(scenario, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        all_new = {general['id'] for general in GENERALS.values() if general['id'] >= 306}
        print(f'{year}: added={added}, start={len(assigned)}, unlockable={len(all_new-assigned)}')


if __name__ == '__main__':
    main()
