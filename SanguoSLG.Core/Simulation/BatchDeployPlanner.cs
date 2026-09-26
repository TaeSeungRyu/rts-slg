namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record BatchDeployDraft(
    string TroopCode,
    int Troops,
    GeneralId Vanguard,
    GeneralId? Adjutant = null);

public sealed record BatchDeployValidation(bool Ok, IReadOnlyDictionary<int, string> RowErrors)
{
    public static BatchDeployValidation Success { get; } = new(true, new Dictionary<int, string>());
}

public sealed class BatchDeployPlanner
{
    public const int MaxUnits = 5;
    public const int PreferredTroops = 10_000;

    public IReadOnlyList<BatchDeployDraft> Recommend(
        CityId city,
        IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyList<General> generals,
        IReadOnlyCollection<GeneralId> availableGenerals,
        IReadOnlyList<TroopTemplate> troops,
        int unitTroopLimit,
        IReadOnlyCollection<GeneralId>? excludedGenerals = null,
        IReadOnlyDictionary<string, int>? reservedTroops = null)
    {
        var excluded = excludedGenerals?.ToHashSet() ?? [];
        var generalById = generals.ToDictionary(g => g.Id);
        var troopByCode = troops.ToDictionary(t => t.Code);
        var free = availableGenerals
            .Where(id => !excluded.Contains(id) && generalById.ContainsKey(id))
            .Distinct()
            .Select(id => generalById[id])
            .ToList();

        var candidates = garrisons
            .Where(g => g.City == city && !g.Trainee && g.Troops > 0 && troopByCode.ContainsKey(g.TroopCode))
            .Select(g => g with { Troops = Math.Max(0, g.Troops - (reservedTroops?.GetValueOrDefault(g.TroopCode) ?? 0)) })
            .Where(g => g.Troops > 0)
            .OrderByDescending(g => g.Troops >= PreferredTroops)
            .ThenBy(g => g.TroopCode, StringComparer.Ordinal)
            .SelectMany(g => Split(g, unitTroopLimit))
            .Take(MaxUnits)
            .ToList();

        var drafts = new List<BatchDeployDraft>();
        var used = new HashSet<GeneralId>();
        foreach (var candidate in candidates)
        {
            var troopClass = troopByCode[candidate.TroopCode].Class;
            var vanguard = Rank(free.Where(g => !used.Contains(g.Id)), troopClass).FirstOrDefault();
            if (vanguard is null) break;
            used.Add(vanguard.Id);
            drafts.Add(new BatchDeployDraft(candidate.TroopCode, candidate.Troops, vanguard.Id));
        }

        for (var i = 0; i < drafts.Count; i++)
        {
            var troopClass = troopByCode[drafts[i].TroopCode].Class;
            var adjutant = Rank(free.Where(g => !used.Contains(g.Id)), troopClass).FirstOrDefault();
            if (adjutant is null) break;
            used.Add(adjutant.Id);
            drafts[i] = drafts[i] with { Adjutant = adjutant.Id };
        }

        return drafts;
    }

    public BatchDeployValidation Validate(
        IReadOnlyList<BatchDeployDraft> drafts,
        CityId city,
        IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyCollection<GeneralId> availableGenerals,
        int unitTroopLimit,
        IReadOnlyDictionary<string, int>? reservedTroops = null,
        IReadOnlyCollection<GeneralId>? reservedGenerals = null)
    {
        var errors = new Dictionary<int, string>();
        var available = garrisons.Where(g => g.City == city && !g.Trainee)
            .GroupBy(g => g.TroopCode)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Troops) - (reservedTroops?.GetValueOrDefault(g.Key) ?? 0));
        var allowedGenerals = availableGenerals.ToHashSet();
        var usedGenerals = reservedGenerals?.ToHashSet() ?? [];
        var usedTroops = new Dictionary<string, int>();

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            string? error = null;
            if (i >= MaxUnits) error = $"부대는 최대 {MaxUnits}개까지 편성할 수 있습니다.";
            else if (draft.Troops <= 0 || draft.Troops > unitTroopLimit) error = $"병력은 1~{unitTroopLimit:N0}명이어야 합니다.";
            else if (!available.ContainsKey(draft.TroopCode)) error = "도시에 없는 병종입니다.";
            else if (!allowedGenerals.Contains(draft.Vanguard)) error = "선봉 장수가 출전 가능한 상태가 아닙니다.";
            else if (!usedGenerals.Add(draft.Vanguard)) error = "장수를 중복 편성할 수 없습니다.";
            else if (draft.Adjutant is { } adjutant && !allowedGenerals.Contains(adjutant)) error = "부관 장수가 출전 가능한 상태가 아닙니다.";
            else if (draft.Adjutant is { } duplicate && !usedGenerals.Add(duplicate)) error = "장수를 중복 편성할 수 없습니다.";

            var total = usedTroops.GetValueOrDefault(draft.TroopCode) + draft.Troops;
            usedTroops[draft.TroopCode] = total;
            if (error is null && total > available.GetValueOrDefault(draft.TroopCode)) error = "대기 병력이 부족합니다.";
            if (error is not null) errors[i] = error;
        }

        return errors.Count == 0 && drafts.Count > 0
            ? BatchDeployValidation.Success
            : new BatchDeployValidation(false, errors);
    }

    private static IEnumerable<(string TroopCode, int Troops)> Split(GarrisonForce garrison, int unitTroopLimit)
    {
        var cap = Math.Max(1, Math.Min(PreferredTroops, unitTroopLimit));
        var remaining = garrison.Troops;
        while (remaining >= cap)
        {
            yield return (garrison.TroopCode, cap);
            remaining -= cap;
        }
        if (remaining > 0) yield return (garrison.TroopCode, remaining);
    }

    private static IOrderedEnumerable<General> Rank(IEnumerable<General> generals, TroopClass troopClass)
        => generals.OrderByDescending(g => g.AptitudeFor(troopClass) >= AptitudeGrade.S)
            .ThenByDescending(g => g.AptitudeFor(troopClass))
            .ThenByDescending(g => g.Might)
            .ThenBy(g => g.Id.Value);
}
