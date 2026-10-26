"""Generate the scenario terrain preview from Natural Earth 1:50m land geometry."""

import json
import heapq
import math
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "SanguoSLG.Game/assets/maps/east-asia-map.json"
CITY_SITES = ROOT / "data/east-asia-city-sites.json"
RTK13_CITY_SITES = ROOT / "data/east-asia-rtk13-cities.json"
LAND_URL = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_land.geojson"
WEST, EAST, SOUTH, NORTH = 97.72, 133.0, 18.0, 51.94285714285714 - 20 * (51.94285714285714 - 18.0) / 124
WIDTH, HEIGHT = 174, 115


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
    [(98, 31), (101, 29), (104, 28.8), (106, 29.5), (108.3, 30), (110.5, 30.6),
     (113, 30.7), (115, 30.4), (117.3, 30.8), (121.2, 31.3)],
    [(106, 24), (109, 23), (112, 23), (114, 23), (115, 22.7)],
    [(116, 43), (120, 43), (123, 43.5), (126, 44), (130, 45)],
]
YANGTZE_CROSSING = (106.5, 29.6)
WESTERN_RIVER_ROAD = [(104.1, 30.7), (103.1, 31.3), (101.5, 31.4),
                      (99.5, 31.7), (98.2, 31.8)]
CENTRAL_WEST_BANK_ROAD = [(114.3, 31.5), (112.8, 31.5), (111.3, 31.4),
                          (109.3, 31.2), (107.6, 31.0), (106.8, 30.7)]
CENTRAL_MOUNTAIN_PASS = [(108.9, 34.3), (110.8, 34.4), (112.5, 34.6)]
SOUTHWEST_RIVER_ROAD = [(106.5, 29.0), (105.2, 27.4), (103.5, 26.0), (102.7, 25.0)]
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
    if (near_line(lon, lat, WESTERN_RIVER_ROAD, 0.48)
            or near_line(lon, lat, CENTRAL_WEST_BANK_ROAD, 0.48)):
        return "M"
    if (near_line(lon, lat, CENTRAL_MOUNTAIN_PASS, 0.44)
            or near_line(lon, lat, SOUTHWEST_RIVER_ROAD, 0.44)):
        return "M"
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
    desert_radius = ((lon - 103.0) / 10.2) ** 2 + ((lat - 42.9) / 3.9) ** 2
    desert_edge = 1 + 0.15 * math.sin(lon * 1.7 + lat * 0.4) + 0.10 * math.cos(lon * 2.1 - lat)
    if desert_radius < desert_edge:
        return "D" if noise < max(0.35, 0.92 - desert_radius * 0.27) else "P"
    if any(near_line(lon, lat, river, 0.28) for river in RIVERS):
        return "R"
    if (lat < 34 and lon > 102 or 30 < lat < 40 and 112 < lon < 122
            or 124 < lon and lat < 41) and noise < 0.36:
        return "F"
    return "P"


ROUTE_PAIRS = [
    ("서부 강변—성도", (98.2, 31.8), (104.1, 30.7)),
    ("무한 북안—의창 북안", (114.3, 31.5), (111.3, 31.4)),
    ("의창 북안—중경 북안", (111.3, 31.4), (106.8, 30.7)),
    ("장안—한중", (108.9, 34.3), (107.0, 33.1)),
    ("한중—성도", (107.0, 33.1), (104.1, 30.7)),
    ("성도—중경", (104.1, 30.7), (106.5, 29.6)),
    ("중경—의창", (106.5, 29.6), (111.3, 30.7)),
    ("장안—낙양", (108.9, 34.3), (112.5, 34.6)),
    ("낙양—업", (112.5, 34.6), (114.5, 36.1)),
    ("업—북경", (114.5, 36.1), (116.4, 39.9)),
    ("무한—건업", (114.3, 30.6), (118.8, 32.1)),
    ("건업—북경", (118.8, 32.1), (116.4, 39.9)),
    ("북경—요동", (116.4, 39.9), (123.4, 41.1)),
    ("요동—평양", (123.4, 41.1), (125.7, 39.0)),
    ("평양—한성", (125.7, 39.0), (127.0, 37.5)),
    ("한성—부산", (127.0, 37.5), (129.0, 35.2)),
    ("성도—곤명", (104.1, 30.7), (102.7, 25.0)),
    ("곤명—남녕", (102.7, 25.0), (108.3, 22.8)),
    ("남녕—광주", (108.3, 22.8), (113.3, 23.1)),
]


