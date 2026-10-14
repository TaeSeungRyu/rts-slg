namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class EventHuntService(HexMap map, IReadOnlyList<EventUnitDefinition> definitions,
    IReadOnlyList<EventTreasureDefinition>? treasureDefinitions = null)
{
    public static readonly FactionId EventFaction = new(-19);

    public EventUnitDefinition? Definition(string code)
        => definitions.FirstOrDefault(definition => definition.Code == code);

    public IReadOnlyList<EventTreasureDefinition> RewardChoices(string eventCode)
        => (treasureDefinitions ?? []).Where(item => item.Source == eventCode).ToList();

    public CommandResult ChooseReward(GameState state, string encounterId, FactionId faction, string treasureCode)
    {
        var encounter = state.Encounters.FirstOrDefault(x => x.Id == encounterId);
        var definition = RewardChoices(encounter?.Code ?? "").FirstOrDefault(x => x.Code == treasureCode);
        if (encounter is not { RewardChoicePending: true } || encounter.RewardFaction != faction
            || definition is null)
            return CommandResult.Fail("선택할 수 없는 신수 보상입니다.", state);
        var rewardId = $"{encounter.Id}:reward";
        if (!state.Treasures.Any(x => x.Id == rewardId))
            return CommandResult.Fail("처치 보상을 찾을 수 없습니다.", state);
        return CommandResult.Success(state with
        {
            FactionTreasures = state.Treasures.Select(x => x.Id == rewardId
                ? x with { Code = definition.Code, Name = definition.Name } : x).ToList(),
            EventEncounters = state.Encounters.Select(x => x.Id == encounterId
                ? x with { RewardChoicePending = false } : x).ToList(),
        });
    }

    /// <summary>AI도 플레이어와 같은 한 번의 3지선다 보상을 확정한다. 우선순위는 현재 세력의 편성 장수에 대한 효용이다.</summary>
    public GameState ChooseAiRewards(GameState state, FactionId playerFaction)
    {
        foreach (var encounter in state.Encounters.Where(x => x.RewardChoicePending
                     && x.RewardFaction is { } owner && owner != playerFaction).ToList())
        {
            var faction = encounter.RewardFaction!.Value;
            var choices = RewardChoices(encounter.Code);
            var activeGenerals = state.Assignments.Where(x => x.Faction == faction)
                .Select(x => state.Generals.FirstOrDefault(g => g.Id == x.General))
                .Where(g => g is not null).ToList();
            var choice = choices.Select((item, index) => (Item: item, Index: index,
                    Score: item.Effect switch
                    {
                        "might" => activeGenerals.Any(g => g!.Might <= 90) ? 100 : 0,
                        "aptitude_floor" => activeGenerals.Any(g => g!.Aptitudes.Values.Any(a => a < AptitudeGrade.A)) ? 100 : 0,
                        "melee_attack" => activeGenerals.Count > 0 ? 70 : 0,
                        "normal_attack" or "damage_reduction" or "fatal_to_wounded" => 60,
                        "summon" => 40,
                        _ => 0,
                    }))
                .OrderByDescending(x => x.Score).ThenBy(x => x.Index).FirstOrDefault();
            if (choice.Item is null) continue;
            var result = ChooseReward(state, encounter.Id, faction, choice.Item.Code);
            if (!result.Ok) continue;
            state = result.State;
            var equipment = new TreasureEquipmentService(treasureDefinitions ?? []);
            foreach (var general in activeGenerals.Where(g => g is not null)
                         .OrderByDescending(g => g!.Might).ThenBy(g => g!.Id.Value))
            {
                var equipped = equipment.Equip(state, $"{encounter.Id}:reward", general!.Id);
                if (!equipped.Ok) continue;
                state = equipped.State;
                break;
            }
        }
        return state;
    }

    public GameState ScheduleDiscoveries(GameState state)
    {
        var encounters = state.Encounters.ToList();
        var known = encounters.Select(encounter => encounter.Id).ToHashSet(StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var discovery in state.Discoveries.OrderBy(d => d.Day)
            .ThenBy(d => d.Faction.Value).ThenBy(d => d.City.Value).ThenBy(d => d.Explorer.Value))
        {
            var definition = definitions.FirstOrDefault(d => d.DiscoveryCode == discovery.Code);
            if (definition is null) continue;
            var id = $"{discovery.Day}:{discovery.Faction.Value}:{discovery.City.Value}:{discovery.Explorer.Value}:{discovery.Code}";
            occurrences.TryGetValue(id, out var occurrence);
            occurrences[id] = occurrence + 1;
            if (occurrence > 0) id = $"{id}:{occurrence + 1}";
            if (!known.Add(id)) continue;
            encounters.Add(new(id, definition.Code, discovery.Faction, discovery.City,
                discovery.Day + definition.SpawnDelayDays));
        }
        return state with { EventEncounters = encounters };
    }

    public GameState SpawnDue(GameState state, int day, out IReadOnlyList<WorldEvent> events)
    {
        events = [];
        var appeared = new List<WorldEvent>();
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
            appeared.Add(new(WorldEventKind.EventUnitAppeared, encounter.DiscoveringFaction,
                City: encounter.DiscoveryCity, Amount: definition.Troops, Code: definition.Code,
                ExtraAmount: id.Value));
            state = state with
            {
                FieldArmies = state.Armies.Append(unit).ToList(),
                EventEncounters = state.Encounters.Select(e => e.Id == encounter.Id ? e with { Unit = id } : e).ToList(),
            };
        }
        events = appeared;
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
            .OrderBy(tile => tile.Distance(city.Position))
            .ThenByDescending(tile => state.Cities.Where(other => other.Id != city.Id)
                .Select(other => tile.Distance(other.Position)).DefaultIfEmpty(int.MaxValue).Min())
            .ThenBy(tile => definition.SpawnTerrains.Contains(map.TerrainAt(tile)) ? 0 : 1)
            .ThenBy(tile => tile.Q).ThenBy(tile => tile.R)
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
            var cityTarget = target is null && unit.TroopCode == "dragon"
                ? state.Cities.Where(city => city.Owner != EventFaction && city.Wall > 0
                        && center.DistanceTo(RenewalHexSpace.Center(city.Position))
                            <= (long)definition.Detection * ContinuousPosition.UnitsPerTile)
                    .OrderBy(city => center.DistanceTo(RenewalHexSpace.Center(city.Position)))
                    .ThenBy(city => city.Id.Value).FirstOrDefault()
                : null;
            return unit with { Field = unit.Field with
            {
                Mode = target is null && cityTarget is null ? UnitMode.Standby : UnitMode.Attack,
                Target = target?.Field.Position ?? cityTarget?.Position,
                ContinuousTarget = target is null
                    ? cityTarget is null ? null : RenewalHexSpace.Center(cityTarget.Position)
                    : target.RenewalPosition ?? RenewalHexSpace.Center(target.Field.Position),
                AssignedUnitTarget = target?.Id, PursuitTarget = null, ReturnCity = null,
                Waypoints = null, ContinuousWaypoints = null,
            } };
        }).ToList() };
    }

    /// <summary>용의 주 공격이 적중하면 목표 주변 1칸의 다른 적에게 주 피해의 20%를 준다.</summary>
    public AdvanceTurn ApplyDragonSplash(IReadOnlyList<CombatUnit> before, AdvanceTurn turn)
    {
        if (turn.Combat is not { } combat || turn.FieldCombatExchanges.Count == 0) return turn;
        var units = turn.Units.ToDictionary(unit => unit.Id);
        var original = before.ToDictionary(unit => unit.Id);
        var damageTaken = combat.DamageTaken.ToDictionary();
        var damageDealt = combat.DamageDealt.ToDictionary();
        var pools = combat.Pools.ToDictionary();
        var exchanges = (combat.Exchanges ?? []).ToList();
        foreach (var hit in turn.FieldCombatExchanges.Where(hit => hit.IsPrimaryTarget))
        {
            if (!original.TryGetValue(hit.Attacker, out var dragon) || dragon.TroopCode != "dragon"
                || dragon.Field.Owner != EventFaction || !original.ContainsKey(hit.Target)) continue;
            var primaryDamage = exchanges.Where(x => x.Attacker == dragon.Id && x.Target == hit.Target)
                .Sum(x => x.Amount);
            var splash = primaryDamage * 20 / 100;
            if (splash <= 0) continue;
            foreach (var victim in units.Values.Where(unit => unit.Id != hit.Target
                         && unit.Field.Owner != EventFaction && unit.Pool.Active > 0
                         && unit.Field.Position.Distance(RenewalHexSpace.NearestHex(hit.TargetPosition)) <= 1)
                     .OrderBy(unit => unit.Id.Value).ToList())
            {
                var activeBefore = victim.Pool.Active;
                var pool = victim.Pool.TakeDamage(splash, 70);
                var dealt = activeBefore - pool.Active;
                if (dealt <= 0) continue;
                units[victim.Id] = victim with { Pool = pool };
                pools[victim.Id] = pool;
                damageTaken[victim.Id] = damageTaken.GetValueOrDefault(victim.Id) + dealt;
                damageDealt[dragon.Id] = damageDealt.GetValueOrDefault(dragon.Id) + dealt;
                exchanges.Add(new UnitDamage(dragon.Id, victim.Id, dealt));
            }
        }
        return turn with { Units = turn.Units.Select(unit => units[unit.Id]).ToList(),
            Combat = combat with { DamageTaken = damageTaken, DamageDealt = damageDealt,
                Pools = pools, Exchanges = exchanges } };
    }

    /// <summary>교전에 참여한 봉황은 부상병을 최대 병력의 5%까지 회복한다. 전멸 시에는 1회만 부활한다.</summary>
    public (GameState State, AdvanceTurn Turn) ApplyPhoenixRecovery(GameState state,
        IReadOnlyList<CombatUnit> before, AdvanceTurn turn)
    {
        if (turn.Combat is not { } combat) return (state, turn);
        var units = turn.Units.ToDictionary(unit => unit.Id);
        var pools = combat.Pools.ToDictionary();
        var encounters = state.Encounters.ToList();
        var recoveries = turn.BeastRecoveries.ToDictionary();
        foreach (var phoenix in before.Where(unit => unit.Field.Owner == EventFaction
                     && unit.TroopCode == "phoenix").OrderBy(unit => unit.Id.Value))
        {
            if (combat.DamageTaken.GetValueOrDefault(phoenix.Id) <= 0
                && combat.DamageDealt.GetValueOrDefault(phoenix.Id) <= 0) continue;
            var index = encounters.FindIndex(encounter => encounter.Unit == phoenix.Id);
            if (index < 0 || !pools.TryGetValue(phoenix.Id, out var pool)) continue;
            var defeated = pool.Active <= 0;
            if (defeated && encounters[index].RevivedOnce) continue;
            var recovered = pool.Heal(Math.Max(1, phoenix.MaxTroops * 5 / 100));
            if (recovered.Active <= pool.Active) continue;
            recoveries[phoenix.Id] = recovered.Active - pool.Active;
            pools[phoenix.Id] = recovered;
            var unit = units.GetValueOrDefault(phoenix.Id) ?? phoenix;
            var moved = turn.Movement.Units.FirstOrDefault(field => field.Id == phoenix.Id);
            units[phoenix.Id] = unit with { Pool = recovered,
                Field = moved ?? unit.Field,
                RenewalPosition = moved is null ? unit.RenewalPosition
                    : RenewalHexSpace.Center(moved.Position) };
            if (defeated) encounters[index] = encounters[index] with { RevivedOnce = true };
        }
        return (state with { EventEncounters = encounters }, turn with
        {
            Units = units.Values.OrderBy(unit => unit.Id.Value).ToList(),
            Combat = combat with { Pools = pools },
            EventBeastRecoveries = recoveries,
        });
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
            var hits = turn.Combat?.Exchanges?.Where(x => x.Target == unitId && x.Amount > 0)
                .GroupBy(x => x.Attacker).ToDictionary(x => x.Key, x => x.Sum(hit => hit.Amount))
                ?? new Dictionary<UnitId, int>();
            if (hits.Count == 0)
            {
                foreach (var exchange in turn.FieldCombatExchanges.Where(x => x.Target == unitId))
                {
                    var targetCount = turn.FieldCombatExchanges.Count(x => x.Attacker == exchange.Attacker);
                    var amount = turn.Combat?.DamageDealt.GetValueOrDefault(exchange.Attacker) ?? 0;
                    if (amount > 0) hits[exchange.Attacker] = amount / Math.Max(1, targetCount);
                }
            }
            var tacticDamage = turn.StratagemDamage.GetValueOrDefault(unitId);
            foreach (var casterId in tacticIds.Where(id => before.Any(unit => unit.Id == id)))
                hits[casterId] = hits.GetValueOrDefault(casterId) + tacticDamage / Math.Max(1, tacticIds.Count);
            var oldContributions = encounter.DamageContributions?.ToList() ?? [];
            var remaining = before.First(unit => unit.Id == unitId).Pool.Active;
            var rawTotal = hits.Values.Sum();
            foreach (var (attackerId, raw) in hits.OrderBy(x => x.Key.Value))
            {
                var source = before.FirstOrDefault(unit => unit.Id == attackerId && unit.Field.Owner.Value > 0);
                if (source is null || raw <= 0) continue;
                var effective = Math.Min(remaining, rawTotal > remaining
                    ? (int)Math.Ceiling((double)raw * remaining / rawTotal) : raw);
                effective = Math.Max(0, effective);
                remaining -= effective;
                var old = oldContributions.FindIndex(x => x.Unit == attackerId);
                var entry = new EventHuntDamageContribution(source.Id, source.Field.Owner,
                    new[] { source.VanguardId, source.AdjutantId }.OfType<GeneralId>().Distinct().ToList(),
                    source.IsArmyGroup ? [TroopClass.Infantry, TroopClass.Archer, TroopClass.Siege]
                        : [source.IsSupply ? TroopClass.Supply : source.Class],
                    effective + (old < 0 ? 0 : oldContributions[old].Damage));
                if (old < 0) oldContributions.Add(entry);
                else oldContributions[old] = entry;
            }
            if (hits.Count == 0 && turn.StatusDamage.GetValueOrDefault(unitId) > 0
                && encounter.Contributor is { } lastContributor)
            {
                var old = oldContributions.FindIndex(x => x.Unit == lastContributor.Unit);
                var amount = Math.Min(remaining, turn.StatusDamage[unitId]);
                if (old >= 0) oldContributions[old] = oldContributions[old] with
                { Damage = oldContributions[old].Damage + amount };
                else oldContributions.Add(new(lastContributor.Unit, lastContributor.Faction,
                    lastContributor.Generals, lastContributor.TroopClasses, amount));
            }
            encounter = encounter with { DamageContributions = oldContributions };
            if (turn.Units.Any(unit => unit.Id == unitId && unit.Pool.Active > 0))
            {
                encounters[index] = encounter;
                continue;
            }
            var damaged = turn.Combat?.DamageTaken.GetValueOrDefault(unitId) > 0
                || turn.StatusDamage.GetValueOrDefault(unitId) > 0 || turn.StratagemDamage.GetValueOrDefault(unitId) > 0;
            var contributor = damaged ? encounter.Contributor : null;
            encounters[index] = encounter with { DefeatedDay = day, RewardFaction = contributor?.Faction,
                RewardChoicePending = contributor is not null && RewardChoices(encounter.Code).Count > 1 };
            if (contributor is null) continue;
            var damageTotal = oldContributions.Sum(x => x.Damage);
            if (damageTotal <= 0) continue;
            treasures.Add(new($"{encounter.Id}:reward", definition.TreasureCode, definition.TreasureName,
                contributor.Faction, day, encounter.Id));
            foreach (var contribution in oldContributions.Where(x => x.Damage > 0))
            {
                var p = (double)contribution.Damage / damageTotal;
                var experience = 200 + (int)Math.Round(400 * p, MidpointRounding.AwayFromZero);
                var aptitude = 10 + (int)Math.Round(40 * p, MidpointRounding.AwayFromZero);
                foreach (var generalId in contribution.Generals)
                {
                    var generalIndex = generals.FindIndex(general => general.Id == generalId);
                    if (generalIndex < 0) continue;
                    var general = GeneralGrowth.AddGeneralExperience(generals[generalIndex], experience, out _);
                    foreach (var troopClass in contribution.TroopClasses)
                        general = AptitudeGrowth.AddExperience(general, troopClass, aptitude, out _);
                    generals[generalIndex] = general;
                }
            }
            reports.Add(new(WorldEventKind.EventUnitDefeated, contributor.Faction,
                contributor.Generals.Select(id => (GeneralId?)id).FirstOrDefault(), encounter.DiscoveryCity,
                oldContributions.Count == 1 ? 600 : 0, definition.Code,
                oldContributions.Count == 1 ? 50 : 0));
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
