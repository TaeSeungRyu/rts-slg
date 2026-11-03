namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record GenericRecruitment(CityId City, int MonthIndex, GeneralId General,
    string Kind, int DialogueVariant, GeneralId Ruler, GeneralId Companion,
    bool Presented = false, int DialoguePage = 0);

public static class GenericRecruitmentService
{
    public const int GoldCost = 1000;

    private static readonly string[] Surnames =
    [
        "진", "허", "위", "노", "도", "구", "임", "전", "설", "양",
        "방", "하", "석", "원", "좌", "문", "온", "복", "탁", "견",
    ];

    private static readonly string[] GivenNames =
    [
        "자명", "중헌", "문정", "경원", "숙평", "계선", "공례", "백유",
        "영준", "덕창", "현도", "승문", "태안", "원직", "사홍",
    ];

    private static readonly string[] ShortGivenNames =
    [
        "민", "준", "헌", "정", "원", "평", "선", "유", "창", "도",
        "안", "직", "홍", "명", "경",
    ];

    private static readonly string[] MartialActives = ["heavy_blow", "double_hit", "brace", "patch"];
    private static readonly string[] MartialPassives =
        ["drilled", "war_cry", "grit", "plainsman", "teamwork", "guard_drill", "endurance", "shield_basics"];
    private static readonly string[] CivilPassives = ["pacifier", "quartermaster", "drillmaster", "tuntian", "merchant"];

    public static CommandResult Recruit(GameState state, City city, GeneralId actor, bool martial)
    {
        if (city.IsGate) return CommandResult.Fail("관문에서는 장수를 모집할 수 없습니다.", state);
        if (city.Gold < GoldCost) return CommandResult.Fail("모집에 필요한 금 1,000이 부족합니다.", state);
        var faction = state.Factions.FirstOrDefault(f => f.Id == city.Owner);
        if (faction is null || state.PostingOf(actor)?.Faction != city.Owner)
            return CommandResult.Fail("이 세력의 장수만 모집을 요청할 수 있습니다.", state);
        var month = (state.Day - 1) / GameState.DaysPerMonth;
        if (state.GenericRecruitmentHistory.Any(entry => entry.City == city.Id && entry.MonthIndex == month))
            return CommandResult.Fail("이 도시는 이번 달에 이미 장수를 모집했습니다.", state);

        var id = new GeneralId(state.Generals.Max(g => g.Id.Value) + 1);
        var seed = unchecked(2166136261u);
        foreach (var value in new[] { state.StartYear, state.Day, city.Id.Value, id.Value, martial ? 1 : 2 })
            seed = unchecked((seed ^ (uint)value) * 16777619u);
        var random = new Random(unchecked((int)seed));
        var taken = state.Generals.Select(g => g.Name).ToHashSet(StringComparer.Ordinal);
        var preferred = random.Next(100) < 70 ? ShortGivenNames : GivenNames;
        var fallback = ReferenceEquals(preferred, ShortGivenNames) ? GivenNames : ShortGivenNames;
        var names = (from surname in Surnames from given in preferred select surname + given)
            .Where(name => !taken.Contains(name)).ToArray();
        if (names.Length == 0)
            names = (from surname in Surnames from given in fallback select surname + given)
                .Where(name => !taken.Contains(name)).ToArray();
        var name = names.Length > 0 ? names[random.Next(names.Length)] : $"진자명{id.Value}";
        var might = random.Next(martial ? 50 : 20, martial ? 76 : 36);
        var intellect = random.Next(martial ? 20 : 50, martial ? 46 : 76);
        var politics = random.Next(martial ? 20 : 50, martial ? 46 : 76);
        var aptitudes = Enum.GetValues<TroopClass>().ToDictionary(troop => troop,
            _ => (AptitudeGrade)random.Next(martial ? (int)AptitudeGrade.D : (int)AptitudeGrade.F,
                martial ? (int)AptitudeGrade.B + 1 : (int)AptitudeGrade.C + 1));
        var active = martial && might >= 70 ? MartialActives[random.Next(MartialActives.Length)] : null;
        IReadOnlyList<GeneralSkill> battlePassives = martial
            ? [new GeneralSkill(MartialPassives[random.Next(MartialPassives.Length)], 1)] : [];
        IReadOnlyList<GeneralSkill> adminPassives = !martial && (intellect >= 70 || politics >= 70)
            ? [new GeneralSkill(CivilPassives[random.Next(CivilPassives.Length)], 1)] : [];
        var ageGroup = random.Next(2, 6);
        var portrait = $"res://assets/portraits/generic/{ageGroup}0s-{(martial ? "martial" : "civil")}.png";
        var ruler = faction.Ruler;
        var companions = state.Assignments.Where(p => p.Faction == city.Owner && p.General != ruler)
            .Select(p => p.General).Distinct().OrderBy(g => g.Value).ToArray();
        var companion = companions.Length > 0 ? companions[random.Next(companions.Length)] : ruler;
        var general = new General(id, name, aptitudes, might, intellect, politics, active,
            battlePassives, adminPassives, state.Year - ageGroup * 10 - random.Next(10),
            Region: city.Region, Desc: martial ? "무관모집으로 발탁된 장수" : "문관모집으로 발탁된 장수",
            PortraitPath: portrait);
        var entry = new GenericRecruitment(city.Id, month, id, martial ? "martial" : "civil",
            random.Next(3), ruler, companion);
        return CommandResult.Success(state with
        {
            Cities = state.Cities.Select(c => c.Id == city.Id ? c with { Gold = c.Gold - GoldCost } : c).ToArray(),
            Generals = state.Generals.Append(general).ToArray(),
            Postings = state.Assignments.Append(new GeneralPosting(id, city.Owner, city.Id)).ToArray(),
            GenericRecruitments = state.GenericRecruitmentHistory.Append(entry).ToArray(),
        });
    }

    public static GameState MarkPresented(GameState state, GeneralId general)
        => state with { GenericRecruitments = state.GenericRecruitmentHistory.Select(entry => entry.General == general
            ? entry with { Presented = true } : entry).ToArray() };

    public static GameState SetDialoguePage(GameState state, GeneralId general, int page)
        => state with { GenericRecruitments = state.GenericRecruitmentHistory.Select(entry => entry.General == general
            ? entry with { DialoguePage = Math.Clamp(page, 0, 5) } : entry).ToArray() };
}
