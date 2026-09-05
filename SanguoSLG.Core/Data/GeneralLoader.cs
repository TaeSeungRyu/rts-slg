namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

/// <summary>
/// data/generals.json을 읽어 장수 목록으로 매핑한다(spec-general.md 사양). 엔진 비의존.
/// </summary>
public sealed class GeneralLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<General> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "generals.json")));

    public IReadOnlyList<General> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<GeneralDto>>(json, Options)
            ?? throw new InvalidDataException("장수 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(Map).ToList();
    }

    private static General Map(GeneralDto d) => new(
        new GeneralId(d.Id),
        d.Name,
        d.Aptitudes.ToDictionary(kv => ParseClass(kv.Key), kv => ParseGrade(kv.Value)),
        d.Might,
        d.Intellect,
        d.Politics,
        d.BattleActive,
        d.BattlePassives.Select(s => new GeneralSkill(s.Code, s.Tier)).ToList(),
        d.AdminPassives.Select(s => new GeneralSkill(s.Code, s.Tier)).ToList(),
        d.Birth,
        d.Region,
        d.Desc);

    private static TroopClass ParseClass(string value) => value switch
    {
        "infantry" => TroopClass.Infantry,
        "archer" => TroopClass.Archer,
        "cavalry" => TroopClass.Cavalry,
        "elephant" => TroopClass.Elephant,
        "siege" => TroopClass.Siege,
        "naval" => TroopClass.Naval,
        _ => throw new InvalidDataException($"알 수 없는 병종 분류: {value}"),
    };

    private static AptitudeGrade ParseGrade(string value) => value switch
    {
        "F" => AptitudeGrade.F,
        "D" => AptitudeGrade.D,
        "C" => AptitudeGrade.C,
        "B" => AptitudeGrade.B,
        "A" => AptitudeGrade.A,
        "A+" => AptitudeGrade.APlus,
        "S" => AptitudeGrade.S,
        "SS" => AptitudeGrade.SS,
        "SSS" => AptitudeGrade.SSS,
        _ => throw new InvalidDataException($"알 수 없는 통솔 등급: {value}"),
    };
}
