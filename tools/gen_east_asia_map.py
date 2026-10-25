"""Generate the scenario terrain preview from Natural Earth 1:50m land geometry."""

import json
import math
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "SanguoSLG.Game/assets/maps/east-asia-map.json"
LAND_URL = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_land.geojson"
WEST, EAST, SOUTH, NORTH = 91.0, 133.0, 18.0, 54.0
WIDTH, HEIGHT = 100, 70


def rings_of(geometry):
    coordinates = geometry["coordinates"]
    polygons = [coordinates] if geometry["type"] == "Polygon" else coordinates
    for polygon in polygons:
        outer = polygon[0]
        bounds = (min(p[0] for p in outer), max(p[0] for p in outer),
                  min(p[1] for p in outer), max(p[1] for p in outer))
        if bounds[1] >= WEST and bounds[0] <= EAST and bounds[3] >= SOUTH and bounds[2] <= NORTH:
            yield (bounds, polygon)


def inside_ring(lon, lat, ring):
    inside = False
    previous = ring[-1]
    for current in ring:
        x1, y1 = previous
        x2, y2 = current
        if (y1 > lat) != (y2 > lat):
            crossing = x1 + (lat - y1) * (x2 - x1) / (y2 - y1)
            if lon < crossing:
                inside = not inside
        previous = current
    return inside


def on_land(lon, lat, polygons):
    for bounds, rings in polygons:
        if bounds[0] <= lon <= bounds[1] and bounds[2] <= lat <= bounds[3]:
            if inside_ring(lon, lat, rings[0]) and not any(
                    inside_ring(lon, lat, hole) for hole in rings[1:]):
                return True
    return False


MOUNTAIN_CHAINS = [
    ([(75, 34), (84, 32), (92, 30), (100, 29)], 1.1),
    ([(78, 43), (86, 42), (94, 42)], 1.3),
    ([(80, 36), (91, 36), (102, 35)], 1.1),
    ([(87, 49), (92, 48), (99, 49)], 1.0),
    ([(98, 25), (101, 27), (103, 30), (104, 33)], 1.4),
    ([(104, 34), (109, 33), (113, 33)], 1.0),
    ([(111, 39), (114, 36), (115, 33)], 0.75),
    ([(109, 25), (113, 25), (117, 26)], 0.8),
    ([(127.6, 39), (128.5, 36), (129.2, 34)], 0.65),
    ([(130, 33), (131, 32), (132.5, 34)], 0.55),
]
RIVERS = [
    [(96, 35), (101, 36), (105, 37), (108, 39), (111, 40), (111, 37), (114, 35), (117, 36), (119, 37)],
    [(91, 32), (97, 31), (101, 29), (105, 29), (108, 30), (111, 30), (114, 30.5), (118, 31), (121, 31.4)],
    [(106, 24), (109, 23), (112, 23), (114, 23), (115, 22.7)],
    [(116, 43), (120, 43), (123, 43.5), (126, 44), (130, 45)],
]


def distance_to_segment(lon, lat, first, second):
    ax, ay = first
    bx, by = second
    scale = math.cos(math.radians(lat))
    dx, dy = (bx - ax) * scale, by - ay
    t = max(0.0, min(1.0, (((lon - ax) * scale * dx + (lat - ay) * dy)
                            / max(dx * dx + dy * dy, 0.00001))))
    return math.hypot((lon - ax - (bx - ax) * t) * scale,
                      lat - ay - (by - ay) * t)


def near_line(lon, lat, chain, radius):
    return any(distance_to_segment(lon, lat, a, b) <= radius
               for a, b in zip(chain, chain[1:]))


def terrain(lon, lat, column, row):
    noise = ((column * 92821 + row * 68917 + column * row * 13) % 997) / 997
    if (lon < 104 and 27 < lat < 38 and (lat < 35 or lon < 97)):
        return "M" if noise < 0.62 else "H"
    if any(near_line(lon, lat, chain, width) for chain, width in MOUNTAIN_CHAINS):
        return "M" if noise < 0.83 else "H"
    if (78 < lon < 91 and 37 < lat < 42) or (93 < lon < 113 and 40 < lat < 46):
        return "D" if noise < 0.88 else "P"
    if any(near_line(lon, lat, river, 0.28) for river in RIVERS):
        return "R"
    if (lat < 34 and lon > 102 or 30 < lat < 40 and 112 < lon < 122
            or 124 < lon and lat < 41) and noise < 0.36:
        return "F"
    return "P"


def main():
    with urllib.request.urlopen(LAND_URL, timeout=45) as response:
        data = json.load(response)
    polygons = [item for feature in data["features"]
                for item in rings_of(feature["geometry"])]
    rows = []
    for row in range(HEIGHT):
        lat = NORTH - (row + 0.5) * (NORTH - SOUTH) / HEIGHT
        characters = []
        for column in range(WIDTH):
            lon = WEST + (column + 0.5) * (EAST - WEST) / WIDTH
            characters.append(terrain(lon, lat, column, row)
                              if on_land(lon, lat, polygons) else "O")
        rows.append(characters)
    for row in range(HEIGHT):
        for column in range(WIDTH):
            if rows[row][column] != "O":
                continue
            if any(0 <= row + dr < HEIGHT and 0 <= column + dc < WIDTH
                   and rows[row + dr][column + dc] not in "OW"
                   for dr in (-1, 0, 1) for dc in (-1, 0, 1)):
                rows[row][column] = "W"
    result = {
        "name": "동아시아 시나리오 기본 지형",
        "source": "Natural Earth 1:50m land (public domain); 산맥·하천·식생은 게임용 수작업 배치",
        "bounds": {"west": WEST, "east": EAST, "south": SOUTH, "north": NORTH},
        "width": WIDTH, "height": HEIGHT,
        "legend": {"P": "plains", "F": "forest", "M": "mountain", "H": "rock_mountain",
                   "D": "desert", "R": "river", "W": "water_shallow", "O": "water_deep"},
        "rows": ["".join(row) for row in rows],
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{OUTPUT}: {WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
