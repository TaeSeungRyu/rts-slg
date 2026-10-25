"""Generate the scenario terrain preview from Natural Earth 1:50m land geometry."""

import json
import heapq
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
SHU_RIDGES = [
    ([(104.5, 34.4), (108.0, 34.3), (111.8, 34.0)], 0.68),
    ([(103.8, 32.7), (106.0, 32.35), (108.2, 32.3), (111.3, 31.8)], 0.73),
    ([(103.1, 29.2), (102.9, 31.2), (103.2, 33.4)], 0.55),
    ([(109.3, 29.2), (109.9, 30.4), (110.0, 32.0)], 0.48),
]
SHU_ROADS = [
    [(104.1, 30.7), (105.0, 31.4), (105.8, 32.3), (106.9, 33.1)],
    [(106.9, 33.1), (107.8, 34.1), (108.9, 34.3)],
    [(106.5, 29.6), (107.8, 30.1), (109.0, 30.6), (110.4, 30.8)],
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
    if 103.8 < lon < 108.2 and 29.8 < lat < 31.7:
        return "F" if noise < 0.15 else "P"
    if 106.0 < lon < 108.5 and 32.85 < lat < 33.55:
        return "P"
    if any(near_line(lon, lat, ridge, width) for ridge, width in SHU_RIDGES):
        return "M" if any(near_line(lon, lat, road, 0.32) for road in SHU_ROADS) else "H"
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


ROUTE_PAIRS = [
    ("장안—한중", (108.9, 34.3), (107.0, 33.1)),
    ("한중—성도", (107.0, 33.1), (104.1, 30.7)),
    ("성도—중경", (104.1, 30.7), (106.5, 29.6)),
    ("중경—의창", (106.5, 29.6), (111.3, 30.7)),
    ("의창—무한", (111.3, 30.7), (114.3, 30.6)),
    ("장안—낙양", (108.9, 34.3), (112.5, 34.6)),
    ("낙양—업", (112.5, 34.6), (114.5, 36.1)),
    ("업—북경", (114.5, 36.1), (116.4, 39.9)),
    ("무한—건업", (114.3, 30.6), (118.8, 32.1)),
    ("무한—광주", (114.3, 30.6), (113.3, 23.1)),
    ("건업—북경", (118.8, 32.1), (116.4, 39.9)),
]


def route_network(rows):
    def nearest(lon, lat):
        column = int((lon - WEST) / (EAST - WEST) * WIDTH)
        row = int((NORTH - lat) / (NORTH - SOUTH) * HEIGHT)
        options = ((abs(column - x) + abs(row - y), x, y)
                   for y in range(max(0, row - 3), min(HEIGHT, row + 4))
                   for x in range(max(0, column - 3), min(WIDTH, column + 4))
                   if rows[y][x] in "PFMDR")
        _, x, y = min(options)
        return (x, y)

    def neighbors(x, y):
        offset = -1 if x % 2 == 0 else 1
        for xx, yy in ((x, y - 1), (x, y + 1), (x - 1, y), (x + 1, y),
                       (x - 1, y + offset), (x + 1, y + offset)):
            if 0 <= xx < WIDTH and 0 <= yy < HEIGHT and rows[yy][xx] in "PFMDR":
                yield (xx, yy)

    result = []
    for name, start, finish in ROUTE_PAIRS:
        origin, target = nearest(*start), nearest(*finish)
        frontier = [(0, origin)]
        distances = {origin: 0}
        parents = {origin: None}
        while frontier:
            distance, current = heapq.heappop(frontier)
            if current == target:
                break
            if distance != distances[current]:
                continue
            for neighbor in neighbors(*current):
                x, y = neighbor
                cost = {"P": 10, "F": 16, "M": 24, "D": 15, "R": 14}[rows[y][x]]
                score = distance + cost
                if score < distances.get(neighbor, 10**9):
                    distances[neighbor] = score
                    parents[neighbor] = current
                    heapq.heappush(frontier, (score, neighbor))
        if target not in parents:
            raise ValueError(f"No land route: {name}")
        path = []
        current = target
        while current is not None:
            path.append(list(current))
            current = parents[current]
        result.append({"name": name, "tiles": path[::-1]})
    return result


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
        "land_routes": route_network(rows),
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{OUTPUT}: {WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
