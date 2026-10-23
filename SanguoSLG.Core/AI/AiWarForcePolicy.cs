namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed record AiWarForceAssessment(CityId Origin, CityId Target, CityId? Support,
    int OriginAvailable, int SupportAvailable, int DefenderTroops,
    int RequiredRatioTenths, int PlannedTroops, bool CanAttack);

public static class AiWarForcePolicy
{
    public static int Available(GameState state, City city)
    {
        var stationed = state.Garrisons.Where(line => line.City == city.Id && !line.Trainee)
            .Sum(line => Math.Max(0, line.Troops));
        var committed = state.Armies.Where(unit => unit.OriginCity == city.Id
            && unit.Field.Owner == city.Owner).Sum(unit => Math.Max(0, unit.Pool.Active));
        var minimumReserve = (int)Math.Ceiling((stationed + committed) * 0.20);
        return Math.Max(0, stationed - minimumReserve);
    }

    public static int ConcurrentTargetLimit(int ownedCities) => ownedCities switch
    {
        <= 0 => 0,
        <= 5 => 1,
        <= 10 => 2,
        _ => 3,
    };

    public static int OccupiedTargets(GameState state, FactionId faction)
        => state.Armies.Where(unit => unit.Field.Owner == faction && !unit.IsSupply
            && !unit.IsTransport && unit.Field.Target is not null
            && state.Cities.Any(city => city.Position == unit.Field.Target
                && state.AreHostile(faction, city.Owner)))
            .Select(unit => unit.Field.Target!.Value).Distinct().Count();

    public static AiWarForceAssessment Evaluate(GameState state, City origin, City target,
        IReadOnlyList<AiWarCandidate> candidates, BalanceConfig balance)
    {
        var defenders = state.Garrisons.Where(line => line.City == target.Id)
            .Sum(line => Math.Max(0, line.Troops));
        var primary = Available(state, origin);
        var support = (City?)null;
        var secondary = 0;
        var minimum = defenders == 0 ? 1 : (int)Math.Ceiling(defenders * 1.2);
        if (primary < minimum)
        {
            var primaryRoute = candidates.FirstOrDefault(candidate => candidate.Origin == origin.Id
                && candidate.Target == target.Id);
            support = state.Cities.Where(city => city.Id != origin.Id
                    && city.Owner == origin.Owner && AiWarCandidatePolicy.CanLaunch(city, balance))
                .Select(city => (City: city, Route: candidates.FirstOrDefault(candidate =>
                    candidate.Origin == city.Id && candidate.Target == target.Id)))
                .Where(option => primaryRoute is not null && option.Route is not null
                    && Math.Abs(option.Route.TravelDays - primaryRoute.TravelDays) < 21)
                .OrderBy(option => option.Route!.RouteTiles)
                .ThenBy(option => option.City.Id.Value)
                .Select(option => option.City).FirstOrDefault();
            if (support is not null) secondary = Available(state, support);
        }
        var total = primary + secondary;
        var ratio = defenders == 0 ? 0 : Enumerable.Range(12, 9).Reverse()
            .FirstOrDefault(tenths => total * 10L >= defenders * (long)tenths);
        var canAttack = defenders == 0 ? total >= 1 : ratio >= 12;
        var planned = !canAttack ? 0 : defenders == 0 ? 1
            : (int)Math.Ceiling(defenders * ratio / 10.0);
        return new AiWarForceAssessment(origin.Id, target.Id, support?.Id, primary,
            secondary, defenders, ratio, planned, canAttack);
    }
}
