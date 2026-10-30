"""Build a city-by-city candidate list from the ROTK XIV officer roster.

This is a planning aid. It does not modify game data or claim that every proposed
city was an officer's historical posting. Run with Python 3; network is required.
"""

from __future__ import annotations

import html
import io
import json
import re
import urllib.request
import zipfile
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "SanguoSLG.Game" / "assets"
OUTPUT = ROOT / "doc" / "plan-scenario-sub-generals.md"
ROSTER_URL = "https://xgodgame.blogspot.com/2020/02/14_9.html"
UNIHAN_URL = "https://www.unicode.org/Public/UCD/latest/ucd/Unihan.zip"

RULERS = {
    "장각": "張角", "하진": "何進", "동탁": "董卓", "원소": "袁紹",
    "원술": "袁術", "조조": "曹操", "공손찬": "公孫瓚", "유표": "劉表",
    "도겸": "陶謙", "공융": "孔融", "마등": "馬騰", "한수": "韓遂",
    "유언": "劉焉", "유비": "劉備", "이각": "李傕", "여포": "呂布",
    "유장": "劉璋", "유요": "劉繇", "손책": "孫策", "손권": "孫權",
    "장로": "張魯", "마초": "馬超", "공손강": "公孫康", "사섭": "士燮",
}
REGIONAL_CITIES = {
    "유비": ("平原", "小沛", "下邳", "北海", "新野", "江陵", "成都"),
    "도겸": ("下邳", "小沛", "琅邪", "廣陵", "北海"),
    "공융": ("北海", "平原", "東萊"),
    "한수": ("天水", "武威", "安定", "西平"),
    "마초": ("天水", "武威", "安定", "西平"),
    "마등": ("天水", "武威", "安定", "西平"),
    "공손강": ("襄平", "北平", "薊", "遼東"),
    "공손찬": ("襄平", "北平", "薊", "遼東"),
    "사섭": ("交趾", "南海", "建安"),
    "장로": ("漢中", "上庸", "天水"),
}


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    return urllib.request.urlopen(request, timeout=45).read()


NAME_OVERRIDES = {"田豫": "전예", "王朗": "왕랑", "張嶷": "장억"}


def unihan_readings() -> dict[str, list[str]]:
    archive = zipfile.ZipFile(io.BytesIO(fetch(UNIHAN_URL)))
    result = {}
    for line in archive.read("Unihan_Readings.txt").decode().splitlines():
        if "\tkHangul\t" not in line:
            continue
        codepoint, _, readings = line.split("\t")
        result[chr(int(codepoint[2:], 16))] = [item.split(":")[0] for item in readings.split()]
    return result


def rows(source: str) -> list[list[list[str]]]:
    result = []
    for table in re.findall(r"<table\b[^>]*>.*?</table>", source, re.I | re.S)[:2]:
        parsed = []
        for tr in re.findall(r"<tr\b[^>]*>(.*?)</tr>", table, re.I | re.S)[1:]:
            cells = re.findall(r"<t[dh]\b[^>]*>(.*?)</t[dh]>", tr, re.I | re.S)
            parsed.append([html.unescape(re.sub(r"<[^>]+>", "", cell)).strip() for cell in cells])
        result.append(parsed)
    assert len(result) == 2 and len(result[0]) > 900
    return result


