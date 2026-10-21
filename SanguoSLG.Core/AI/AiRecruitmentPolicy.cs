namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public static class AiRecruitmentPolicy
{
    public static string Choose(GameState state, City city,
        IReadOnlyDictionary<string, TroopTemplate> troops, string fallback)
    {
        var residents = state.GeneralsAt(city.Id)
            .Where(id => !state.IsGeneralInField(id))
            .Select(id => state.Generals.First(general => general.Id == id)).ToList();
        var stationed = state.Garrisons.Where(line => line.City == city.Id && !line.Trainee)
            .Sum(line => line.Troops);
        var siege = state.Garrisons.Where(line => line.City == city.Id && !line.Trainee
                && troops.TryGetValue(line.TroopCode, out var template)
                && template.Class == TroopClass.Siege).Sum(line => line.Troops);
        if (stationed >= 30_000 && siege < Math.Max(5000, stationed / 10)
            && state.Cities.Any(target => state.AreHostile(city.Owner, target.Owner)
                && state.IsScouted(city.Owner, target.Id)
                && city.Position.Distance(target.Position) <= 15))
        {
            var required = troops.Values.Where(troop => troop.Class == TroopClass.Siege
                    && FactionTroopUnlock.Check(state, city.Owner, troop.Code).Allowed)
                .OrderByDescending(troop => troop.RangeCastle >= 2)
                .ThenByDescending(troop => residents.Count == 0 ? AptitudeGrade.F
                    : residents.Max(general => general.AptitudeFor(TroopClass.Siege)))
                .ThenByDescending(troop => troop.AtkBuilding)
                .ThenBy(troop => troop.Code, StringComparer.Ordinal).FirstOrDefault();
            if (required is not null) return required.Code;
        }
        var best = troops.Values.Where(troop => troop.Class is not
                (TroopClass.Naval or TroopClass.Supply or TroopClass.Defense)
                && FactionTroopUnlock.Check(state, city.Owner, troop.Code).Allowed)
            .Select(troop => new
            {
                Troop = troop,
                Grade = residents.Count == 0 ? AptitudeGrade.F
                    : residents.Max(general => general.AptitudeFor(troop.Class)),
                Stock = state.Garrisons.Where(line => line.City == city.Id
                    && line.TroopCode == troop.Code && !line.Trainee)
                    .Sum(line => line.Troops),
            })
            .Where(candidate => candidate.Grade >= AptitudeGrade.S)
            .OrderByDescending(candidate => candidate.Grade)
            .ThenBy(candidate => candidate.Stock)
            .ThenBy(candidate => candidate.Troop.Code, StringComparer.Ordinal)
            .FirstOrDefault();
        if (best is not null) return best.Troop.Code;
        if (troops.ContainsKey(city.AutoRecruitTroopCode)
            && FactionTroopUnlock.Check(state, city.Owner, city.AutoRecruitTroopCode).Allowed)
            return city.AutoRecruitTroopCode;
        return fallback;
    }
}
