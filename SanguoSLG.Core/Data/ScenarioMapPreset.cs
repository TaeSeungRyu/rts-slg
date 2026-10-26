namespace SanguoSLG.Core.Data;

using System.Text.Json;
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
            Rows.Any(row => row.Length != Width || row.Any(code => !"PFMHDRBWO".Contains(code))))
            throw new InvalidDataException("맵 크기, 버전 또는 지형 코드가 올바르지 않습니다.");
        var ids = new HashSet<int>();
        foreach (var site in Sites)
        {
            if (site.Id <= 0 || !ids.Add(site.Id) || string.IsNullOrWhiteSpace(site.Name) ||
                site.Direction is < 0 or > 5 || site.Column < 0 || site.Column >= Width ||
                site.Row < 0 || site.Row >= Height ||
                site.Kind is not ("city" or "port" or "facility"))
                throw new InvalidDataException("맵 배치물의 ID, 이름, 위치 또는 방향이 올바르지 않습니다.");
        }
        if (Sites.GroupBy(site => (site.Column, site.Row)).Any(group => group.Count() > 1))
            throw new InvalidDataException("같은 타일에 여러 배치물을 둘 수 없습니다.");
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