def korean(text: str, readings: dict[str, list[str]]) -> str:
    if text in NAME_OVERRIDES:
        return NAME_OVERRIDES[text]
    syllables = []
    for index, character in enumerate(text):
        choices = readings.get(character, [character])
        rieul = [choice for choice in choices if "가" <= choice <= "힣" and (ord(choice) - 0xAC00) // 588 == 5]
        non_rieul = [choice for choice in choices if choice not in rieul]
        syllables.append((non_rieul or choices)[0] if index == 0 else (rieul or choices)[0])
    value = "".join(syllables)
    for old, new in (("류", "유"), ("리", "이"), ("녀", "여"), ("녕", "영"),
                     ("료", "요"), ("려", "여"), ("렴", "염"), ("릉", "능"),
                     ("륙", "육"), ("로", "노"), ("락", "낙"), ("량", "양")):
        if value.startswith(old):
            value = new + value[len(old):]
    return value


def ruler_pool(year: int, ruler: str, first: dict, second: dict) -> list[tuple[int, str]]:
    chinese = RULERS[ruler]
    aliases = {
        "마초": ["馬騰"], "유언": ["劉璋"], "유장": ["劉焉"],
        "손책": ["孫堅"], "공손강": ["公孫度", "公孫恭"],
        "한수": ["馬騰"], "하진": ["何進"], "유비": ["劉表"],
        "조조": ["曹丕"],
    }
    choices = []
    for name, record in first.items():
        birth, debut, death, positions = record
        if (year and (debut > year or death < year)) or name == chinese:
            continue
        if year == 184:
            affiliations = [(positions[0], 0)]
        elif year == 190:
            affiliations = [(positions[1], 0), (positions[2], 1), (positions[4], 2)]
        elif year == 194:
            affiliations = [(positions[2], 0), (positions[4], 1)]
        elif year == 208:
            affiliations = [(positions[4], 0), (second.get(name, ""), 1)]
        elif year == 214:
            affiliations = [(second.get(name, ""), 0), (positions[4], 1)]
        else:
            affiliations = [(positions[2], 0), (positions[4], 1), (second.get(name, ""), 2)]
        score = -1
        for affiliation, distance in affiliations:
            if affiliation == chinese:
                score = max(score, 100 - distance * 15)
            elif affiliation in aliases.get(ruler, []):
                score = max(score, 70 - distance * 15)
        if score >= 0:
            choices.append((score, name))
    return sorted(choices, key=lambda entry: (-entry[0], entry[1]))


def fallback_pool(year: int, ruler: str, first: dict, second: dict) -> list[tuple[int, str]]:
    regions = REGIONAL_CITIES.get(ruler, ())
    result = []
    for name, record in first.items():
        _, debut, death, positions = record
        if year and not debut <= year <= death:
            continue
        if name in RULERS.values():
            continue
        near = [positions[0 if year == 184 else 1 if year == 190 else 2 if year == 194 else 4]]
        if year >= 214 or year == 0:
            near.append(second.get(name, ""))
        neutral = any("(" in location for location in near)
        if not neutral:
            continue
        regional = any(any(location.startswith(city) for city in regions) for location in near)
        if regions and not regional:
            continue
        # Prefer independent officers in the ruler's region to another ruler's staff.
        score = (30 if regional else 0) + 10
        result.append((score, name))
    return sorted(result, key=lambda entry: (-entry[0], entry[1]))


def main() -> None:
    readings = unihan_readings()
    first_rows, second_rows = rows(fetch(ROSTER_URL).decode())
    first = {}
    for row in first_rows:
        if len(row) != 9:
            continue
        try:
            first[row[0]] = (int(row[1]), int(row[2]), int(row[3]), row[4:9])
        except ValueError:
            pass
    second = {row[0]: row[4] for row in second_rows if len(row) == 9}
    generals = {record["id"]: record["name"] for record in json.loads((ROOT / "data" / "generals.json").read_text(encoding="utf-8-sig"))}
    existing_names = set(generals.values())
    sites = {site["Id"]: site for site in json.loads((ASSETS / "maps" / "east-asia-user-preset.json").read_text(encoding="utf-8-sig"))["Sites"]}
    lines = [
        "# 시나리오별 추가 장수 생성 후보",
        "",
        "기준: 현재 기본 지도, `data/generals.json`, 기본 시나리오 6개. **항구는 제외**하고, 현재 소유한 각 성에 **새 장수 3명씩**을 추가로 만드는 목표다. 이 문서는 생성 후보 목록이며 장수 데이터나 시작 배치를 변경하지 않는다.",
        "",
        "이름은 [삼국지14 무장 명감](https://www.gamecity.ne.jp/sangokushi14/officers-list.html), 등장·생몰 시기와 세력은 [삼국지14 시나리오별 무장 등장표](https://xgodgame.blogspot.com/2020/02/14_9.html)를 참고했다. [삼국지10의 조운 소개](https://www.gamecity.ne.jp/sangokushi/introduction/contents1/choun.htm)와 [삼국지12의 마초 소개](https://www.gamecity.ne.jp/sangokushi/12/bushoumeikan/0025_bachou.htm)도 초기 세력 관계를 교차 확인하는 데 사용했다. 소속은 가까운 연도 시나리오를 우선 참고했으며 **각 성에 대한 배치는 게임 균형을 위한 제안**이다. 역사적 주둔지를 확정한 것이 아니다. `△`는 해당 연도 세력 소속을 직접 확인하지 못한 후보(전후 연도 소속 또는 해당 지역의 재야 장수)이므로 생성 전에 검토한다. `창작 장수 슬롯`은 삼국지14 명단만으로 해당 세력에 적합한 실존 후보가 모자라는 경우이며, 기존 세력의 장수를 임의로 옮겨 채우지 않았다. 한글 음은 [유니코드 Unihan 읽기 자료](https://www.unicode.org/Public/UCD/latest/ucd/Unihan.zip)의 한자음을 사용했으며 일부 통용 표기와 다를 수 있다.",
        "",
        "같은 시나리오 안에서는 한 장수를 두 성에 중복 배치하지 않는다. 다른 연도 시나리오에서는 같은 장수 데이터를 재사용한다. 이미 게임 데이터에 있는 장수는 신규 생성 후보에서 제외한다. `가상, 영웅부활`은 역사적 출생·사망 시기 필터를 적용하지 않는다.",
        "",
    ]
    report = []
    scenario_files = [ASSETS / "scenarios" / f"historical-{year}.json" for year in (184, 190, 194, 208, 214)]
    scenario_files.append(ASSETS / "scenarios" / "fictional-revival.json")
    for file in scenario_files:
        scenario = json.loads(file.read_text(encoding="utf-8-sig"))
        year = scenario["StartYear"] if file.stem != "fictional-revival" else 0
        faction_by_id = {f["Id"]: generals[f["RulerGeneralId"]] for f in scenario["Factions"]}
        owned_cities = defaultdict(list)
        for city in scenario["Cities"]:
            if city["OwnerFactionId"] and sites[city["SiteId"]]["Kind"] == "city":
                owned_cities[city["OwnerFactionId"]].append(city["SiteId"])
        existing_by_city = Counter(g["SiteId"] for g in scenario["Generals"])
        scenario_existing = {generals[g["GeneralId"]] for g in scenario["Generals"]}
        used = set()
        used_korean = set()
        missing = []
        stats = Counter()
        lines.extend([f"## {scenario['Title']}", "", "| 세력 | 성 | 현재 장수 | 신규 생성 후보 3명 |", "| --- | --- | ---: | --- |"])
        for faction in scenario["Factions"]:
            faction_id = faction["Id"]
            ruler = faction_by_id[faction_id]
            pool = ruler_pool(year, ruler, first, second)
            city_ids = sorted(owned_cities[faction_id], key=lambda site_id: (existing_by_city[site_id], sites[site_id]["Name"]))
            for site_id in city_ids:
                chosen = []
                for score, name in pool:
                    translated = korean(name, readings)
                    if (name in used or translated in used_korean or translated in existing_names or translated in scenario_existing
                            or not re.fullmatch(r"[가-힣]+", translated) or name.endswith("氏")):
                        continue
                    if translated in {korean(candidate, readings) for _, candidate in chosen}:
                        continue
                    chosen.append((score, name))
                    used.add(name)
                    used_korean.add(translated)
                    if len(chosen) == 3:
                        break
                while len(chosen) < 3:
                    # A cross-faction filler is explicitly flagged; never imply historical allegiance.
                    other = next(((score, name) for score, name in fallback_pool(year, ruler, first, second)
                                  if name not in used and korean(name, readings) not in used_korean
                                  and korean(name, readings) not in existing_names and korean(name, readings) not in scenario_existing
                                  and re.fullmatch(r"[가-힣]+", korean(name, readings)) and not name.endswith("氏")), None)
                    if other is None:
                        missing.append((ruler, site_id, len(chosen)))
                        break
                    chosen.append(other)
                    used.add(other[1])
                    used_korean.add(korean(other[1], readings))
                labels = [f"{korean(name, readings)} ({name})" + (" △" if score < 100 and year else "") for score, name in chosen]
                labels.extend(f"{sites[site_id]['Name']} 창작 장수 슬롯 {len(labels) + 1}" for _ in range(3 - len(labels)))
                stats["cities"] += 1
                stats["proposals"] += len(chosen)
                stats["slots"] += 3 - len(chosen)
                stats["review"] += sum(score < 100 and year != 0 for score, _ in chosen)
                lines.append(f"| {ruler} | {sites[site_id]['Name']} | {existing_by_city[site_id]} | {' · '.join(labels)} |")
        lines.extend(["", f"소유 성 **{stats['cities']}개**, 실명 신규 후보 **{stats['proposals']}명**, 창작 장수 슬롯 **{stats['slots']}개** (소속·연도 재검토 표시 {stats['review']}명).", ""])
        report.append((file.stem, stats, missing))
    summary = ["## 규모", "", "| 시나리오 | 소유 성 | 신규 실명 후보 | 창작 장수 슬롯 |", "| --- | ---: | ---: | ---: |"]
    for name, stats, _ in report:
        summary.append(f"| {name} | {stats['cities']} | {stats['proposals']} | {stats['slots']} |")
    summary.extend(["", "같은 장수를 여러 연도 시나리오에서 재사용하므로 열의 수를 합산해 서로 다른 장수 데이터 수로 해석하지 않는다.", ""])
    first_section = next(index for index, line in enumerate(lines) if line.startswith("## "))
    lines[first_section:first_section] = summary
    OUTPUT.write_text("\n".join(lines), encoding="utf-8")
    for name, stats, missing in report:
        print(name, dict(stats), "missing", missing[:5])


if __name__ == "__main__":
    main()
