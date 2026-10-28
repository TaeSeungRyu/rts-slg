namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record MapEditorSite(int Id, string Name, string Kind, string Size, int Column, int Row,
    int Direction = 0, int? ParentCityId = null);

public sealed record ScenarioMapPreset(int Version, int Width, int Height, IReadOnlyList<string> Rows,
    IReadOnlyList<MapEditorSite> Sites)
{
    public static readonly string Directions = "E SE SW W NW NE";

    public HexCoord ToAxial(int column, int row)
    {
        if (column < 0 || column >= Width || row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException(nameof(column));
        return new HexCoord(column, row - (column - (column & 1)) / 2);
    }

    public (int Column, int Row) ToOffset(HexCoord tile)
    {
        var row = tile.R + (tile.Q - (tile.Q & 1)) / 2;
        if (tile.Q < 0 || tile.Q >= Width || row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException(nameof(tile));
        return (tile.Q, row);
    }

    public ScenarioMapPreset Snapshot() => this with
    {
        Rows = Rows.ToArray(),
        Sites = Sites.ToArray(),
    };

    public void Validate()
    {
        if (Version != 1 || Width <= 0 || Height <= 0 || Rows.Count != Height ||
            Rows.Any(row => row.Length != Width || row.Any(code => !"PFMHDRBWOSTXCIELNQV".Contains(code))))
            throw new InvalidDataException("맵 크기, 버전 또는 지형 코드가 올바르지 않습니다.");
        var ids = new HashSet<int>();
        foreach (var site in Sites)
        {
            if (site.Id <= 0 || !ids.Add(site.Id) || string.IsNullOrWhiteSpace(site.Name) ||
                site.Direction is < 0 or > 5 || site.Column < 0 || site.Column >= Width ||
                site.Row < 0 || site.Row >= Height ||
                site.Kind is not ("city" or "port" or "facility" or "landmark" or "ruin" or "gate") ||
                site.Kind == "city" && site.Size is not ("small" or "medium" or "large") ||
                site.Kind == "port" && site.Size is not ("small" or "medium") ||
                site.Kind == "gate" && site.Size is not ("small" or "medium") ||
                site.Kind == "landmark" && site.Size != "waterfall_cliff" ||
                site.Kind == "ruin" && MapRuinCatalog.Find(site.Size) is null ||
                site.Kind == "facility" && site.Size is not ("paddy" or "farm" or "village" or "workshop"))
                throw new InvalidDataException("맵 배치물의 ID, 이름, 위치 또는 방향이 올바르지 않습니다.");
        }
        if (Sites.GroupBy(site => (site.Column, site.Row)).Any(group => group.Count() > 1))
            throw new InvalidDataException("같은 타일에 여러 배치물을 둘 수 없습니다.");
        if (Sites.Any(site => site.Kind == "facility" &&
            !Sites.Any(city => city.Kind == "city" && city.Id == site.ParentCityId)))
            throw new InvalidDataException("생산 시설에는 연결된 성이 필요합니다.");
        var occupied = new HashSet<HexCoord>();
        var coastDirections = new[] { new HexCoord(1, 0), new HexCoord(0, 1),
            new HexCoord(-1, 1), new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1) };
        foreach (var site in Sites)
        {
            var anchor = ToAxial(site.Column, site.Row);
            var offsets = site.Kind == "gate" && site.Size == "medium"
                ? new[] { new HexCoord(0, 0), new HexCoord(1, 0) }
                : site.Kind == "landmark"
                ? FeatureFootprint.OffsetsFor(FeatureType.WaterfallCliff)
                : site.Kind == "city" ? CastleFootprint.OffsetsFor(site.Size switch
            {
                "large" => CastleSize.Large,
                "medium" => CastleSize.Medium,
                _ => CastleSize.Small,
            }) : site.Kind == "port" && site.Size == "medium"
                ? FeatureFootprint.OffsetsFor(FeatureType.PortMedium)
                : [new HexCoord(0, 0)];
            var footprint = offsets.Select(offset => anchor + CastleFootprint.Rotate(offset, site.Direction)).ToArray();
            foreach (var tile in footprint)
            {
                var column = tile.Q;
                var row = tile.R + (column - (column & 1)) / 2;
                if (column < 0 || column >= Width || row < 0 || row >= Height ||
                    !(site.Kind == "landmark" ? "PFMDBSTNQHXC"
                        : site.Kind == "ruin" && MapRuinCatalog.Find(site.Size)!.Naval ? "WO"
                        : "PFMDBSTNQ")
                        .Contains(Rows[row][column]) || !occupied.Add(tile))
                    throw new InvalidDataException("배치물 발자국이 지도 밖, 물/험산 또는 다른 배치물과 겹칩니다.");
            }
            if (site.Kind == "port" && !footprint.Any(tile =>
            {
                var neighbor = tile + coastDirections[site.Direction];
                var column = neighbor.Q;
                var row = neighbor.R + (column - (column & 1)) / 2;
                return column >= 0 && column < Width && row >= 0 && row < Height
                    && Rows[row][column] is 'W' or 'O';
            }))
                throw new InvalidDataException("항구 부두는 인접 물 타일을 향해야 합니다.");
        }
        foreach (var facility in Sites.Where(site => site.Kind == "facility"))
        {
            var city = Sites.First(site => site.Id == facility.ParentCityId);
            var cityAnchor = ToAxial(city.Column, city.Row);
            var cityFootprint = CastleFootprint.OffsetsFor(city.Size switch
            {
                "large" => CastleSize.Large,
                "medium" => CastleSize.Medium,
                _ => CastleSize.Small,
            });
            if (!cityFootprint.Any(offset => (cityAnchor + CastleFootprint.Rotate(offset, city.Direction))
                .Distance(ToAxial(facility.Column, facility.Row)) <= 2))
                throw new InvalidDataException("생산 시설이 연결된 성에서 2칸을 넘었습니다.");
        }
    }
}

public static class ScenarioMapPresetStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static ScenarioMapPreset Load(string path)
    {
        var preset = JsonSerializer.Deserialize<ScenarioMapPreset>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("맵 프리셋을 읽을 수 없습니다.");
        preset.Validate();
        return preset.Snapshot();
    }

    public static void Save(string path, ScenarioMapPreset preset)
    {
        preset.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preset, Options));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
