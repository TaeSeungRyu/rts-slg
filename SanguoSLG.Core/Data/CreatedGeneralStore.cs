namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

public sealed record CreatedGeneralRecord(GeneralEditorRecord General, string Gender, int RecruitPointCost,
    string? PortraitPath = null, int? RequiredGeneralId = null,
    double FaceCenterX = 0.5, double FaceCenterY = 0.35, double FaceZoom = 1);

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
            if (!double.IsFinite(entry.FaceCenterX) || entry.FaceCenterX is < 0 or > 1
                || !double.IsFinite(entry.FaceCenterY) || entry.FaceCenterY is < 0 or > 1
                || !double.IsFinite(entry.FaceZoom) || entry.FaceZoom is < 1 or > 4)
                throw new InvalidDataException("생성 장수 얼굴 구도값이 올바르지 않습니다.");
            if (entry.RequiredGeneralId is { } required && (required <= 0 || required == entry.General.Id))
                throw new InvalidDataException("영입 선행 장수를 확인하세요.");
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
        if (entries.Any(entry => entry.RequiredGeneralId is { } required && !known.Contains(required)))
            throw new InvalidDataException("영입 선행 장수가 현재 시나리오에 없습니다.");
        var prerequisites = scenario.HeroUnlockList.Where(hero => hero.RequiredGeneral is not null)
            .ToDictionary(hero => hero.General.Value, hero => hero.RequiredGeneral!.Value.Value);
        foreach (var entry in entries)
            if (entry.RequiredGeneralId is { } required) prerequisites[entry.General.Id] = required;
        foreach (var entry in entries)
        {
            var seen = new HashSet<int>();
            var next = entry.General.Id;
            while (prerequisites.TryGetValue(next, out var required))
            {
                if (!seen.Add(next)) throw new InvalidDataException("영입 선행 장수 조건이 순환합니다.");
                next = required;
            }
        }
        var generalsJson = JsonSerializer.Serialize(entries.Select(e => e.General), Options);
        var added = new GeneralLoader().LoadFromJson(generalsJson)
            .Select((general, index) => general with
            {
                Gender = entries[index].Gender,
                PortraitPath = entries[index].PortraitPath,
                FaceCenterX = entries[index].FaceCenterX,
                FaceCenterY = entries[index].FaceCenterY,
                FaceZoom = entries[index].FaceZoom,
            });
        var heroes = entries.Select(e => new HeroUnlockDefinition(new GeneralId(e.General.Id),
            HeroUnlockType.Wanderer, RecruitPointCost: e.RecruitPointCost,
            RequiredGeneral: e.RequiredGeneralId is { } required ? new GeneralId(required) : null,
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
