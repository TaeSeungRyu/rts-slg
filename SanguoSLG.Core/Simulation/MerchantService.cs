namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class MerchantService(HexMap map, Func<int, City, IRandomSource>? randomForCity = null)
{
    public GameState Advance(GameState state, int fromDay, int toDay)
    {
        var visits = state.Merchants.ToList();
        // Moving armies can enter a merchant's tile after the previous weekly spawn check.
        // A visit ends before the next map redraw instead of leaving two models overlapped.
        visits.RemoveAll(visit => state.Armies.Any(unit => unit.Pool.Active > 0
            && !unit.IsWaitingDeployment && (unit.RenewalPosition is { } continuous
                ? RenewalHexSpace.NearestHex(continuous) : unit.Field.Position) == visit.Position));
        for (var day = fromDay + 1; day <= toDay; day++)
        {
            var expiredCities = visits.Where(visit => visit.ExpiresDay <= day)
                .Select(visit => visit.City).ToHashSet();
            visits.RemoveAll(visit => visit.ExpiresDay <= day);
            if (day % 7 != 0) continue;
            foreach (var city in state.Cities.OrderBy(city => city.Id.Value))
            {
                if (expiredCities.Contains(city.Id) || visits.Any(visit => visit.City == city.Id)) continue;
                var random = randomForCity?.Invoke(day, city)
                    ?? new SeededRandomSource(unchecked(day * 7919 + city.Id.Value * 104729 + state.StartYear));
                if (random.Next(0, 100) >= 10) continue;
                var plot = city.Position.Neighbors()
                    .Concat(city.Position.Neighbors().SelectMany(tile => tile.Neighbors()))
                    .Distinct()
                    .Where(tile => tile.Distance(city.Position) is >= 1 and <= 2
                        && map.Contains(tile) && map.TerrainAt(tile) == TerrainType.Plains
                        && !Occupied(state, visits, tile))
                    .OrderBy(tile => tile.Distance(city.Position))
                    .ThenBy(tile => tile.Q).ThenBy(tile => tile.R)
                    .Select(tile => (HexCoord?)tile).FirstOrDefault();
                if (plot is null) continue;
                var choices = MerchantCatalog.Artifacts.Select(item => item.Code).ToList();
                var stock = new List<string>(4);
                for (var index = 0; index < 4; index++)
                {
                    var choice = random.Next(0, choices.Count);
                    stock.Add(choices[choice]);
                    choices.RemoveAt(choice);
                }
                visits.Add(new MerchantVisit(city.Id, plot.Value, day, day + 14,
                    random.Next(80, 121), random.Next(40, 76), stock));
            }
        }
        return state with { MerchantVisits = visits };
    }

    public static CommandResult BuyGrain(GameState state, CityId cityId, FactionId faction,
        int units, int marketPricePer100)
    {
        if (!TryTrade(state, cityId, faction, out var city, out var visit))
            return CommandResult.Fail("거래할 수 있는 상인이 없습니다.", state);
        if (units <= 0) return CommandResult.Fail("구매 수량을 지정하세요.", state);
        var cost = checked((int)(((long)marketPricePer100 * visit!.BuyGrainPercent * units + 9999) / 10000));
        if (city!.Gold < cost) return CommandResult.Fail("도시의 금이 부족합니다.", state);
        return CommandResult.Success(state with { Cities = state.Cities.Select(item => item.Id == cityId
            ? item with { Gold = item.Gold - cost, Provisions = checked(item.Provisions + units) }
            : item).ToList() });
    }

    public static CommandResult SellGrain(GameState state, CityId cityId, FactionId faction,
        int units, int marketPricePer100)
    {
        if (!TryTrade(state, cityId, faction, out var city, out var visit))
            return CommandResult.Fail("거래할 수 있는 상인이 없습니다.", state);
        if (units <= 0) return CommandResult.Fail("판매 수량을 지정하세요.", state);
        if (city!.Provisions < units) return CommandResult.Fail("도시의 군량이 부족합니다.", state);
        var gain = checked((int)((long)marketPricePer100 * visit!.SellGrainPercent * units / 10000));
        return CommandResult.Success(state with { Cities = state.Cities.Select(item => item.Id == cityId
            ? item with { Gold = checked(item.Gold + gain), Provisions = item.Provisions - units }
            : item).ToList() });
    }

    public static CommandResult BuyArtifact(GameState state, CityId cityId, FactionId faction, string code)
    {
        if (!TryTrade(state, cityId, faction, out var city, out var visit))
            return CommandResult.Fail("거래할 수 있는 상인이 없습니다.", state);
        var artifact = MerchantCatalog.Find(code);
        if (artifact is null || !visit!.ArtifactCodes.Contains(code))
            return CommandResult.Fail("이 상인이 판매하지 않는 유물입니다.", state);
        if (city!.Gold < artifact.GoldCost) return CommandResult.Fail("도시의 금이 부족합니다.", state);
        var next = 1;
        string id;
        do { id = $"merchant:{cityId.Value}:{state.Day}:{next++}"; }
        while (state.Treasures.Any(item => item.Id == id));
        var treasure = new FactionTreasure(id, artifact.Code, artifact.Name, faction,
            state.Day, $"merchant:{cityId.Value}:{visit.ArrivedDay}");
        return CommandResult.Success(state with
        {
            Cities = state.Cities.Select(item => item.Id == cityId
                ? item.AddGold(-artifact.GoldCost) : item).ToList(),
            FactionTreasures = state.Treasures.Append(treasure).ToList(),
        });
    }

    public static GameState Dismiss(GameState state, CityId cityId)
        => state with { MerchantVisits = state.Merchants.Where(visit => visit.City != cityId).ToList() };

    private static bool TryTrade(GameState state, CityId cityId, FactionId faction,
        out City? city, out MerchantVisit? visit)
    {
        city = state.Cities.FirstOrDefault(item => item.Id == cityId && item.Owner == faction);
        visit = state.Merchants.FirstOrDefault(item => item.City == cityId && item.ExpiresDay > state.Day);
        return city is not null && visit is not null;
    }

    private static bool Occupied(GameState state, IReadOnlyList<MerchantVisit> visits, HexCoord plot)
        => visits.Any(visit => visit.Position == plot)
            || state.Cities.Any(city => CastleFootprint.TilesFor(city).Contains(plot))
            || state.Placements.Any(facility => facility.Plot == plot)
            || state.Ruins.Any(ruin => ruin.Position == plot)
            || state.Buildings.Any(building => building.Position == plot)
            || state.Armies.Any(unit => unit.Pool.Active > 0 && !unit.IsWaitingDeployment
                && (unit.RenewalPosition is { } continuous
                    ? RenewalHexSpace.NearestHex(continuous) : unit.Field.Position) == plot);
}
