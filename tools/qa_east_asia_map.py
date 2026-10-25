import json
from collections import deque
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
    return 0 <= column < width and 0 <= row < height and rows[row][column] in "PFMDR"


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
assert bounds["west"] == 91 and bounds["east"] == 133
assert len(route((104.1, 30.7), (107.0, 33.1))) > 4
assert len(route((107.0, 33.1), (108.9, 34.3))) > 3
for latitude in (32.14, 32.66):
    corridor = [rows[row][column] for row in range(height) for column in range(width)
                if abs(coordinate(column, row)[1] - latitude) < .1
                and 104 <= coordinate(column, row)[0] <= 111]
    assert sum(kind in "PFMDR" for kind in corridor) <= 5, (latitude, corridor)
print("[east-asia-map-qa] passed: cropped bounds, Shu access and narrow Daba passes")
