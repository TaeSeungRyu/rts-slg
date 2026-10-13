namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class EventHuntService(HexMap map, IReadOnlyList<EventUnitDefinition> definitions)
{
    public static readonly FactionId EventFaction = new(-19);

    public EventUnitDefinition? Definition(string code)
        => definitions.FirstOrDefault(definition => definition.Code == code);

    public GameState ScheduleDiscoveries(GameState state)
    {
        var encounters = state.Encounters.ToList();
        var known = encounters.Select(encounter => encounter.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var discovery in state.Discoveries.OrderBy(d => d.Day)
            .ThenBy(d => d.Faction.Value).ThenBy(d => d.City.Value).ThenBy(d => d.Explorer.Value))
        {
            var definition = definitions.FirstOrDefault(d => d.DiscoveryCode == discovery.Code);
            if (definition is null) continue;
            var id = $"{discovery.Day}:{discovery.Faction.Value}:{discovery.City.Value}:{discovery.Explorer.Value}:{discovery.Code}";
            if (!known.Add(id)) continue;
            encounters.Add(new(id, definition.Code, discovery.Faction, discovery.City,
                discovery.Day + definition.SpawnDelayDays));
        }
        return state with { EventEncounters = encounters };
    }

    public GameState SpawnDue(GameState state, int day, out IReadOnlyList<WorldEvent> events)
    {
        events = [];
        if (state.Armies.Any(unit => unit.Field.Owner == EventFaction && unit.Pool.Active > 0)) return state;
        var occupied = state.Cities.SelectMany(CastleFootprint.TilesFor)
            .Concat(state.Placements.Select(facility => facility.Plot))
            .Concat(state.Buildings.Select(building => building.Position))
            .Concat(state.Ruins.Select(ruin => ruin.Position))
            .Concat(state.Armies.Select(unit => unit.RenewalPosition is { } position
                ? RenewalHexSpace.NearestHex(position) : unit.Field.Position)).ToHashSet();
        foreach (var encounter in state.Encounters.Where(e => e.Unit is null && e.DefeatedDay is null
                && e.ScheduledDay <= day).OrderBy(e => e.ScheduledDay).ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            var definition = Definition(encounter.Code);
            var city = state.Cities.FirstOrDefault(c => c.Id == encounter.DiscoveryCity);
            if (definition is null || city is null) continue;
            var position = map.Tiles().Where(tile => !occupied.Contains(tile)
                    && definition.SpawnTerrains.Contains(map.TerrainAt(tile))
                    && TerrainRules.CanEnter(MovementDomain.Land, map.TerrainAt(tile))
                    && tile.Distance(city.Position) >= definition.SpawnMinDistance
                    && tile.Distance(city.Position) <= definition.SpawnMaxDistance)
                .OrderBy(tile => tile.Distance(city.Position)).ThenBy(tile => tile.Q).ThenBy(tile => tile.R)
                .Select(tile => (HexCoord?)tile).FirstOrDefault();
            if (position is null) continue;
            var id = new UnitId(state.Armies.Select(u => u.Id.Value)
                .Concat(state.Encounters.Where(e => e.Unit is not null).Select(e => e.Unit!.Value.Value))
                .Append(0).Max() + 1);
            var field = new FieldUnit(id, EventFaction, position.Value, definition.Speed, definition.Detection,
                definition.Range, MovementDomain.Land, UnitMode.Standby, null, id.Value);
            var unit = new CombatUnit(field,
                new CombatStats(definition.Troops, definition.Attack, definition.Defense, AptitudeGrade.A.Percent()),
                new TroopPool(definition.Troops, 0), UnitCombatState.Create(0),
                MaxTroops: definition.Troops, Class: TroopClass.Cavalry, TroopCode: definition.Code,
                RenewalPosition: RenewalHexSpace.Center(position.Value));
            events = [new(WorldEventKind.EventUnitAppeared, encounter.DiscoveringFaction,
                City: encounter.DiscoveryCity, Amount: definition.Troops, Code: definition.Code,
                ExtraAmount: id.Value)];
            return state with
            {
                FieldArmies = state.Armies.Append(unit).ToList(),
                EventEncounters = state.Encounters.Select(e => e.Id == encounter.Id ? e with { Unit = id } : e).ToList(),
            };
        }
        return state;
    }

    public GameState PlanDaily(GameState state)
    {
        var garrisons = state.Buildings.Where(building => building.GarrisonUnit is not null)
            .Select(building => building.GarrisonUnit!.Value).ToHashSet();
        return state with { FieldArmies = state.Armies.Select(unit =>
        {
            if (unit.Field.Owner != EventFaction || Definition(unit.TroopCode) is not { } definition) return unit;
            var center = unit.RenewalPosition ?? RenewalHexSpace.Center(unit.Field.Position);
            var target = state.Armies.Where(candidate => candidate.Field.Owner != EventFaction
                    && candidate.Pool.Active > 0 && !candidate.IsWaitingDeployment
                    && candidate.Field.Domain is MovementDomain.Land or MovementDomain.LandMountain
                    && !garrisons.Contains(candidate.Id)
                    && center.DistanceTo(candidate.RenewalPosition ?? RenewalHexSpace.Center(candidate.Field.Position))
                        <= (long)definition.Detection * ContinuousPosition.UnitsPerTile)
                .OrderBy(candidate => candidate.Pool.Active).ThenBy(candidate => candidate.Id.Value).FirstOrDefault();
            return unit with { Field = unit.Field with
            {
                Mode = target is null ? UnitMode.Standby : UnitMode.Attack,
                Target = target?.Field.Position,
                ContinuousTarget = target is null ? null
                    : target.RenewalPosition ?? RenewalHexSpace.Center(target.Field.Position),
                AssignedUnitTarget = target?.Id, PursuitTarget = null, ReturnCity = null,
                Waypoints = null, ContinuousWaypoints = null,
            } };
        }).ToList() };
    }
}