def route_network(rows):
    def nearest(lon, lat):
        column = int((lon - WEST) / (EAST - WEST) * WIDTH)
        row = int((NORTH - lat) / (NORTH - SOUTH) * HEIGHT)
        options = ((abs(column - x) + abs(row - y), x, y)
                   for y in range(max(0, row - 3), min(HEIGHT, row + 4))
                   for x in range(max(0, column - 3), min(WIDTH, column + 4))
                   if rows[y][x] in "PFMDRB")
        _, x, y = min(options)
        return (x, y)

    def neighbors(x, y):
        offset = -1 if x % 2 == 0 else 1
        for xx, yy in ((x, y - 1), (x, y + 1), (x - 1, y), (x + 1, y),
                       (x - 1, y + offset), (x + 1, y + offset)):
            if 0 <= xx < WIDTH and 0 <= yy < HEIGHT and rows[yy][xx] in "PFMDRB":
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
                cost = {"P": 10, "F": 16, "M": 24, "D": 15, "R": 14, "B": 18}[rows[y][x]]
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


def city_positions(rows):
    with CITY_SITES.open(encoding="utf-8") as source:
        sites = [site for site in json.load(source) if site["region"] != "중국"]
    with RTK13_CITY_SITES.open(encoding="utf-8") as source:
        sites = [{"name": name, "size": size, "longitude": longitude,
                  "latitude": latitude, "region": "중국"}
                 for name, size, longitude, latitude in json.load(source)] + sites
    regions = {
        "중국": (98.0, 125.3, 21.0, 43.5),
        "대한민국": (125.6, 130.4, 34.0, 38.7),
        "대만": (120.0, 122.4, 21.8, 25.5),
        "일본": (129.2, 132.5, 31.6, 34.4),
    }
    result = []
    for site in sites:
        longitude, latitude = site["longitude"], site["latitude"]
        column = int((longitude - WEST) / (EAST - WEST) * WIDTH)
        row = int((NORTH - latitude) / (NORTH - SOUTH) * HEIGHT)
        options = []
        for y in range(max(0, row - 8), min(HEIGHT, row + 9)):
            for x in range(max(0, column - 8), min(WIDTH, column + 9)):
                lon = WEST + (x + 0.5) * (EAST - WEST) / WIDTH
                lat = NORTH - (y + 0.5) * (NORTH - SOUTH) / HEIGHT
                west, east, south, north = regions[site["region"]]
                if not (west <= lon <= east and south <= lat <= north):
                    continue
                if rows[y][x] not in "PFMD":
                    continue
                if any((x - city["tile"][0]) ** 2 + (y - city["tile"][1]) ** 2 < 9
                       for city in result):
                    continue
                neighbors = ((x, y - 1), (x, y + 1), (x - 1, y), (x + 1, y),
                             (x - 1, y + (-1 if x % 2 == 0 else 1)),
                             (x + 1, y + (-1 if x % 2 == 0 else 1)))
                surrounding_land = sum(0 <= xx < WIDTH and 0 <= yy < HEIGHT
                                       and rows[yy][xx] in "PFMDRB"
                                       for xx, yy in neighbors)
                if surrounding_land < {"small": 2, "medium": 4, "large": 5}[site["size"]]:
                    continue
                distance = ((lon - longitude) / ((EAST - WEST) / WIDTH)) ** 2 + (
                    (lat - latitude) / ((NORTH - SOUTH) / HEIGHT)) ** 2
                terrain_penalty = {"P": 0, "F": 0.4, "M": 2.5, "D": 1.2}[rows[y][x]]
                options.append((distance + terrain_penalty - surrounding_land * 0.03,
                                x, y, lon, lat))
        if not options:
            raise ValueError(f"No suitable city site: {site['name']}")
        _, x, y, lon, lat = min(options)
        result.append({"name": site["name"], "region": site["region"],
                       "size": site["size"], "tile": [x, y],
                       "longitude": round(lon, 4), "latitude": round(lat, 4)})
    return result


