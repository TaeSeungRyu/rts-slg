import json
from collections import Counter, deque
from pathlib import Path

MAP_PATH = Path(__file__).resolve().parents[1] / "SanguoSLG.Game/assets/maps/east-asia-map.json"
data = json.loads(MAP_PATH.read_text(encoding="utf-8"))
rows = data["rows"]
width, height = data["width"], data["height"]
bounds = data["bounds"]


def cell(lon, lat):
    return (int((lon - bounds["west"]) / (bounds["east"] - bounds["west"]) * width),
            int((bounds["north"] - lat) / (bounds["north"] - bounds["south"]) * height))


def coordinate(column, row):
    return (bounds["west"] + (column + .5) * (bounds["east"] - bounds["west"]) / width,
            bounds["north"] - (row + .5) * (bounds["north"] - bounds["south"]) / height)


def passable(column, row):
    return 0 <= column < width and 0 <= row < height and rows[row][column] in "PFMDRB"


def neighbors(column, row):
    offset = -1 if column % 2 == 0 else 1
    return ((column, row - 1), (column, row + 1), (column - 1, row),
            (column + 1, row), (column - 1, row + offset), (column + 1, row + offset))


def route(start, end):
    origin, goal = cell(*start), cell(*end)
    assert passable(*origin) and passable(*goal), (origin, goal)
    queue = deque([origin])
    parent = {origin: None}
    while queue:
        current = queue.popleft()
        if current == goal:
            break
        for neighbor in neighbors(*current):
            if passable(*neighbor) and neighbor not in parent:
                parent[neighbor] = current
                queue.append(neighbor)
    assert goal in parent, (start, end)
    path = []
    current = goal
    while current is not None:
        path.append(current)
        current = parent[current]
    return list(reversed(path))


assert len(rows) == height and all(len(row) == width for row in rows)
assert bounds["west"] == 97.72 and bounds["east"] == 133
assert abs(bounds["north"] - ((54 - 6 * 36 / 105) - 20 * ((54 - 6 * 36 / 105) - 18) / 124)) < 1e-10
assert width == 174 and height == 115
crossing_tiles = [(column, row) for row in range(height) for column in range(width)
                  if rows[row][column] == "B"]
assert 1 <= len(crossing_tiles) <= 12
assert all(abs(coordinate(x, y)[0] - 106.5) < .25
           and abs(coordinate(x, y)[1] - 29.6) < .75
           for x, y in crossing_tiles)
assert rows[cell(106.5, 29.6)[1]][cell(106.5, 29.6)[0]] == "B"
for crossing in ((111.3, 30.7), (114.3, 30.6)):
    assert rows[cell(*crossing)[1]][cell(*crossing)[0]] == "W"
assert rows[cell(110.5, 30.6)[1]][cell(110.5, 30.6)[0]] == "W"
assert not passable(*cell(110.5, 30.6))
assert len(data["land_routes"]) >= 10
assert {"북경—요동", "요동—평양", "평양—한성", "한성—부산"}.issubset(
    {item["name"] for item in data["land_routes"]})
assert {"성도—곤명", "곤명—남녕", "남녕—광주"}.issubset(
    {item["name"] for item in data["land_routes"]})
assert "서부 강변—성도" in {item["name"] for item in data["land_routes"]}
for name in ("무한 북안—의창 북안", "의창 북안—중경 북안"):
    plotted = next(item for item in data["land_routes"] if item["name"] == name)
    assert len(plotted["tiles"]) < 35, (name, len(plotted["tiles"]))
    assert all(rows[y][x] != "B" for x, y in plotted["tiles"]), name
assert "의창—무한" not in {item["name"] for item in data["land_routes"]}
assert "무한—광주" not in {item["name"] for item in data["land_routes"]}
for plotted in data["land_routes"]:
    tiles = plotted["tiles"]
    assert len(tiles) >= 2 and all(passable(*tile) for tile in tiles), plotted["name"]
    assert all(tuple(right) in neighbors(*left) for left, right in zip(tiles, tiles[1:])), plotted["name"]
cities = data["cities"]
assert len(cities) == len({city["name"] for city in cities})
assert Counter(city["region"] for city in cities) == {
    "중국": 60, "대한민국": 5, "대만": 1, "일본": 2}
assert Counter(city["size"] for city in cities if city["region"] == "대한민국") == {
    "medium": 2, "small": 3}
assert all(city["size"] == "small" for city in cities if city["region"] == "일본")
assert all(passable(*city["tile"]) and rows[city["tile"][1]][city["tile"][0]] != "B"
           for city in cities)
assert all((left["tile"][0] - right["tile"][0]) ** 2
           + (left["tile"][1] - right["tile"][1]) ** 2 >= 9
           for index, left in enumerate(cities) for right in cities[index + 1:])
assert len(data["ports"]) == 10
assert all(rows[port["tile"][1]][port["tile"][0]] in "PFMDB"
           for port in data["ports"])
assert len(route((104.1, 30.7), (107.0, 33.1))) > 4
assert len(route((107.0, 33.1), (108.9, 34.3))) > 3
for latitude in (32.14, 32.66):
    corridor = [rows[row][column] for row in range(height) for column in range(width)
                if abs(coordinate(column, row)[1] - latitude) < .1
                and 104 <= coordinate(column, row)[0] <= 111]
    assert sum(kind in "PFMDRB" for kind in corridor) <= 10, (latitude, corridor)
assert {"Q", "I", "C", "X", "T", "N"}.issubset(set("".join(rows)))
desert_spread = [row.count("D") + row.count("Q") for row in rows]
assert len(set(desert_spread[:30])) > 10
print(f"[east-asia-map-qa] passed: bounds, routes, {len(cities)} city sites")
