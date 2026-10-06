namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class RenewalCombatPhaseService
{
    private readonly CombatPhaseResolver _unitResolver;
    private readonly BattleResolver _battle;
    private readonly int _woundedPercent;
    private readonly RenewalMovementMap? _movementMap;

    public RenewalCombatPhaseService(BalanceConfig balance, RenewalMovementMap? movementMap = null)
    {
        _battle = new BattleResolver(balance.MultiTargetSecondaryPercent);
        _unitResolver = new CombatPhaseResolver(_battle, balance.WoundedPercent);
        _woundedPercent = balance.WoundedPercent;
        _movementMap = movementMap;
    }

    public RenewalCombatResolution Resolve(RenewalAdvanceState state)
    {
        var units = state.Units.Where(x => x.IsActive).ToDictionary(x => x.Id);
        var events = new List<RenewalAdvanceEvent>();
        var allProfiles = state.CombatProfiles
            ?? new Dictionary<UnitId, RenewalCombatProfile>();
        allProfiles = ApplyExistingStatuses(allProfiles, state, events);
        var profiles = allProfiles
            .Where(x => units.ContainsKey(x.Key) && x.Value.Participant.Pool.Active > 0)
            .ToDictionary(x => x.Key, x => x.Value);
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
        var tacticPrepared = PrepareTacticActives(chargedProfiles, chargedProfiles.Keys.ToHashSet(),
            units, state, events, out var tacticFired);
        intents = CollectUnitIntents(units, tacticPrepared);
        foreach (var casterId in tacticFired.Keys) intents.Remove(casterId);
        engagements = BuildEngagements(intents, units, tacticPrepared)
            .ToList();
        var attackers = engagements.Select(x => x.Attacker).ToHashSet();
        attackers.UnionWith(CollectStructureAndSiteAttackers(state, units, tacticPrepared));
        var prepared = PrepareActives(tacticPrepared, combatParticipants, attackers,
            engagements, units, state, events, out var fired, out var defenseSkills,
            out var armorBreakTargets, tacticFired);
        var participants = prepared.ToDictionary(x => x.Key,
            x => BuildParticipant(prepared[x.Key], units[x.Key],
                fired.GetValueOrDefault(x.Key), defenseSkills.GetValueOrDefault(x.Key)));
        var field = _unitResolver.Resolve(engagements, participants);
        var pools = allProfiles.ToDictionary(x => x.Key, x => x.Value.Participant.Pool);
        foreach (var (id, pool) in field.Pools) pools[id] = pool;

        foreach (var (target, amount) in field.DamageTaken.OrderBy(x => x.Key.Value))
        {
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.AttackResolved,
                state.Day, RenewalAdvancePhase.Attack, state.MovementTick,
                Target: RenewalTargetId.ForUnit(target), Amount: amount));
        }

        foreach (var targetId in armorBreakTargets)
        {
            if (prepared.TryGetValue(targetId, out var target)
                && pools.GetValueOrDefault(targetId)?.Active > 0
                && target.CombatState is { } combatState)
            {
                prepared[targetId] = target with { CombatState = combatState.AddStatus(
                    new StatusEffect(StatusKind.ArmorBreak, 0, 3, false,
                        DfDownPercent: 20)) };
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.StatusApplied,
                    state.Day, RenewalAdvancePhase.AttackAftermath, state.MovementTick,
                    targetId, Detail: StatusKind.ArmorBreak.ToString()));
            }
        }

        var structures = ResolveStructures(state, units, prepared, events);
        var sites = ResolveSites(state, units, prepared, pools, events, out var waiting);
        var nextProfiles = allProfiles.ToDictionary(x => x.Key, x =>
            prepared.TryGetValue(x.Key, out var charged)
                ? charged with
                {
                    Participant = pools.TryGetValue(x.Key, out var pool)
                        ? charged.Participant with { Pool = pool }
                        : charged.Participant,
                }
                : x.Value);
        TickStatusesPresentAtAttackStart(state.CombatProfiles, nextProfiles);
        TransferDefeatedLoot(engagements, units, nextProfiles, events, state);
        var nextUnits = state.Units.Select(unit => ApplyUnitOutcome(
                units.GetValueOrDefault(unit.Id, unit), pools, waiting, state, events))
            .ToList();

        return new RenewalCombatResolution(state with
        {
            Units = nextUnits,
            CombatProfiles = nextProfiles,
            Structures = structures,
            Sites = sites,
        }, events);
    }

    private Dictionary<UnitId, RenewalCombatProfile> PrepareTacticActives(
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles,
        IReadOnlySet<UnitId> participants,
        IDictionary<UnitId, RenewalUnitState> units,
        RenewalAdvanceState state, ICollection<RenewalAdvanceEvent> events,
        out Dictionary<UnitId, ActiveSkill> fired)
    {
        var result = profiles.ToDictionary(x => x.Key, x => x.Value);
        fired = [];
        foreach (var id in participants.OrderBy(x => units[x].CommandId).ThenBy(x => x.Value))
        {
            var caster = result[id];
            if (caster.CombatState is not { } combatState || IsDazed(combatState)
                || combatState.ScheduledActive is not { Type: ActiveType.Tactic } skill
                || !combatState.SharedActiveGauge.IsReady)
            {
                continue;
            }

            if (skill.Code == "cleanse")
            {
                foreach (var ally in units.Values.Where(x => x.Owner == units[id].Owner
                    && InHexRange(x.Position, units[id].Position, 2)).OrderBy(x => x.Id.Value))
                {
                    if (result[ally.Id].CombatState is { } allyState)
                    {
                        result[ally.Id] = result[ally.Id] with
                        {
                            CombatState = allyState.Purge(PurgeScope.Fire).Purge(PurgeScope.NonFire),
                        };
                    }
                }
                ConsumeTactic(result, id, skill, state, events, fired);
                continue;
            }

            var range = skill.Code == "lightning" ? 3 : skill.Code == "rout" ? 2 : 1;
            var target = units.Values.Where(x => x.Owner != units[id].Owner
                    && result.TryGetValue(x.Id, out var targetProfile)
                    && targetProfile.Participant.Pool.Active > 0
                    && InHexRange(x.Position, units[id].Position, range))
                .Where(x => skill.Code is not ("confound" or "rout")
                    || caster.Participant.Intellect > result[x.Id].Participant.Intellect)
                .OrderBy(x => x.Position.DistanceTo(units[id].Position))
                .ThenBy(x => x.Id.Value).FirstOrDefault();
            if (target is null) continue;

            switch (skill.Code)
            {
                case "fire_plot":
                    foreach (var enemy in AreaEnemies(target.Position, units[id].Owner, 1,
                                 units, result))
                    {
                        AddStatus(result, enemy.Id,
                            new StatusEffect(StatusKind.Burn, 200, 4, true,
                                PermanentLoss: true), state, events);
                    }
                    break;
                case "lightning":
                    foreach (var enemy in AreaEnemies(target.Position, units[id].Owner, 1,
                                 units, result))
                    {
                        var profile = result[enemy.Id];
                        var center = enemy.Id == target.Id;
                        var normal = center ? profile.Participant.Pool.Active * 5 / 100 : 0;
                        var permanent = profile.Participant.Pool.Active * (center ? 10 : 5) / 100;
                        var pool = normal > 0
                            ? profile.Participant.Pool.TakeDamage(normal, _woundedPercent)
                            : profile.Participant.Pool;
                        if (permanent > 0) pool = pool.TakeDamage(permanent, 0);
                        result[enemy.Id] = profile with
                        {
                            Participant = profile.Participant with { Pool = pool },
                        };
                        events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.AttackResolved,
                            state.Day, RenewalAdvancePhase.Attack, state.MovementTick, id,
                            Target: RenewalTargetId.ForUnit(enemy.Id),
                            Amount: normal + permanent, Detail: skill.Code));
                    }
                    break;
                case "confound":
                    AddStatus(result, target.Id,
                        new StatusEffect(StatusKind.Daze, 0, 1, false), state, events);
                    break;
                case "rout":
                    PushAway(units, target.Id, units[id].Position, state);
                    break;
                case "discord":
                    AddStatus(result, target.Id,
                        new StatusEffect(StatusKind.Nullify, 0, 2, false,
                            NullifyAptPassive: true), state, events);
                    break;
                default:
                    continue;
            }
            ConsumeTactic(result, id, skill, state, events, fired);
        }
        return result;
    }

    private static void ConsumeTactic(IDictionary<UnitId, RenewalCombatProfile> profiles,
        UnitId id, ActiveSkill skill, RenewalAdvanceState state,
        ICollection<RenewalAdvanceEvent> events, IDictionary<UnitId, ActiveSkill> fired)
    {
        var profile = profiles[id];
        var (_, consumed) = profile.CombatState!.FiringTactic();
        profiles[id] = profile with { CombatState = consumed };
        fired[id] = skill;
        events.Add(SkillEvent(state, id, skill));
    }

    private static void AddStatus(IDictionary<UnitId, RenewalCombatProfile> profiles,
        UnitId id, StatusEffect status, RenewalAdvanceState state,
        ICollection<RenewalAdvanceEvent> events)
    {
        var profile = profiles[id];
        if (profile.CombatState is null) return;
        profiles[id] = profile with { CombatState = profile.CombatState.AddStatus(status) };
        events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.StatusApplied,
            state.Day, RenewalAdvancePhase.AttackAftermath, state.MovementTick, id,
            Detail: status.Kind.ToString()));
    }

    private static IEnumerable<RenewalUnitState> AreaEnemies(ContinuousPosition center,
        FactionId owner, int range, IDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles) => units.Values
        .Where(x => x.Owner != owner && profiles.TryGetValue(x.Id, out var profile)
            && profile.Participant.Pool.Active > 0
            && InHexRange(x.Position, center, range));

    private static bool InHexRange(ContinuousPosition left, ContinuousPosition right, int range) =>
        RenewalHexSpace.NearestHex(left).Distance(RenewalHexSpace.NearestHex(right)) <= range;

    private void PushAway(IDictionary<UnitId, RenewalUnitState> units, UnitId targetId,
        ContinuousPosition casterPosition, RenewalAdvanceState state)
    {
        var target = units[targetId];
        var dx = target.Position.X - casterPosition.X;
        var dy = target.Position.Y - casterPosition.Y;
        var length = Math.Max(1L, target.Position.DistanceTo(casterPosition));
        for (var tiles = 4; tiles >= 1; tiles--)
        {
            var distance = tiles * ContinuousPosition.UnitsPerTile;
            var destination = new ContinuousPosition(
                target.Position.X + dx * distance / length,
                target.Position.Y + dy * distance / length);
            if (_movementMap is not null
                && (!_movementMap.CanStand(target.Domain, destination,
                        RenewalAdvanceSimulator.UnitCollisionRadius)
                    || _movementMap.FirstStaticCollision(target.Position, destination,
                        RenewalAdvanceSimulator.UnitCollisionRadius,
                        target.Domain) != RenewalStopReason.None))
            {
                continue;
            }
            var blocked = units.Values.Any(x => x.Id != targetId && x.Owner != target.Owner
                && x.IsActive && x.Position.DistanceTo(destination)
                    < RenewalAdvanceSimulator.UnitCollisionRadius * 2)
                || (state.Structures ?? []).Any(x => x.IsActive
                    && x.Position.DistanceTo(destination)
                        < RenewalAdvanceSimulator.UnitCollisionRadius)
                || (state.Sites ?? []).Any(x =>
                    x.Position.DistanceTo(destination)
                        < RenewalAdvanceSimulator.UnitCollisionRadius);
            if (blocked) continue;
            units[targetId] = target with
            {
                Position = destination,
                Destination = destination,
                StopReason = RenewalStopReason.None,
            };
            return;
        }
    }

    private Dictionary<UnitId, RenewalCombatProfile> ApplyExistingStatuses(
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles,
        RenewalAdvanceState state, ICollection<RenewalAdvanceEvent> events)
    {
        var result = profiles.ToDictionary(x => x.Key, x => x.Value);
        foreach (var (id, profile) in profiles.OrderBy(x => x.Key.Value))
        {
            if (profile.CombatState is not { } combatState)
            {
                continue;
            }
            var pool = profile.Participant.Pool;
            foreach (var status in combatState.Statuses)
            {
                var damage = status.TickDamage(pool.Active);
                if (damage > 0)
                {
                    pool = pool.TakeDamage(damage,
                        status.PermanentLoss ? 0 : _woundedPercent);
                    events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.StatusTicked,
                        state.Day, RenewalAdvancePhase.Attack, state.MovementTick, id,
                        Amount: damage, Detail: status.Kind.ToString()));
                }
            }
            result[id] = profile with
            {
                Participant = profile.Participant with { Pool = pool },
            };
        }
        return result;
    }

    private static void TickStatusesPresentAtAttackStart(
        IReadOnlyDictionary<UnitId, RenewalCombatProfile>? starting,
        IDictionary<UnitId, RenewalCombatProfile> current)
    {
        if (starting is null) return;
        foreach (var (id, profile) in current.ToList())
        {
            if (profile.CombatState is not { } currentState
                || !starting.TryGetValue(id, out var original)
                || original.CombatState is not { } originalState)
            {
                continue;
            }
            var unticked = originalState.Statuses.ToHashSet();
            var statuses = currentState.Statuses.Select(status => unticked.Remove(status)
                    ? status.Tick()
                    : status)
                .Where(status => !status.IsExpired).ToList();
            current[id] = profile with
            {
                CombatState = currentState with { Statuses = statuses },
            };
        }
    }

    private static HashSet<UnitId> CollectStructureAndSiteAttackers(RenewalAdvanceState state,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles)
    {
        var result = new HashSet<UnitId>();
        foreach (var structure in state.Structures ?? Array.Empty<RenewalStructureCombatState>())
        {
            result.UnionWith(AttackersFor(structure.Id, structure.Owner, structure.Position,
                units, profiles, x => x.BuildingAttackRange).Select(x => x.Id));
        }
        foreach (var site in state.Sites ?? Array.Empty<RenewalSiteCombatState>())
        {
            result.UnionWith(AttackersFor(site.Id, site.Owner, site.Position, units, profiles,
                    x => x.CastleAttackRange)
                .OrderBy(x => x.AttackRangeReachedTick ?? int.MaxValue)
                .ThenBy(x => x.CommandId).ThenBy(x => x.Id.Value)
                .Take(site.ParticipationLimit).Select(x => x.Id));
        }
        return result;
    }

    private static Dictionary<UnitId, RenewalCombatProfile> PrepareActives(
        IReadOnlyDictionary<UnitId, RenewalCombatProfile> profiles,
        IReadOnlySet<UnitId> participants, IReadOnlySet<UnitId> attackers,
        IReadOnlyList<UnitEngagement> engagements,
        IReadOnlyDictionary<UnitId, RenewalUnitState> units,
        RenewalAdvanceState state, ICollection<RenewalAdvanceEvent> events,
        out Dictionary<UnitId, ActiveSkill> fired,
        out Dictionary<UnitId, ActiveSkill> defenseSkills,
        out HashSet<UnitId> armorBreakTargets,
        IReadOnlyDictionary<UnitId, ActiveSkill>? alreadyFired = null)
    {
        var result = profiles.ToDictionary(x => x.Key, x => x.Value);
        fired = alreadyFired?.ToDictionary(x => x.Key, x => x.Value) ?? [];
        defenseSkills = [];
        armorBreakTargets = [];
        var ordered = participants.OrderBy(x => units[x].CommandId).ThenBy(x => x.Value).ToList();

        foreach (var id in ordered)
        {
            var profile = result[id];
            if (profile.CombatState is not { } combatState || IsDazed(combatState)) continue;
            var (skill, next) = combatState.FiringDefenseActive();
            if (skill is null) continue;
            if (skill.Code == "evasion")
            {
                next = next.AddStatus(new StatusEffect(StatusKind.Evasion, 0, 3, false));
            }
            result[id] = profile with { CombatState = next };
            fired[id] = skill;
            defenseSkills[id] = skill;
            events.Add(SkillEvent(state, id, skill));
        }

        foreach (var id in ordered)
        {
            if (fired.ContainsKey(id)) continue;
            var profile = result[id];
            if (profile.CombatState is not { } combatState || IsDazed(combatState)) continue;
            var (skill, next) = combatState.FiringActive();
            if (skill is null || skill.Type == ActiveType.Strike && !attackers.Contains(id)) continue;
            if (skill.Code == "rally")
            {
                next = next.AddStatus(new StatusEffect(StatusKind.Rally, 0, 3, false,
                    AtkBonusPercent: 10, DfBonusPercent: 10));
            }
            if (skill.Code == "armor_break")
            {
                var target = engagements.FirstOrDefault(x => x.Attacker == id)?.Targets.FirstOrDefault();
                if (target is { } targetId) armorBreakTargets.Add(targetId);
            }
            result[id] = profile with { CombatState = next };
            fired[id] = skill;
            events.Add(SkillEvent(state, id, skill));
        }
        return result;
    }

    private static BattleParticipant BuildParticipant(RenewalCombatProfile profile,
        RenewalUnitState unit, ActiveSkill? fired, ActiveSkill? defense)
    {
        var participant = profile.Participant;
        var stats = ApplyStatuses(participant.Stats, profile.CombatState);
        return participant with
        {
            Stats = stats,
            Mode = ToLegacyMode(unit.Mode),
            AttackRange = unit.AttackRange,
            StrikeActive = fired?.Type == ActiveType.Strike ? fired : null,
            HealActive = fired?.Type == ActiveType.Heal ? fired : null,
            DefenseActive = defense is { Code: not "evasion" } ? defense : null,
            RangedDamageTakenPercent = profile.CombatState?.Statuses.Any(
                x => x.Kind == StatusKind.Evasion) == true ? 70 : 100,
        };
    }

    private static CombatStats ApplyStatuses(CombatStats stats, UnitCombatState? state)
    {
        if (state is null) return stats;
        var nullified = state.Statuses.Any(x => x.NullifyAptPassive);
        var dfDown = state.Statuses.Sum(x => x.DfDownPercent);
        var atkBonus = state.Statuses.Sum(x => x.AtkBonusPercent);
        var dfBonus = state.Statuses.Sum(x => x.DfBonusPercent);
        return stats with
        {
            AptitudePercent = nullified ? 100 : stats.AptitudePercent,
            AtkBonusPercent = nullified ? 100 : stats.AtkBonusPercent + atkBonus,
            DfBonusPercent = nullified ? 100 : stats.DfBonusPercent + dfBonus,
            DfStat = Math.Max(0, stats.DfStat * Math.Max(0, 100 - dfDown) / 100),
        };
    }

    private static bool IsDazed(UnitCombatState state) =>
        state.Statuses.Any(x => x.IsDaze);

    private static RenewalAdvanceEvent SkillEvent(RenewalAdvanceState state, UnitId id,
        ActiveSkill skill) => new(RenewalAdvanceEventKind.ActiveSkillFired,
        state.Day, RenewalAdvancePhase.Attack, state.MovementTick, id,
        Detail: skill.Code);

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
            if (profiles[attacker.Id].CombatState is { } combatState && IsDazed(combatState))
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
        if (profiles[defender.Id].CombatState is { } combatState && IsDazed(combatState))
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