def port_positions(rows, cities):
    # Geographic approximations for the editable scenario, not an RTK13 port roster.
    seeds = [
        ("계항", "small", 118.0, 38.8), ("북해항", "small", 119.4, 36.8),
        ("광릉항", "medium", 119.5, 32.1), ("하구항", "small", 121.5, 31.1),
        ("강하항", "medium", 114.4, 30.6), ("강릉항", "small", 112.3, 30.2),
        ("건업항", "medium", 118.9, 31.8), ("회계항", "small", 120.7, 30.0),
        ("남해항", "medium", 113.5, 23.0), ("합포항", "small", 109.2, 21.6),
    ]
    result = []
    # Hex direction order agrees with the map editor's E, SE, SW, W, NW, NE.
    offsets_even = ((1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1))
    offsets_odd = ((1, 0), (1, 1), (0, -1), (-1, 0), (-1, -1), (0, 1))
    for name, size, longitude, latitude in seeds:
        column = int((longitude - WEST) / (EAST - WEST) * WIDTH)
        row = int((NORTH - latitude) / (NORTH - SOUTH) * HEIGHT)
        options = []
        for y in range(max(0, row - 12), min(HEIGHT, row + 13)):
            for x in range(max(0, column - 12), min(WIDTH, column + 13)):
                if rows[y][x] not in "PFMDB":
                    continue
                if any((x - city["tile"][0]) ** 2 + (y - city["tile"][1]) ** 2 < 16
                       for city in cities):
                    continue
                if any((x - port["tile"][0]) ** 2 + (y - port["tile"][1]) ** 2 < 16
                       for port in result):
                    continue
                for direction, (dx, dy) in enumerate(offsets_even if x % 2 == 0 else offsets_odd):
                    xx, yy = x + dx, y + dy
                    if not (0 <= xx < WIDTH and 0 <= yy < HEIGHT and rows[yy][xx] in "OWR"):
                        continue
                    distance = (x - column) ** 2 + (y - row) ** 2
                    options.append((distance, x, y, direction))
        if not options:
            raise ValueError(f"No suitable port site: {name}")
        _, x, y, direction = min(options)
        result.append({"name": name, "size": size, "tile": [x, y], "direction": direction})
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
            if on_land(lon, lat, polygons):
                characters.append("W" if near_line(lon, lat, RIVERS[1], 0.38)
                                  else terrain(lon, lat, column, row))
            else:
                characters.append("O")
        rows.append(characters)
    for row in range(HEIGHT):
        for column in range(WIDTH):
            if rows[row][column] != "O":
                continue
            if any(0 <= row + dr < HEIGHT and 0 <= column + dc < WIDTH
                   and rows[row + dr][column + dc] not in "OW"
                   for dr in (-1, 0, 1) for dc in (-1, 0, 1)):
                rows[row][column] = "W"
    for row in range(HEIGHT):
        lat = NORTH - (row + 0.5) * (NORTH - SOUTH) / HEIGHT
        for column in range(WIDTH):
            lon = WEST + (column + 0.5) * (EAST - WEST) / WIDTH
            if (rows[row][column] == "W"
                    and abs(lon - YANGTZE_CROSSING[0]) < 0.25
                    and abs(lat - YANGTZE_CROSSING[1]) < 0.75):
                rows[row][column] = "B"
    routes = route_network(rows)
    cities = city_positions(rows)
    ports = port_positions(rows, cities)
    reserved = {tuple(tile) for route in routes for tile in route["tiles"]}
    reserved.update((x, y) for city in cities
                    for y in range(max(0, city["tile"][1] - 3), min(HEIGHT, city["tile"][1] + 4))
                    for x in range(max(0, city["tile"][0] - 3), min(WIDTH, city["tile"][0] + 4)))
    reserved.update((x, y) for port in ports
                    for y in range(max(0, port["tile"][1] - 2), min(HEIGHT, port["tile"][1] + 3))
                    for x in range(max(0, port["tile"][0] - 2), min(WIDTH, port["tile"][0] + 3)))
    for row in range(HEIGHT):
        latitude = NORTH - (row + 0.5) * (NORTH - SOUTH) / HEIGHT
        for column in range(WIDTH):
            if (column, row) in reserved:
                continue
            sample = (column * 31 + row * 47 + column * row * 7) % 97
            current = rows[row][column]
            longitude = WEST + (column + 0.5) * (EAST - WEST) / WIDTH
            if (99.0 <= longitude <= 109.0 and 21.0 <= latitude <= 30.0
                    and current in "PFMDHN"):
                # Irregular southwest forest and wetlands, while routes and settlements stay intact.
                wetness = (column * 43 + row * 29 + column * row * 11) % 100
                if wetness < 27:
                    rows[row][column] = "N"
                elif wetness < 83:
                    rows[row][column] = "F"
                continue
            if current == "D" and sample < 7:
                rows[row][column] = "Q"
            elif current in "HM" and latitude > 36 and sample < 9:
                rows[row][column] = "I"
            elif current == "H" and sample < 7:
                rows[row][column] = "C" if sample < 4 else "X"
            elif current == "M" and sample < 4:
                rows[row][column] = "T"
            elif current == "P" and sample < 2 and any(
                    0 <= row + dy < HEIGHT and 0 <= column + dx < WIDTH
                    and rows[row + dy][column + dx] in "RW"
                    for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1))):
                rows[row][column] = "N"
    result = {
        "name": "동아시아 시나리오 기본 지형",
        "source": "Natural Earth 1:50m land (public domain); 산맥·하천·식생은 게임용 수작업 배치",
        "bounds": {"west": WEST, "east": EAST, "south": SOUTH, "north": NORTH},
        "width": WIDTH, "height": HEIGHT,
        "legend": {"P": "plains", "F": "forest", "M": "mountain", "H": "rock_mountain",
                   "D": "desert", "R": "river", "B": "river_crossing", "W": "water_shallow",
                   "O": "water_deep", "Q": "desert_cactus", "I": "ice_mountain", "C": "cliff",
                   "X": "karst", "T": "rock_hill", "N": "swamp"},
        "rows": ["".join(row) for row in rows],
        "land_routes": routes,
        "cities": cities,
        "ports": ports,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{OUTPUT}: {WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
