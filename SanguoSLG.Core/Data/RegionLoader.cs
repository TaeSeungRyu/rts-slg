namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

/// <summary>data/regions.json을 읽어 지역 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class RegionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<Region> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "regions.json")));

    public IReadOnlyList<Region> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<RegionDto>>(json, Options)
            ?? throw new InvalidDataException("지역 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(d => new Region(d.Code, d.Name, d.Realm, d.Note)).ToList();
    }

    private sealed class RegionDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Realm { get; init; } = "";
        public string Note { get; init; } = "";
    }
}
