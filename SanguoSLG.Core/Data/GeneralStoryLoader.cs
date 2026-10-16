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
        if (definitions.Count < 10 || definitions.Select(d => d.Kind)
                .Distinct(StringComparer.Ordinal).Count() != definitions.Count
            || definitions.Any(d => string.IsNullOrWhiteSpace(d.Kind) || string.IsNullOrWhiteSpace(d.Title)
                || string.IsNullOrWhiteSpace(d.OptionA) || string.IsNullOrWhiteSpace(d.OptionB)
                || d.Variants is not { Count: >= 3 }
                || d.Variants.Any(v => v is not { Count: >= 2 and <= 4 }
                    || v.Any(string.IsNullOrWhiteSpace))))
            throw new InvalidDataException("장수 대화 최소 10종과 대사 변형(각 3종, 2~4줄)이 필요합니다.");
        return definitions;
    }
}
