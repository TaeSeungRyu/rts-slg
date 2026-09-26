namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record BatchDeployDraft(
    string TroopCode,
    int Troops,
    GeneralId Vanguard,
    GeneralId? Adjutant = null);

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
        IReadOnlyCollection<GeneralId>? excludedGenerals = null)
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
