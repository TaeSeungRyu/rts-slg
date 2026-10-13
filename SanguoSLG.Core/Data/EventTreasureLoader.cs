namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

public sealed class EventTreasureLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public IReadOnlyList<EventTreasureDefinition> LoadFromDirectory(string directory)
        => LoadFromJson(File.ReadAllText(Path.Combine(directory, "event-treasures.json")));

    public IReadOnlyList<EventTreasureDefinition> LoadFromJson(string json)
    {
        var items = JsonSerializer.Deserialize<List<EventTreasureDefinition>>(json, Options)
            ?? throw new InvalidDataException("신수 보물 데이터를 읽을 수 없습니다.");
        if (items.Count == 0 || items.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != items.Count
            || items.Any(x => string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Name)
                || string.IsNullOrWhiteSpace(x.Source) || string.IsNullOrWhiteSpace(x.Effect)
                || x.Value < 0 || (x.Effect == "summon") != (x.SummonCode is not null)))
            throw new InvalidDataException("신수 보물 정의가 중복되거나 유효하지 않습니다.");
        return items;
    }
}
