namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed record AiConstructionCandidate(UnitId Unit, string DefinitionCode,
    HexCoord Position, int Score, string Reason);

public static class AiFieldConstructionEvaluator
{
    public static IReadOnlyList<AiConstructionCandidate> Evaluate(GameState state, FactionId faction,
        HexMap map, IReadOnlyList<FieldBuildingDefinition> definitions,
        IReadOnlySet<HexCoord> visible, BalanceConfig? economy = null)
    {
        if (state.Buildings.Count(building => building.Owner == faction
            && !building.IsCompleted(state.Day)) >= 2) return [];

        var balance = economy ?? new BalanceConfig(MonthlyTaxPerCity: 100);
        var ownedCities = state.Cities.Where(city => city.Owner == faction && !city.IsPort
            && city.Security >= 70
            && city.Wall * 2 >= CastleWall.Max(city.Castle, balance, city.WallLevel))
            .OrderBy(city => city.Id.Value).ToList();
        var threats = state.Armies.Where(unit => state.AreHostile(faction, unit.Field.Owner)
            && unit.Pool.Active > 0 && visible.Contains(unit.Field.Position))
            .OrderBy(unit => unit.Id.Value).ToList();
        var enemyCities = state.Cities.Where(city => state.AreHostile(faction, city.Owner)
            && state.IsScouted(faction, city.Id)).OrderBy(city => city.Id.Value).ToList();
        var candidates = new List<AiConstructionCandidate>();

        foreach (var unit in state.Armies.Where(unit => unit.Field.Owner == faction
            && unit.Field.Domain == MovementDomain.Land && unit.Class != TroopClass.Naval
            && unit.Field.Mode == UnitMode.Standby && unit.Field.Target is null
            && !unit.IsWaitingDeployment && !unit.IsConstructing && !unit.IsTransport
            && !unit.IsSupply && !unit.IsSummon && unit.Pool.Active >= FieldConstructionService.MinimumTroops
            && unit.TracksProvisions && unit.Provisions > 0 && unit.AiPressureUntilDay <= state.Day
            && !state.Buildings.Any(building => building.GarrisonUnit == unit.Id))
            .OrderBy(unit => unit.Id.Value))
        {
            var city = ownedCities.Where(city => city.Position.Distance(unit.Field.Position) <= 5)
                .OrderBy(city => city.Position.Distance(unit.Field.Position))
                .ThenBy(city => city.Id.Value).FirstOrDefault();
            if (city is null) continue;
            var cityThreats = threats.Where(enemy => enemy.Field.Position.Distance(city.Position) <= 7)
                .ToList();
            var scoutedFront = enemyCities.Any(enemy => enemy.Position.Distance(city.Position) <= 10);
            if (cityThreats.Count == 0 && !scoutedFront) continue;

            foreach (var position in new[] { unit.Field.Position }.Concat(unit.Field.Position.Neighbors())
                .Where(map.Contains).OrderBy(tile => tile.Q).ThenBy(tile => tile.R))
            {
                if (state.Buildings.Any(building => building.Position == position)
                    || state.Placements.Any(placement => placement.Plot == position)
                    || state.Ruins.Any(ruin => ruin.Position == position)
                    || state.Cities.Any(other => CastleFootprint.TilesFor(other).Contains(position))
                    || threats.Any(enemy => enemy.Field.Position.Distance(position) <= 2)) continue;

                foreach (var definition in definitions.OrderBy(definition => definition.Code,
                    StringComparer.Ordinal))
                {
                    if (unit.CarryingGold < definition.GoldCost
                        || unit.Provisions < definition.ProvisionsCost
                        || unit.Pool.Active - definition.TroopCost <= 0
                        || !AllowedTerrain(definition.Kind, map.TerrainAt(position))) continue;
                    var overlapping = state.Buildings.Any(building => building.Owner == faction
                        && building.DefinitionCode == definition.Code
                        && building.Position.Distance(position) <= definition.EffectRadius * 2);
                    if (overlapping) continue;

                    var score = definition.Kind switch
                    {
                        FieldBuildingKind.Palisade when cityThreats.Count > 0
                            && cityThreats.Any(enemy => enemy.Field.Position.Distance(position) <= 5)
                            && position.Distance(city.Position) >= 2 => 30,
                        FieldBuildingKind.Formation when cityThreats.Count > 0
                            && position.Distance(city.Position) is >= 2 and <= 3 => 24,
                        FieldBuildingKind.Watchtower when scoutedFront
                            && position.Distance(city.Position) is >= 2 and <= 4 => 18,
                        FieldBuildingKind.Fort when scoutedFront && cityThreats.Count == 0
                            && position.Distance(city.Position) is >= 2 and <= 4 => 15,
                        FieldBuildingKind.ScoutPost when scoutedFront
                            && position.Distance(city.Position) is >= 3 and <= 5
                            && CountUnseen(map, visible, position, definition.EffectRadius) >= 4 => 12,
                        _ => 0,
                    };
                    if (score <= 0) continue;
                    if (position.Distance(city.Position) <= 1) continue;
                    score += Math.Min(3, cityThreats.Count);
                    candidates.Add(new AiConstructionCandidate(unit.Id, definition.Code,
                        position, score, cityThreats.Count > 0 ? "도시 접근축 방어" : "정찰된 전선 준비"));
                }
            }
        }

        return candidates.OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DefinitionCode, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Position.Q).ThenBy(candidate => candidate.Position.R)
            .ThenBy(candidate => candidate.Unit.Value).ToList();
    }

    private static bool AllowedTerrain(FieldBuildingKind kind, TerrainType terrain)
        => kind == FieldBuildingKind.ScoutPost
            ? terrain is TerrainType.Forest or TerrainType.Mountain
            : terrain is TerrainType.Plains or TerrainType.Forest or TerrainType.Desert;

    private static int CountUnseen(HexMap map, IReadOnlySet<HexCoord> visible,
        HexCoord center, int radius)
        => map.Tiles().Count(tile => tile.Distance(center) <= radius && !visible.Contains(tile));
}
