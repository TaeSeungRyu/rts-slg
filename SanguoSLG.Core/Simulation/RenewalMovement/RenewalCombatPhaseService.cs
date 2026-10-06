namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed class RenewalCombatPhaseService
{
    private readonly CombatPhaseResolver _unitResolver;
    private readonly BattleResolver _battle;
    private readonly int _woundedPercent;

    public RenewalCombatPhaseService(BalanceConfig balance)
    {
        _battle = new BattleResolver(balance.MultiTargetSecondaryPercent);
        _unitResolver = new CombatPhaseResolver(_battle, balance.WoundedPercent);
        _woundedPercent = balance.WoundedPercent;
    }

    public RenewalCombatResolution Resolve(RenewalAdvanceState state)
    {
        var units = state.Units.Where(x => x.IsActive).ToDictionary(x => x.Id);
        var allProfiles = state.CombatProfiles
            ?? new Dictionary<UnitId, RenewalCombatProfile>();
        var profiles = allProfiles
            .Where(x => units.ContainsKey(x.Key))
            .ToDictionary(x => x.Key, x => x.Value);
        var events = new List<RenewalAdvanceEvent>();
        var intents = CollectUnitIntents(units, profiles);
        var engagements = BuildEngagements(intents, units, profiles);
        var combatParticipants = CollectCombatParticipants(state, engagements, units, profiles);
        var chargedProfiles = profiles.ToDictionary(x => x.Key, x => combatParticipants.Contains(x.Key)
            ? x.Value with
            {
                CombatState = x.Value.CombatState?.AdvanceCombat(),
                CombatGrowthAwards = x.Value.CombatGrowthAwards + 1,
            }
            : x.Value);
        foreach (var id in combatParticipants.OrderBy(x => x.Value))
        {
            events.Add(new RenewalAdvanceEvent(
                RenewalAdvanceEventKind.CombatParticipationRecorded,
                state.Day, RenewalAdvancePhase.Attack, state.MovementTick, id,
                Amount: chargedProfiles[id].CombatGrowthAwards));
        }
        var participants = chargedProfiles.ToDictionary(x => x.Key,
            x => x.Value.Participant with
            {
                Mode = ToLegacyMode(units[x.Key].Mode),
                AttackRange = units[x.Key].AttackRange,
            });
        var field = _unitResolver.Resolve(engagements, participants);
        var pools = field.Pools.ToDictionary(x => x.Key, x => x.Value);

        foreach (var (target, amount) in field.DamageTaken.OrderBy(x => x.Key.Value))
        {
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.AttackResolved,
                state.Day, RenewalAdvancePhase.Attack, state.MovementTick,
                Target: RenewalTargetId.ForUnit(target), Amount: amount));
        }

        var structures = ResolveStructures(state, units, chargedProfiles, events);
        var sites = ResolveSites(state, units, chargedProfiles, pools, events, out var waiting);
        var nextProfiles = allProfiles.ToDictionary(x => x.Key, x =>
            chargedProfiles.TryGetValue(x.Key, out var charged)
                ? charged with
                {
                    Participant = pools.TryGetValue(x.Key, out var pool)
                        ? charged.Participant with { Pool = pool }
                        : charged.Participant,
                }
                : x.Value);
        TransferDefeatedLoot(engagements, units, nextProfiles, events, state);
        var nextUnits = state.Units.Select(unit => ApplyUnitOutcome(unit, pools, waiting, state, events))
            .ToList();

        return new RenewalCombatResolution(state with
        {
            Units = nextUnits,
            CombatProfiles = nextProfiles,
            Structures = structures,
            Sites = sites,
        }, events);
    }

    private static HashSet<UnitId> CollectCombatParticipants(RenewalAdvanceState state,
        IReadOnlyList<UnitEngagement> engagements,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles)
    {
        var result = engagements.SelectMany(x => x.Targets.Append(x.Attacker)).ToHashSet();
        foreach (var structure in state.Structures ?? Array.Empty<RenewalStructureCombatState>())
        {
            result.UnionWith(AttackersFor(structure.Id, structure.Owner, structure.Position,
                units, profiles, x => x.BuildingAttackRange).Select(x => x.Id));
        }
        foreach (var site in state.Sites ?? Array.Empty<RenewalSiteCombatState>())
        {
            var participating = AttackersFor(site.Id, site.Owner, site.Position, units, profiles,
                    x => x.CastleAttackRange)
                .OrderBy(x => x.AttackRangeReachedTick
                    ?? checked((state.Day - 1) * RenewalAdvanceSimulator.MovementTicksPerDay
                        + state.MovementTick))
                .ThenBy(x => x.CommandId).ThenBy(x => x.Id.Value)
                .Take(site.ParticipationLimit).Select(x => x.Id);
            result.UnionWith(participating);
        }
        return result;
    }

    private static void TransferDefeatedLoot(IReadOnlyList<UnitEngagement> engagements,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IDictionary<UnitId, RenewalCombatProfile> profiles,
        ICollection<RenewalAdvanceEvent> events, RenewalAdvanceState state)
    {
        foreach (var defeated in profiles.Values
            .Where(x => x.Participant.Pool.Active <= 0
                && (x.CarryingGold > 0 || x.Provisions > 0))
            .OrderBy(x => x.Unit.Value).ToList())
        {
            var looter = engagements
                .Where(x => x.Targets.Contains(defeated.Unit)
                    && profiles.TryGetValue(x.Attacker, out var candidate)
                    && candidate.Participant.Pool.Active > 0)
                .OrderBy(x => units[x.Attacker].CommandId)
                .ThenBy(x => x.Attacker.Value)
                .Select(x => (UnitId?)x.Attacker).FirstOrDefault();
            if (looter is not { } looterId || !profiles.TryGetValue(looterId, out var receiver))
            {
                continue;
            }
            profiles[looterId] = receiver with
            {
                CarryingGold = receiver.CarryingGold + defeated.CarryingGold,
                Provisions = receiver.Provisions + defeated.Provisions,
            };
            profiles[defeated.Unit] = defeated with { CarryingGold = 0, Provisions = 0 };
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.LootTransferred,
                state.Day, RenewalAdvancePhase.AttackAftermath, state.MovementTick,
                looterId, Target: RenewalTargetId.ForUnit(defeated.Unit),
                Amount: defeated.CarryingGold + defeated.Provisions));
        }
    }

    private static Dictionary<UnitId, UnitId> CollectUnitIntents(
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles)
    {
        var intents = new Dictionary<UnitId, UnitId>();
        foreach (var attacker in units.Values.OrderBy(x => x.CommandId).ThenBy(x => x.Id.Value))
        {
            if (!profiles.ContainsKey(attacker.Id))
            {
                continue;
            }
            var selected = SelectedTarget(attacker);
            if (selected is not { Kind: RenewalTargetKind.Unit } target
                || !units.TryGetValue(new UnitId(checked((int)target.Value)), out var defender)
                || defender.Owner == attacker.Owner
                || !profiles.ContainsKey(defender.Id)
                || !InRange(attacker, defender.Position))
            {
                continue;
            }
            intents[attacker.Id] = defender.Id;
        }
        return intents;
    }

    private static IReadOnlyList<UnitEngagement> BuildEngagements(
        IReadOnlyDictionary<UnitId, UnitId> intents,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles)
    {
        var targets = intents.ToDictionary(x => x.Key, x => new List<UnitId> { x.Value });
        foreach (var edge in intents.OrderBy(x => units[x.Key].CommandId).ThenBy(x => x.Key.Value))
        {
            var defender = units[edge.Value];
            var attacker = units[edge.Key];
            if (!CanCounter(defender, attacker, profiles))
            {
                continue;
            }
            if (!targets.TryGetValue(defender.Id, out var counterTargets))
            {
                counterTargets = [];
                targets[defender.Id] = counterTargets;
            }
            if (!counterTargets.Contains(attacker.Id))
            {
                counterTargets.Add(attacker.Id);
            }
        }

        return targets
            .OrderBy(x => units[x.Key].CommandId).ThenBy(x => x.Key.Value)
            .Select(x => new UnitEngagement(x.Key, x.Value))
            .ToList();
    }

    private static bool CanCounter(RenewalUnitState defender, RenewalUnitState attacker,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles)
    {
        if (!profiles.ContainsKey(defender.Id) || defender.Mode == RenewalOrderMode.March
            || !InRange(defender, attacker.Position))
        {
            return false;
        }
        return defender.Mode != RenewalOrderMode.Attack
            || defender.AssignedTarget == RenewalTargetId.ForUnit(attacker.Id);
    }

    private IReadOnlyList<RenewalStructureCombatState> ResolveStructures(
        RenewalAdvanceState state,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles,
        ICollection<RenewalAdvanceEvent> events)
    {
        var result = new List<RenewalStructureCombatState>();
        foreach (var structure in state.Structures ?? Array.Empty<RenewalStructureCombatState>())
        {
            var hp = structure.HitPoints;
            foreach (var attacker in AttackersFor(structure.Id, structure.Owner,
                structure.Position, units, profiles, x => x.BuildingAttackRange))
            {
                var profile = profiles[attacker.Id];
                var attackStats = profile.Participant.Stats with
                {
                    Troops = profile.Participant.Pool.Active,
                    AtkStat = profile.BuildingAttack ?? profile.Participant.Stats.AtkStat,
                };
                hp = Math.Max(0, hp - _battle.Damage(attackStats,
                    new CombatStats(hp, 0, structure.Defense)));
            }
            var damage = structure.HitPoints - hp;
            var updated = structure with { HitPoints = hp };
            result.Add(updated);
            if (damage > 0)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.StructureDamaged,
                    state.Day, RenewalAdvancePhase.Attack, state.MovementTick,
                    Target: structure.Id, Amount: damage));
            }
            if (structure.HitPoints > 0 && hp == 0)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.StructureDestroyed,
                    state.Day, RenewalAdvancePhase.Attack, state.MovementTick,
                    Target: structure.Id));
            }
        }
        return result;
    }

    private IReadOnlyList<RenewalSiteCombatState> ResolveSites(
        RenewalAdvanceState state,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles,
        IDictionary<UnitId, TroopPool> pools,
        ICollection<RenewalAdvanceEvent> events,
        out HashSet<UnitId> waiting)
    {
        waiting = [];
        var result = new List<RenewalSiteCombatState>();
        foreach (var site in state.Sites ?? Array.Empty<RenewalSiteCombatState>())
        {
            var candidates = AttackersFor(site.Id, site.Owner, site.Position, units, profiles,
                    x => x.CastleAttackRange)
                .OrderBy(x => x.AttackRangeReachedTick
                    ?? checked((state.Day - 1) * RenewalAdvanceSimulator.MovementTicksPerDay
                        + state.MovementTick))
                .ThenBy(x => x.CommandId).ThenBy(x => x.Id.Value).ToList();
            var participating = candidates.Take(site.ParticipationLimit).ToList();
            foreach (var excluded in candidates.Skip(site.ParticipationLimit))
            {
                waiting.Add(excluded.Id);
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.SiegeWaiting,
                    state.Day, RenewalAdvancePhase.Attack, state.MovementTick, excluded.Id,
                    Target: site.Id, Amount: site.ParticipationLimit));
            }
            if (participating.Count == 0)
            {
                result.Add(site);
                continue;
            }

            var siegeAttackers = participating.Select(attacker =>
            {
                var profile = profiles[attacker.Id];
                var participant = profile.Participant;
                return new SiegeAttacker(participant.Pool.Active,
                    profile.BuildingAttack ?? participant.Stats.AtkStat,
                    participant.Stats.AtkStat,
                    participant.Stats.DfStat,
                    participant.Stats.AptitudePercent,
                    participant.Stats.AtkBonusPercent,
                    participant.Stats.DfBonusPercent,
                    attacker.Position.DistanceTo(site.Position) <= ContinuousPosition.UnitsPerTile);
            }).ToList();
            var outcome = _battle.ResolveSiege(siegeAttackers, site.Castle);
            for (var i = 0; i < participating.Count; i++)
            {
                var id = participating[i].Id;
                pools[id] = pools[id].TakeDamage(outcome.CounterDamage[i], _woundedPercent);
            }
            var nextCastle = site.Castle with
            {
                WallCurrent = outcome.NewWall,
                Troops = Math.Max(0, site.Castle.Troops - outcome.TroopDamage),
            };
            var captured = nextCastle.WallCurrent == 0 && nextCastle.Troops == 0;
            var owner = captured ? participating[0].Owner : site.Owner;
            var updated = site with { Castle = nextCastle, Owner = owner };
            result.Add(updated);
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.SiteDamaged,
                state.Day, RenewalAdvancePhase.Attack, state.MovementTick,
                Target: site.Id, Amount: outcome.WallDamage + outcome.TroopDamage));
            if (captured)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.SiteCaptured,
                    state.Day, RenewalAdvancePhase.AttackAftermath, state.MovementTick,
                    participating[0].Id, Target: site.Id));
            }
        }
        return result;
    }

    private static IEnumerable<RenewalUnitState> AttackersFor(RenewalTargetId target,
        FactionId owner, ContinuousPosition position,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles,
        Func<RenewalUnitState, int> range) => units.Values
        .Where(unit => profiles.ContainsKey(unit.Id)
            && unit.Owner != owner
            && SelectedTarget(unit) == target
            && InRange(unit, position, range(unit)));

    private static RenewalUnitState ApplyUnitOutcome(RenewalUnitState unit,
        IReadOnlyDictionary<UnitId, TroopPool> pools, IReadOnlySet<UnitId> waiting,
        RenewalAdvanceState state, ICollection<RenewalAdvanceEvent> events)
    {
        if (!pools.TryGetValue(unit.Id, out var pool))
        {
            return unit;
        }
        if (pool.Active <= 0 && unit.IsActive)
        {
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.UnitDefeated,
                state.Day, RenewalAdvancePhase.AttackAftermath, state.MovementTick, unit.Id,
                unit.Position, unit.Position));
            return unit with
            {
                IsActive = false,
                Mode = RenewalOrderMode.Standby,
                Arrived = true,
                StopReason = RenewalStopReason.None,
            };
        }
        return waiting.Contains(unit.Id)
            ? unit with { StopReason = RenewalStopReason.SiegeCapacity }
            : unit.StopReason == RenewalStopReason.SiegeCapacity
                ? unit with { StopReason = RenewalStopReason.TargetInRange }
                : unit;
    }

    private static RenewalTargetId? SelectedTarget(RenewalUnitState unit) => unit.Mode switch
    {
        RenewalOrderMode.Advance => unit.PursuitTarget,
        RenewalOrderMode.Attack => unit.AssignedTarget,
        _ => null,
    };

    private static bool InRange(RenewalUnitState unit, ContinuousPosition target) =>
        InRange(unit, target, unit.AttackRange);

    private static bool InRange(RenewalUnitState unit, ContinuousPosition target, int range) =>
        unit.Position.DistanceTo(target)
            <= Math.Max(0, range) * ContinuousPosition.UnitsPerTile;

    private static UnitMode ToLegacyMode(RenewalOrderMode mode) => mode switch
    {
        RenewalOrderMode.March => UnitMode.March,
        RenewalOrderMode.Attack => UnitMode.Attack,
        _ => UnitMode.Advance,
    };
}
