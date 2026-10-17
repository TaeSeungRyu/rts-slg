namespace SanguoSLG.Core.Data;

using System.Text.Json;

public sealed record GeneralStoryDefinition(
    string Kind, string Title, IReadOnlyList<IReadOnlyList<string>> Variants,
    string OptionA, string OptionB, IReadOnlyDictionary<int, string>? VoiceLines = null);

/// <summary>장수 대화 문구는 게임 규칙과 분리된 데이터다. 장수 ID는 기존 원본을 그대로 사용한다.</summary>
public sealed class GeneralStoryLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public IReadOnlyList<GeneralStoryDefinition> LoadFromDirectory(string directory)
        => LoadFromJson(File.ReadAllText(Path.Combine(directory, "general-story-events.json")));

    public IReadOnlyList<GeneralStoryDefinition> LoadFromJson(string json)
    {
        var definitions = JsonSerializer.Deserialize<List<GeneralStoryDefinition>>(json, Options)
            ?? throw new InvalidDataException("장수 대화 데이터를 읽을 수 없습니다.");
        if (definitions.Count < 8 || definitions.Select(d => d.Kind)
                .Distinct(StringComparer.Ordinal).Count() != definitions.Count
            || definitions.Any(d => string.IsNullOrWhiteSpace(d.Kind) || string.IsNullOrWhiteSpace(d.Title)
                || string.IsNullOrWhiteSpace(d.OptionA) || string.IsNullOrWhiteSpace(d.OptionB)
                || d.Variants is null || d.Variants.Count < (IsConciseReport(d.Kind) ? 1 : 3)
                || d.Variants.Any(v => (d.Kind is "combat_bark" or "city_bark" || IsConciseReport(d.Kind) ? v is not { Count: 1 }
                        : v is not { Count: >= 2 and <= 4 })
                    || v.Any(string.IsNullOrWhiteSpace))))
            throw new InvalidDataException("장수 대화 최소 8종과 유형별 대사 변형 3종이 필요합니다.");
        return definitions;
    }

    private static bool IsConciseReport(string kind)
        => kind is "scout_success" or "scout_fail" or "stratagem_success" or "stratagem_fail";
}
