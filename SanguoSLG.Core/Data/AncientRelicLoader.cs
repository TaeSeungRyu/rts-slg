namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

public sealed class AncientRelicLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public IReadOnlyList<AncientRelicDefinition> LoadFromDirectory(string directory)
        => LoadFromJson(File.ReadAllText(Path.Combine(directory, "ancient-relics.json")));

    public IReadOnlyList<AncientRelicDefinition> LoadFromJson(string json)
    {
        var items = JsonSerializer.Deserialize<List<AncientRelicDefinition>>(json, Options)
            ?? throw new InvalidDataException("고대유물 데이터를 읽을 수 없습니다.");
        if (items.Count != 10 || items.Any(x => string.IsNullOrWhiteSpace(x.Code)
                || string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.ClueCode)
                || string.IsNullOrWhiteSpace(x.ClueName) || string.IsNullOrWhiteSpace(x.Effect)
                || x.Value is < 0 or > 5)
            || items.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != 10
            || items.Select(x => x.ClueCode).Distinct(StringComparer.Ordinal).Count() != 10)
            throw new InvalidDataException("고대유물은 고유 코드와 0~5% 효과를 가진 10종이어야 합니다.");
        return items;
    }
}
