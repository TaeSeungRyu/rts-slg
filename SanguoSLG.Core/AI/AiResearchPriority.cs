namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public static class AiResearchPriority
{
    public static IReadOnlyList<string> General(GameState state, FactionId faction)
    {
        var cities = state.Cities.Where(city => city.Owner == faction).ToList();
        if (cities.Count == 0) return [];
        var fronts = cities.Where(city => state.Cities.Any(enemy =>
            state.AreHostile(faction, enemy.Owner)
            && city.Position.Distance(enemy.Position) <= 15)).ToList();
        var relevant = fronts.Count > 0 ? fronts : cities;
        var wounded = state.CityWounded.Where(item => relevant.Any(city => city.Id == item.City))
            .Sum(item => item.Troops);
        var deployed = state.Armies.Where(unit => unit.Field.Owner == faction && !unit.IsSupply
            && !unit.IsTransport && unit.Pool.Active > 0).ToList();
        var shortProvisions = relevant.Any(city => city.Provisions < 3000)
            || deployed.Any(unit => unit.Provisions < Math.Max(1, unit.Pool.Active / 1000) * 14);
        var shortGold = relevant.Any(city => city.Gold < 1500);
        var shortTroops = relevant.Any(city => state.Garrisons.Where(line => line.City == city.Id
            && !line.Trainee).Sum(line => line.Troops) < 20_000);
        var needsCapacity = deployed.Any(unit => unit.Provisions > 0 && unit.MaxTroops > 0
            && unit.Provisions < Math.Max(1, unit.Pool.Active / 1000) * 14)
            && !shortProvisions;
        var priorities = new List<string>();
        if (relevant.Any(city => city.Security < 70)) priorities.Add(FactionResearch.PublicOrderCode);
        if (shortProvisions) priorities.Add(FactionResearch.AgricultureCode);
        if (shortGold) priorities.Add(FactionResearch.CommerceCode);
        if (wounded >= 1000) priorities.Add(FactionResearch.MedicineCode);
        if (shortTroops && relevant.Any(city => city.RecruitmentOfficer is not null))
            priorities.Add(FactionResearch.ConscriptionCode);
        if (needsCapacity) priorities.Add(FactionResearch.CombatBackpackCode);
        if (relevant.Any(city => city.TrainingOfficer is not null
            && state.Garrisons.Any(line => line.City == city.Id && line.TrainingLevel < 60)))
            priorities.Add(FactionResearch.TrainingCode);
        if (!shortProvisions && !shortGold && cities.Sum(city => city.Gold) >= 20_000
            && FactionResearch.GeneralResearchCodes.Count(code =>
                state.ResearchOf(faction, code) < GeneralResearchRules.MaxLevel) >= 3)
            priorities.Add(FactionResearch.ResearchDurationCode);
        priorities.AddRange([
            FactionResearch.AgricultureCode,
            FactionResearch.CommerceCode,
            FactionResearch.ConscriptionCode,
            FactionResearch.MedicineCode,
            FactionResearch.PublicOrderCode,
            FactionResearch.TrainingCode,
            FactionResearch.CombatBackpackCode,
            FactionResearch.ResearchDurationCode,
        ]);
        return priorities.Distinct().Where(code => state.ResearchOf(faction, code)
            < GeneralResearchRules.MaxLevel).ToList();
    }

    public static IReadOnlyList<string> Stratagem(GameState state, FactionId faction)
    {
        var planned = state.Commands.Where(command => command.Kind == CommandKind.CityStratagem
            && state.Cities.Any(city => city.Id == command.City && city.Owner == faction))
            .Select(command => StratagemResearchRules.ResearchCode(command.Facility))
            .Where(code => code.Length > 0).Distinct().ToList();
        var unscouted = state.Cities.Any(target => state.AreHostile(faction, target.Owner)
            && !state.IsScouted(faction, target.Id)
            && state.Cities.Any(city => city.Owner == faction && !city.IsPort
                && city.Position.Distance(target.Position) <= 15));
        if (unscouted) planned.Insert(0, FactionResearch.ScoutStratagemCode);
        return planned.Distinct().Where(code =>
        {
            var level = state.ResearchOf(faction, code);
            if (level >= StratagemResearchRules.MaxLevel) return false;
            var command = state.Commands.FirstOrDefault(candidate =>
                candidate.Kind == CommandKind.CityStratagem
                && StratagemResearchRules.ResearchCode(candidate.Facility) == code
                && state.Cities.Any(city => city.Id == candidate.City && city.Owner == faction));
            if (command is null || command.TargetCity is not { } targetId) return true;
            var officer = state.Generals.FirstOrDefault(general => general.Id == command.Main);
            var target = state.Cities.FirstOrDefault(city => city.Id == targetId);
            if (officer is null || target is null) return false;
            var governor = state.Generals.FirstOrDefault(general => general.Id == target.Governor);
            return CityStratagems.SuccessPercent(officer.Intellect, governor?.Intellect, level + 1)
                > CityStratagems.SuccessPercent(officer.Intellect, governor?.Intellect, level);
        }).ToList();
    }
}
