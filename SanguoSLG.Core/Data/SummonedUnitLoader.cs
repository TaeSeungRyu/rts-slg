namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

public sealed class SummonedUnitLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public IReadOnlyList<SummonedUnitDefinition> LoadFromDirectory(string directory)
    {
        var definitions = JsonSerializer.Deserialize<List<SummonedUnitDefinition>>(
            File.ReadAllText(Path.Combine(directory, "summoned-units.json")), Options)
            ?? throw new InvalidDataException("소환수 데이터를 읽을 수 없습니다.");
        if (definitions.Count == 0 || definitions.Select(x => x.Code).Distinct().Count() != definitions.Count
            || definitions.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.Troops <= 0
                || x.Attack <= 0 || x.Defense <= 0 || x.Speed <= 0 || x.Range <= 0
                || x.ActiveDays <= 0 || x.CooldownDays <= 0))
            throw new InvalidDataException("소환수 정의가 올바르지 않습니다.");
        return definitions;
    }
}
