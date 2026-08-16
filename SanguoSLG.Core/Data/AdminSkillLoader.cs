namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

/// <summary>data/admin-skills.json을 읽어 내정 스킬 목록으로 매핑한다. 엔진 비의존.</summary>
public sealed class AdminSkillLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<AdminSkill> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "admin-skills.json")));

    public IReadOnlyList<AdminSkill> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<AdminSkillDto>>(json, Options)
            ?? throw new InvalidDataException("내정 스킬 데이터를 역직렬화할 수 없습니다.");

        return dtos.Select(d => new AdminSkill(d.Code, d.Name, d.Bucket, d.Tiers)).ToList();
    }

    private sealed class AdminSkillDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Bucket { get; init; } = "";
        public List<int>? Tiers { get; init; }
    }
}
