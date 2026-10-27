namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

public sealed record CreatedGeneralRecord(GeneralEditorRecord General, string Gender, int RecruitPointCost,
    string? PortraitPath = null);

/// <summary>새 게임에서만 읽는 사용자 장수 목록. 오리진 데이터와 저장 게임은 변경하지 않는다.</summary>
public static class CreatedGeneralStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static IReadOnlyList<CreatedGeneralRecord> Load(string json)
    {
        var entries = JsonSerializer.Deserialize<List<CreatedGeneralRecord>>(json, Options)
            ?? throw new InvalidDataException("생성 장수 목록을 읽을 수 없습니다.");
        var ids = new HashSet<int>();
        foreach (var entry in entries)
        {
            if (entry.General.Id < 10000 || !ids.Add(entry.General.Id))
                throw new InvalidDataException("생성 장수 ID는 10000 이상이며 중복될 수 없습니다.");
            if (string.IsNullOrWhiteSpace(entry.General.Name) || entry.General.Name.Length > 20)
                throw new InvalidDataException("장수 이름은 1~20자여야 합니다.");
            if (entry.Gender is not ("male" or "female") || entry.RecruitPointCost is < 0 or > 1000)
                throw new InvalidDataException("성별 또는 영입 포인트가 올바르지 않습니다.");
            if (entry.PortraitPath is { } portrait && !IsAllowedPortraitPath(portrait))
                throw new InvalidDataException("생성 장수 초상 경로가 올바르지 않습니다.");
        }
        return entries;
    }

    public static string Save(IReadOnlyList<CreatedGeneralRecord> entries)
    {
        Load(JsonSerializer.Serialize(entries, Options));
        return JsonSerializer.Serialize(entries, Options);
    }

    public static Scenario Apply(Scenario scenario, IReadOnlyList<CreatedGeneralRecord> entries)
    {
        var known = scenario.Generals.Select(g => g.Id.Value).ToHashSet();
        if (entries.Any(e => !known.Add(e.General.Id)))
            throw new InvalidDataException("기존 장수와 생성 장수 ID가 겹칩니다.");
        var generalsJson = JsonSerializer.Serialize(entries.Select(e => e.General), Options);
        var added = new GeneralLoader().LoadFromJson(generalsJson)
            .Select((general, index) => general with
            {
                Gender = entries[index].Gender,
                PortraitPath = entries[index].PortraitPath,
            });
        var heroes = entries.Select(e => new HeroUnlockDefinition(new GeneralId(e.General.Id),
            HeroUnlockType.Wanderer, RecruitPointCost: e.RecruitPointCost,
            Title: e.General.Name, Desc: "사용자 생성 장수")).ToList();
        return scenario with
        {
            Generals = scenario.Generals.Concat(added).ToList(),
            HeroUnlocks = scenario.HeroUnlockList.Concat(heroes).ToList(),
        };
    }

    private static bool IsAllowedPortraitPath(string path)
    {
        var prefix = path.StartsWith("res://assets/portraits/created/", StringComparison.Ordinal)
            ? "res://assets/portraits/created/"
            : path.StartsWith("user://general-presets/portraits/", StringComparison.Ordinal)
                ? "user://general-presets/portraits/" : null;
        if (prefix is null || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;
        var name = path[prefix.Length..];
        return name.Length > 4 && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }
}
