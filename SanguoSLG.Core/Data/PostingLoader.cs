namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

/// <summary>data/postings.json을 읽어 장수 배속 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class PostingLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<GeneralPosting> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "postings.json")));

    public IReadOnlyList<GeneralPosting> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<PostingDto>>(json, Options)
            ?? throw new InvalidDataException("배속 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(d => new GeneralPosting(
            new GeneralId(d.General),
            new FactionId(d.Faction),
            d.City is { } c ? new CityId(c) : null)).ToList();
    }

    private sealed class PostingDto
    {
        public int General { get; init; }
        public int Faction { get; init; }
        public int? City { get; init; }
    }
}
