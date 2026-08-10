namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

/// <summary>data/special-units.json을 읽어 특수 유닛 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class SpecialUnitLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<SpecialUnit> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "special-units.json")));

    public IReadOnlyList<SpecialUnit> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<SpecialUnitDto>>(json, Options)
            ?? throw new InvalidDataException("특수 유닛 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(Map).ToList();
    }

    private static SpecialUnit Map(SpecialUnitDto d) => new(
        d.Code,
        d.Name,
        d.Base,
        d.DfOverride,
        d.BuildingAtkOverride,
        d.AtkBonusAll,
        d.AtkBonusBuilding,
        d.AtkBonusImpassable,
        MapClassMap(d.AtkBonusVsClass),
        MapClassMap(d.AttackerBonusFromClass));

    private static IReadOnlyDictionary<TroopClass, int> MapClassMap(Dictionary<string, int>? raw)
    {
        var result = new Dictionary<TroopClass, int>();
        if (raw is null)
        {
            return result;
        }

        foreach (var (key, value) in raw)
        {
            result[ParseClass(key)] = value;
        }

        return result;
    }

    private static TroopClass ParseClass(string name) => name switch
    {
        "infantry" => TroopClass.Infantry,
        "archer" => TroopClass.Archer,
        "cavalry" => TroopClass.Cavalry,
        "elephant" => TroopClass.Elephant,
        "siege" => TroopClass.Siege,
        "naval" => TroopClass.Naval,
        _ => throw new InvalidDataException($"알 수 없는 병종 분류: {name}"),
    };

    private sealed class SpecialUnitDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Base { get; init; } = "";
        public int? DfOverride { get; init; }
        public int? BuildingAtkOverride { get; init; }
        public int AtkBonusAll { get; init; }
        public int AtkBonusBuilding { get; init; }
        public int AtkBonusImpassable { get; init; }
        public Dictionary<string, int>? AtkBonusVsClass { get; init; }
        public Dictionary<string, int>? AttackerBonusFromClass { get; init; }
    }
}
