namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class AutoShipyardService
{
    public static GameState Advance(GameState state)
    {
        if (!state.Cities.Any(city => city.IsPort && city.AiSmallShipNextDay > 0))
            return state;
        var stocks = state.PortShips.ToList();
        var cities = new List<City>();
        foreach (var original in state.Cities.OrderBy(city => city.Id.Value))
        {
            var city = original;
            if (!city.IsPort || city.AiSmallShipNextDay <= 0)
            {
                cities.Add(city);
                continue;
            }
            if (city.AiSmallShipCompletionDay > 0
                && city.AiSmallShipCompletionDay <= state.Day)
            {
                AddStock(stocks, city.Id, "small_boat");
                city = city with { AiSmallShipCompletionDay = 0 };
            }
            if (city.AiMediumShipCompletionDay > 0
                && city.AiMediumShipCompletionDay <= state.Day)
            {
                AddStock(stocks, city.Id, "medium_ship");
                city = city with { AiMediumShipCompletionDay = 0 };
            }
            var required = state.Garrisons.Where(line => line.City == city.Id && line.Troops > 0)
                .GroupBy(line => line.TroopCode, StringComparer.Ordinal)
                .Sum(group => (group.Sum(line => line.Troops) + 9_999) / 10_000);
            var available = stocks.Where(stock => stock.City == city.Id).Sum(stock => stock.Count)
                + (city.AiSmallShipCompletionDay > 0 ? 1 : 0)
                + (city.AiMediumShipCompletionDay > 0 ? 1 : 0);
            if (city.AiSmallShipNextDay <= state.Day)
            {
                if (available < required && city.Gold >= CommandService.ShipBuildCost("small_boat")
                    && city.AiSmallShipCompletionDay == 0)
                {
                    city = city with { Gold = city.Gold - CommandService.ShipBuildCost("small_boat"),
                        AiSmallShipCompletionDay = state.Day + CommandService.ShipBuildBaseDays("small_boat") };
                    available++;
                }
                city = city with { AiSmallShipNextDay = NextDay(city.AiSmallShipNextDay, state.Day, 90) };
            }
            if (city.AiMediumShipNextDay <= state.Day)
            {
                if (available < required && city.Gold >= CommandService.ShipBuildCost("medium_ship")
                    && city.AiMediumShipCompletionDay == 0)
                {
                    city = city with { Gold = city.Gold - CommandService.ShipBuildCost("medium_ship"),
                        AiMediumShipCompletionDay = state.Day + CommandService.ShipBuildBaseDays("medium_ship") };
                }
                city = city with { AiMediumShipNextDay = NextDay(city.AiMediumShipNextDay, state.Day, 180) };
            }
            cities.Add(city);
        }
        return state with { Cities = cities, PortShipStocks = stocks };
    }

    private static int NextDay(int prior, int now, int interval)
    {
        while (prior <= now) prior += interval;
        return prior;
    }

    private static void AddStock(List<PortShipStock> stocks, CityId city, string shipCode)
    {
        var index = stocks.FindIndex(stock => stock.City == city && stock.ShipCode == shipCode);
        if (index >= 0) stocks[index] = stocks[index] with { Count = stocks[index].Count + 1 };
        else stocks.Add(new PortShipStock(city, shipCode, 1));
    }
}
