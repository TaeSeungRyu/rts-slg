namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Simulation;

/// <summary>data/stratagems.json을 읽어 계략 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class StratagemLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<Stratagem> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "stratagems.json")));

    public IReadOnlyList<Stratagem> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<StratagemDto>>(json, Options)
            ?? throw new InvalidDataException("계략 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(d => new Stratagem(
            d.Code, d.Name, ParseKind(d.EffectKind), d.RequiredLevel, d.Cost,
            d.BaseValue, d.Duration, d.Range, ParseTerrain(d.TerrainRule),
            ParseStatus(d.StatusKind), ParsePurge(d.PurgeScope))).ToList();
    }

    private static StratagemEffectKind ParseKind(string name) => name switch
    {
        "instant_damage" => StratagemEffectKind.InstantDamage,
        "damage_over_time" => StratagemEffectKind.DamageOverTime,
        "debuff" => StratagemEffectKind.Debuff,
        "purge" => StratagemEffectKind.Purge,
        _ => throw new InvalidDataException($"알 수 없는 계략 효과: {name}"),
    };

    private static StratagemTerrainRule ParseTerrain(string name) => name switch
    {
        "none" => StratagemTerrainRule.None,
        "river_only" => StratagemTerrainRule.RiverOnly,
        "river_forbidden" => StratagemTerrainRule.RiverForbidden,
        _ => throw new InvalidDataException($"알 수 없는 지형 조건: {name}"),
    };

    private static StatusKind? ParseStatus(string? name) => name switch
    {
        null or "" => null,
        "burn" => StatusKind.Burn,
        "poison" => StatusKind.Poison,
        "attack_down" => StatusKind.AttackDown,
        "ranged_down" => StatusKind.RangedDown,
        "nullify" => StatusKind.Nullify,
        _ => throw new InvalidDataException($"알 수 없는 상태 종류: {name}"),
    };

    private static PurgeScope ParsePurge(string? name) => name switch
    {
        null or "" or "none" => PurgeScope.None,
        "fire" => PurgeScope.Fire,
        "non_fire" => PurgeScope.NonFire,
        _ => throw new InvalidDataException($"알 수 없는 정화 범위: {name}"),
    };

    private sealed class StratagemDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string EffectKind { get; init; } = "";
        public int RequiredLevel { get; init; }
        public int Cost { get; init; }
        public int BaseValue { get; init; }
        public int Duration { get; init; }
        public int Range { get; init; }
        public string TerrainRule { get; init; } = "none";
        public string? StatusKind { get; init; }
        public string? PurgeScope { get; init; }
    }
}
