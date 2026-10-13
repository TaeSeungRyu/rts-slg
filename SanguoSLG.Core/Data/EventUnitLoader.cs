namespace SanguoSLG.Core.Data;

using System.Text.Json;
using System.Text.Json.Serialization;
using SanguoSLG.Core.Domain;

public sealed class EventUnitLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public IReadOnlyList<EventUnitDefinition> LoadFromDirectory(string directory)
        => LoadFromJson(File.ReadAllText(Path.Combine(directory, "event-units.json")));

    public IReadOnlyList<EventUnitDefinition> LoadFromJson(string json)
    {
        var definitions = JsonSerializer.Deserialize<List<EventUnitDefinition>>(json, Options)
            ?? throw new InvalidDataException("이벤트 유닛 데이터를 읽을 수 없습니다.");
        if (definitions.Select(d => d.Code).Distinct(StringComparer.Ordinal).Count() != definitions.Count
            || definitions.Any(d => string.IsNullOrWhiteSpace(d.Code) || string.IsNullOrWhiteSpace(d.Name)
                || string.IsNullOrWhiteSpace(d.DiscoveryCode) || string.IsNullOrWhiteSpace(d.TreasureCode)
                || string.IsNullOrWhiteSpace(d.TreasureName) || d.SpawnDelayDays < 0
                || d.SpawnMinDistance < 1 || d.SpawnMaxDistance < d.SpawnMinDistance
                || d.SpawnTerrains is not { Count: > 0 } || d.Troops <= 0 || d.Attack <= 0 || d.Defense <= 0
                || d.Speed <= 0 || d.Detection <= 0 || d.Range <= 0
                || d.ExperienceReward < 0 || d.AptitudeReward < 0))
            throw new InvalidDataException("이벤트 유닛 정의가 중복되거나 유효하지 않습니다.");
        return definitions;
    }
}
