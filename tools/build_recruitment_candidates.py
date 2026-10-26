import argparse
import json
from pathlib import Path


def required_officers():
    groups = {
        1: [8, 12, 14, 16, 17, 18, 19, 20, 22, 23, 24, 25, 28, 29, 30, 32, 33, 34, 35],
        2: [3, 4, 6, 9, 11, 42, 45, 46, 47, 48, 49, 51, 55, 56, 59, 60],
        3: [52, 53, 116],
        4: [54, 121],
        5: [103, 104, 115],
        6: [43, 126],
        7: [123],
        12: [39, 40, 119, 120],
        25: [26, 27, 124],
        41: [44, 127],
        57: [58],
        61: [62, 65, 66, 67],
        62: [7, 63, 64, 73, 122],
        63: [10, 68, 69, 70, 71, 74, 75, 76, 77, 78, 79, 80, 114, 129, 130],
        71: [72],
        81: [82, 83, 84],
        85: [21, 41],
        87: [88, 89, 90, 91, 111, 112],
        92: [93],
        95: [96, 97],
        98: [99],
        100: [101, 102],
        107: [113],
    }
    result = {}
    for leader, followers in groups.items():
        for follower in followers:
            if follower in result:
                raise ValueError(f"multiple required officers for {follower}: {result[follower]}, {leader}")
            result[follower] = leader
    return result


def update(source, generals):
    existing = {item["general"]: item for item in source}
    if len(existing) != len(source):
        raise ValueError("duplicate recruitment candidate")
    requirements = required_officers()
    ids = {general["id"] for general in generals}
    for general_id, leader in requirements.items():
        if general_id not in ids or leader not in ids or general_id == leader:
            raise ValueError(f"invalid required officer: {general_id} -> {leader}")
        visited = {general_id}
        next_id = leader
        while next_id in requirements:
            if next_id in visited:
                raise ValueError(f"cyclic required officer: {general_id}")
            visited.add(next_id)
            next_id = requirements[next_id]
    for general in generals:
        general_id = general["id"]
        if general_id > 131:
            continue
        item = existing.get(general_id)
        if item is None:
            quality = (general.get("might", 50) + general.get("intellect", 50)
                       + general.get("politics", 50)) // 3
            points = max(60, min(400, 60 + quality * 2))
            item = {
                "general": general_id,
                "type": "faction",
                "recruit_point_cost": points,
                "recruit_gold": points * 4,
                "title": general["name"],
                "desc": "장수 선행 조건에 따라 영입 가능한 인재",
            }
            source.append(item)
            existing[general_id] = item
        item.pop("unlock_year", None)
        item.pop("faction", None)
        item.pop("home_regions", None)
        item.pop("home_cities", None)
        item.pop("conditions", None)
        item.pop("wanderer_conditions", None)
        item["required_general"] = requirements.get(general_id)
    return sorted(source, key=lambda item: item["general"])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--generals", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8-sig"))
    generals = json.loads(args.generals.read_text(encoding="utf-8-sig"))
    result = update(source, generals)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
