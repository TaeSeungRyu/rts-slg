namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

/// <summary>
/// data/troop-types.json을 읽어 병종 템플릿 목록으로 매핑한다. 엔진 비의존(System.IO만).
/// </summary>
public sealed class TroopTypeLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<TroopTemplate> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "troop-types.json")));

    public IReadOnlyList<TroopTemplate> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<TroopTypeDto>>(json, Options)
            ?? throw new InvalidDataException("병종 데이터를 역직렬화할 수 없습니다.");

        return dtos
            .Select(d => new TroopTemplate(d.Code, d.Name, ParseClass(d.Class), d.AtkUnit, d.AtkBuilding, d.Df))
            .ToList();
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

    private sealed class TroopTypeDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Class { get; init; } = "";
        public int AtkUnit { get; init; }
        public int AtkBuilding { get; init; }
        public int Df { get; init; }
    }
}
