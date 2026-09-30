namespace SanguoSLG.Core.Data;

using System.Text.Json.Nodes;
using SanguoSLG.Core.Domain;

/// <summary>불변 오리진 정의에서 ID 기반 프리셋을 적용해 독립 캠페인 장수 사본을 만든다.</summary>
public static class GeneralDataLayers
{
    public static IReadOnlyList<General> CreateCampaign(
        IReadOnlyList<General> origin,
        IReadOnlyList<GeneralPresetOverride>? preset = null)
    {
        var result = origin.Select(Clone).ToDictionary(g => g.Id);
        var seen = new HashSet<GeneralId>();
        foreach (var edit in preset ?? [])
        {
            if (!seen.Add(edit.Id))
            {
                throw new InvalidDataException($"중복 장수 프리셋 ID: {edit.Id.Value}");
            }

            if (!result.TryGetValue(edit.Id, out var general))
            {
                throw new InvalidDataException($"오리진에 없는 장수 프리셋 ID: {edit.Id.Value}");
            }

            result[edit.Id] = general with
            {
                Aptitudes = edit.Aptitudes is null
                    ? general.Aptitudes
                    : general.Aptitudes.Concat(edit.Aptitudes).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value),
                Might = edit.Might ?? general.Might,
                Intellect = edit.Intellect ?? general.Intellect,
                Politics = edit.Politics ?? general.Politics,
                BattleActive = edit.BattleActiveSpecified ? edit.BattleActive : general.BattleActive,
                BattlePassives = edit.BattlePassives?.Select(CloneSkill).ToList() ?? general.Passives.ToList(),
                AdminPassives = edit.AdminPassives?.Select(CloneSkill).ToList() ?? (general.AdminPassives ?? []).ToList(),
            };
        }

        return origin.Select(g => result[g.Id]).ToList();
    }

    public static General Clone(General general) => general with
    {
        Aptitudes = general.Aptitudes.ToDictionary(x => x.Key, x => x.Value),
        BattlePassives = general.Passives.Select(CloneSkill).ToList(),
        AdminPassives = (general.AdminPassives ?? []).Select(CloneSkill).ToList(),
        AptitudeExperience = general.AptitudeExperience?.ToDictionary(x => x.Key, x => x.Value),
        AptitudeBaseGrades = general.AptitudeBaseGrades?.ToDictionary(x => x.Key, x => x.Value),
    };

    private static GeneralSkill CloneSkill(GeneralSkill skill) => skill with { };
}

public sealed record GeneralPresetOverride(
    GeneralId Id,
    IReadOnlyDictionary<TroopClass, AptitudeGrade>? Aptitudes = null,
    int? Might = null,
    int? Intellect = null,
    int? Politics = null,
    string? BattleActive = null,
    bool BattleActiveSpecified = false,
    IReadOnlyList<GeneralSkill>? BattlePassives = null,
    IReadOnlyList<GeneralSkill>? AdminPassives = null);

/// <summary>새 게임 시작 전 선택 프리셋의 부분 JSON을 읽는다. 편집하지 않은 필드는 오리진 값을 유지한다.</summary>
public static class GeneralPresetStore
{
    public static IReadOnlyList<GeneralPresetOverride> Load(string json)
    {
        var root = JsonNode.Parse(json) as JsonArray
            ?? throw new InvalidDataException("장수 프리셋 루트는 배열이어야 합니다.");
        return root.OfType<JsonObject>().Select(Parse).ToList();
    }

    private static GeneralPresetOverride Parse(JsonObject node)
    {
        var id = node["id"]?.GetValue<int>()
            ?? throw new InvalidDataException("장수 프리셋에 ID가 없습니다.");
        return new GeneralPresetOverride(
            new GeneralId(id),
            ParseAptitudes(node["aptitudes"] as JsonObject),
            node["might"]?.GetValue<int>(),
            node["intellect"]?.GetValue<int>(),
            node["politics"]?.GetValue<int>(),
            node["battle_active"]?.GetValue<string>(),
            node.ContainsKey("battle_active"),
            ParseSkills(node["battle_passives"] as JsonArray),
            ParseSkills(node["admin_passives"] as JsonArray));
    }

    private static IReadOnlyDictionary<TroopClass, AptitudeGrade>? ParseAptitudes(JsonObject? node)
        => node?.ToDictionary(x => ParseClass(x.Key), x => ParseGrade(x.Value?.GetValue<string>() ?? "F"));

    private static IReadOnlyList<GeneralSkill>? ParseSkills(JsonArray? nodes)
        => nodes?.OfType<JsonObject>().Select(node => new GeneralSkill(
            node["code"]?.GetValue<string>() ?? throw new InvalidDataException("프리셋 스킬 코드가 없습니다."),
            node["tier"]?.GetValue<int>() ?? 1,
            node["experience"]?.GetValue<int>() ?? 0)).ToList();

    private static TroopClass ParseClass(string value) => value switch
    {
        "infantry" => TroopClass.Infantry, "archer" => TroopClass.Archer,
        "cavalry" => TroopClass.Cavalry, "elephant" => TroopClass.Elephant,
        "siege" => TroopClass.Siege, "naval" => TroopClass.Naval,
        "supply" => TroopClass.Supply, "defense" => TroopClass.Defense,
        _ => throw new InvalidDataException($"알 수 없는 프리셋 병종: {value}"),
    };

    private static AptitudeGrade ParseGrade(string value) => value switch
    {
        "F" => AptitudeGrade.F, "D" => AptitudeGrade.D, "C" => AptitudeGrade.C,
        "B" => AptitudeGrade.B, "A" => AptitudeGrade.A, "A+" => AptitudeGrade.APlus,
        "S" => AptitudeGrade.S, "SS" => AptitudeGrade.SS, "SSS" => AptitudeGrade.SSS,
        _ => throw new InvalidDataException($"알 수 없는 프리셋 적성: {value}"),
    };
}
