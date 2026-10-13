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
        foreach (var encounter in state.Encounters.Where(e => e.Unit is null && e.DefeatedDay is null
                && e.ScheduledDay <= day).OrderBy(e => e.ScheduledDay).ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            var definition = Definition(encounter.Code);
            if (definition is null) continue;
            var position = FindSpawnPosition(state, encounter);
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

    public HexCoord? FindSpawnPosition(GameState state, EventEncounter encounter)
    {
        var definition = Definition(encounter.Code);
        var city = state.Cities.FirstOrDefault(city => city.Id == encounter.DiscoveryCity);
        if (definition is null || city is null) return null;
        var occupied = state.Cities.SelectMany(CastleFootprint.TilesFor)
            .Concat(state.Placements.Select(facility => facility.Plot))
            .Concat(state.Buildings.Select(building => building.Position))
            .Concat(state.Ruins.Select(ruin => ruin.Position))
            .Concat(state.Armies.Select(unit => unit.RenewalPosition is { } position
                ? RenewalHexSpace.NearestHex(position) : unit.Field.Position)).ToHashSet();
        var terrains = definition.SpawnTerrains.Concat(definition.FallbackSpawnTerrains ?? []).ToHashSet();
        return map.Tiles().Where(tile => !occupied.Contains(tile) && terrains.Contains(map.TerrainAt(tile))
                && TerrainRules.CanEnter(MovementDomain.Land, map.TerrainAt(tile))
                && tile.Distance(city.Position) >= definition.SpawnMinDistance
                && tile.Distance(city.Position) <= definition.SpawnMaxDistance)
            .OrderBy(tile => definition.SpawnTerrains.Contains(map.TerrainAt(tile)) ? 0 : 1)
            .ThenBy(tile => tile.Distance(city.Position)).ThenBy(tile => tile.Q).ThenBy(tile => tile.R)
            .Select(tile => (HexCoord?)tile).FirstOrDefault();
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

    public GameState ResolveCombat(GameState state, IReadOnlyList<CombatUnit> before,
        AdvanceTurn turn, int day, out IReadOnlyList<WorldEvent> events)
    {
        var encounters = state.Encounters.ToList();
        var treasures = state.Treasures.ToList();
        var generals = state.Generals.ToList();
        var reports = new List<WorldEvent>();
        for (var index = 0; index < encounters.Count; index++)
        {
            var encounter = encounters[index];
            if (encounter.DefeatedDay is not null || encounter.Unit is not { } unitId
                || Definition(encounter.Code) is not { } definition
                || !before.Any(unit => unit.Id == unitId && unit.Pool.Active > 0)) continue;
            var attackerIds = turn.FieldCombatExchanges.Where(exchange => exchange.Target == unitId)
                .Select(exchange => exchange.Attacker).ToHashSet();
            var tacticIds = TacticContributors(before, turn, unitId).ToHashSet();
            attackerIds.UnionWith(tacticIds);
            var attacker = before.Where(unit => attackerIds.Contains(unit.Id) && unit.Field.Owner.Value > 0
                    && (turn.Combat?.DamageDealt.GetValueOrDefault(unit.Id) > 0 || tacticIds.Contains(unit.Id)))
                .OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value).FirstOrDefault();
            if (attacker is not null)
                encounter = encounter with { Contributor = new(attacker.Id, attacker.Field.Owner,
                    new[] { attacker.VanguardId, attacker.AdjutantId }.OfType<GeneralId>().Distinct().ToList(),
                    attacker.IsArmyGroup ? [TroopClass.Infantry, TroopClass.Archer, TroopClass.Siege]
                        : [attacker.IsSupply ? TroopClass.Supply : attacker.Class]) };
            if (turn.Units.Any(unit => unit.Id == unitId && unit.Pool.Active > 0))
            {
                encounters[index] = encounter;
                continue;
            }
            var damaged = turn.Combat?.DamageTaken.GetValueOrDefault(unitId) > 0
                || turn.StatusDamage.GetValueOrDefault(unitId) > 0 || turn.StratagemDamage.GetValueOrDefault(unitId) > 0;
            var contributor = damaged ? encounter.Contributor : null;
            encounters[index] = encounter with { DefeatedDay = day, RewardFaction = contributor?.Faction };
            if (contributor is null) continue;
            treasures.Add(new($"{encounter.Id}:reward", definition.TreasureCode, definition.TreasureName,
                contributor.Faction, day, encounter.Id));
            foreach (var generalId in contributor.Generals)
            {
                var generalIndex = generals.FindIndex(general => general.Id == generalId);
                if (generalIndex < 0) continue;
                var general = GeneralGrowth.AddGeneralExperience(generals[generalIndex], definition.ExperienceReward, out _);
                foreach (var troopClass in contributor.TroopClasses)
                    general = AptitudeGrowth.AddExperience(general, troopClass, definition.AptitudeReward, out _);
                generals[generalIndex] = general;
            }
            reports.Add(new(WorldEventKind.EventUnitDefeated, contributor.Faction,
                contributor.Generals.Select(id => (GeneralId?)id).FirstOrDefault(), encounter.DiscoveryCity,
                definition.ExperienceReward, definition.Code, definition.AptitudeReward));
        }
        events = reports;
        return state with { EventEncounters = encounters, FactionTreasures = treasures, Generals = generals };
    }

    private static IEnumerable<UnitId> TacticContributors(IReadOnlyList<CombatUnit> before,
        AdvanceTurn turn, UnitId eventUnit)
    {
        var moved = turn.Movement.Units.ToDictionary(unit => unit.Id);
        var beast = before.FirstOrDefault(unit => unit.Id == eventUnit);
        if (beast is null) yield break;
        var beastPosition = moved.GetValueOrDefault(eventUnit)?.Position ?? beast.Field.Position;
        foreach (var (casterId, skill) in turn.FiredActives)
        {
            if (skill.Code is not ("fire_plot" or "lightning")) continue;
            var caster = before.FirstOrDefault(unit => unit.Id == casterId);
            if (caster is null) continue;
            var casterPosition = moved.GetValueOrDefault(casterId)?.Position ?? caster.Field.Position;
            var range = skill.Code == "lightning" ? 3 : 1;
            var target = before.Where(unit => unit.Field.Owner != caster.Field.Owner && unit.Pool.Active > 0)
                .Select(unit => (Unit: unit, Position: moved.GetValueOrDefault(unit.Id)?.Position ?? unit.Field.Position))
                .Where(entry => entry.Position.Distance(casterPosition) <= range)
                .OrderBy(entry => entry.Position.Distance(casterPosition)).ThenBy(entry => entry.Unit.Id.Value)
                .FirstOrDefault();
            if (target.Unit is not null && target.Position.Distance(beastPosition) <= 1) yield return casterId;
        }
    }
}
