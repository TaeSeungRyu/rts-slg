namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Simulation;

/// <summary>data/passive-skills.json을 읽어 패시브 스킬 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class PassiveSkillLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<PassiveSkill> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "passive-skills.json")));

    public IReadOnlyList<PassiveSkill> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<PassiveSkillDto>>(json, Options)
            ?? throw new InvalidDataException("패시브 스킬 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(d => new PassiveSkill(
            d.Code, d.Name, d.Grade,
            d.Effects.Select(e => new PassiveEffect(ParseBucket(e.Bucket), ParseCondition(e.Condition), e.Tiers)).ToList()))
            .ToList();
    }

    private static SkillBucket ParseBucket(string name) => name switch
    {
        "attack" => SkillBucket.Attack,
        "defense" => SkillBucket.Defense,
        _ => throw new InvalidDataException($"알 수 없는 버킷: {name}"),
    };

    private static PassiveCondition ParseCondition(string name) => name switch
    {
        "always" => PassiveCondition.Always,
        "target_building" => PassiveCondition.TargetBuilding,
        "target_unit" => PassiveCondition.TargetUnit,
        "rough" => PassiveCondition.Rough,
        "plains_desert" => PassiveCondition.PlainsDesert,
        "momentum" => PassiveCondition.Momentum,
        "pursuit" => PassiveCondition.Pursuit,
        "enemy_marching" => PassiveCondition.EnemyMarching,
        "melee" => PassiveCondition.Melee,
        "melee_incoming" => PassiveCondition.MeleeIncoming,
        "ranged_incoming" => PassiveCondition.RangedIncoming,
        "hp_below_half" => PassiveCondition.HpBelowHalf,
        "hp_above_half" => PassiveCondition.HpAboveHalf,
        "castle_garrison" => PassiveCondition.CastleGarrison,
        "surrounded" => PassiveCondition.Surrounded,
        "field" => PassiveCondition.Field,
        _ => throw new InvalidDataException($"알 수 없는 조건: {name}"),
    };

    private sealed class PassiveSkillDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Grade { get; init; } = "";
        public List<EffectDto> Effects { get; init; } = new();
    }

    private sealed class EffectDto
    {
        public string Bucket { get; init; } = "";
        public string Condition { get; init; } = "";
        public List<int> Tiers { get; init; } = new();
    }
}
