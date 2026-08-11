namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Simulation;

/// <summary>data/active-skills.json을 읽어 액티브 스킬 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class ActiveSkillLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<ActiveSkill> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "active-skills.json")));

    public IReadOnlyList<ActiveSkill> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<ActiveSkillDto>>(json, Options)
            ?? throw new InvalidDataException("액티브 스킬 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(d => new ActiveSkill(
            d.Code, d.Name, ParseType(d.Type), d.Grade,
            d.DamageMultPercent, d.DefenderDfReductionPercent, d.ExecutePercent, d.ExecuteCapPercent,
            d.BuildingOnly, d.DamageReductionPercent, d.HealPercent, d.HealCapPercent)).ToList();
    }

    private static ActiveType ParseType(string name) => name switch
    {
        "strike" => ActiveType.Strike,
        "defense" => ActiveType.Defense,
        "heal" => ActiveType.Heal,
        _ => throw new InvalidDataException($"알 수 없는 액티브 유형: {name}"),
    };

    private sealed class ActiveSkillDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public string Grade { get; init; } = "";
        public int DamageMultPercent { get; init; } = 100;
        public int DefenderDfReductionPercent { get; init; }
        public int ExecutePercent { get; init; }
        public int ExecuteCapPercent { get; init; }
        public bool BuildingOnly { get; init; }
        public int DamageReductionPercent { get; init; }
        public int HealPercent { get; init; }
        public int HealCapPercent { get; init; } = 40;
    }
}
