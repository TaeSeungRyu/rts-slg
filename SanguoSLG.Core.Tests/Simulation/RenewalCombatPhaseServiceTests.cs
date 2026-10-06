namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalCombatPhaseServiceTests
{
    private static readonly BalanceConfig Balance = new(0);

    private static RenewalUnitState Unit(int id, int faction, long x, long y,
        RenewalOrderMode mode, int range = 1, long command = 0,
        RenewalTargetId? target = null, int? reachedTick = null) =>
        RenewalUnitState.Create(new UnitId(id), new ContinuousPosition(x, y),
            new ContinuousPosition(x, y), 0) with
        {
            Owner = new FactionId(faction),
            Mode = mode,
            AttackRange = range,
            CommandId = command,
            AssignedTarget = mode == RenewalOrderMode.Attack ? target : null,
            PursuitTarget = mode == RenewalOrderMode.Advance ? target : null,
            AttackRangeReachedTick = reachedTick,
            Arrived = mode == RenewalOrderMode.Standby,
        };

    private static RenewalCombatProfile Profile(RenewalUnitState unit, int troops = 10_000,
        int attack = 10, int defense = 10) => new(unit.Id,
        new BattleParticipant(new CombatStats(troops, attack, defense), UnitMode.Attack,
            new TroopPool(troops, 0)), BuildingAttack: attack);

    private static RenewalStepResult AttackPhase(IReadOnlyList<RenewalUnitState> units,
        IReadOnlyList<RenewalStructureCombatState>? structures = null,
        IReadOnlyList<RenewalSiteCombatState>? sites = null,
        IReadOnlyDictionary<UnitId, RenewalCombatProfile>? profiles = null)
    {
        profiles ??= units.ToDictionary(x => x.Id, x => Profile(x));
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Attack, 50, units,
            CombatProfiles: profiles, Structures: structures, Sites: sites);
        var simulator = new RenewalAdvanceSimulator(combat: new RenewalCombatPhaseService(Balance));
        return simulator.StepPhase(state);
    }

    [Fact]
    public void 공격턴_시작위치가_사거리밖이면_이동중_통과했어도_피해가없다()
    {
        var defender = Unit(2, 2, 1_001, 0, RenewalOrderMode.March);
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(defender.Id));

        var result = AttackPhase([attacker, defender]);

        Assert.DoesNotContain(result.Events,
            x => x.Kind == RenewalAdvanceEventKind.AttackResolved);
        Assert.Equal(10_000, result.State.CombatProfiles![defender.Id].Participant.Pool.Active);
    }

    [Fact]
    public void 공격턴_시작위치가_사거리안이면_행군부대는_피해만받고_반격하지않는다()
    {
        var defender = Unit(2, 2, 1_000, 0, RenewalOrderMode.March);
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(defender.Id));

        var result = AttackPhase([attacker, defender]);
        var profiles = result.State.CombatProfiles!;

        Assert.True(profiles[defender.Id].Participant.Pool.Active < 10_000);
        Assert.Equal(10_000, profiles[attacker.Id].Participant.Pool.Active);
    }

    [Fact]
    public void 대인_건축물_성_사거리는_각대상별_병종값을_사용한다()
    {
        var targetId = new RenewalTargetId(RenewalTargetKind.Building, 50);
        var archer = Unit(1, 1, 0, 0, RenewalOrderMode.Attack, range: 2,
            target: targetId) with { BuildingAttackRange = 1, CastleAttackRange = 1 };
        var building = new RenewalStructureCombatState(targetId, new FactionId(2),
            new ContinuousPosition(1_500, 0), 10_000, 10);

        var archerResult = AttackPhase([archer], structures: [building]);
        Assert.Equal(10_000, archerResult.State.Structures!.Single().HitPoints);

        var catapult = archer with { BuildingAttackRange = 2, CastleAttackRange = 2 };
        var catapultResult = AttackPhase([catapult], structures: [building]);
        Assert.True(catapultResult.State.Structures!.Single().HitPoints < 10_000);
    }

    [Fact]
    public void 서로_공격하는_한쌍은_능동공격과_반격을_중복계산하지않는다()
    {
        var a = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(new UnitId(2)));
        var b = Unit(2, 2, 1_000, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(a.Id));
        var expected = new CombatPhaseResolver(new BattleResolver(60), 70).Resolve(
            [new UnitEngagement(a.Id, [b.Id]), new UnitEngagement(b.Id, [a.Id])],
            new Dictionary<UnitId, BattleParticipant>
            {
                [a.Id] = Profile(a).Participant with { Mode = UnitMode.Attack },
                [b.Id] = Profile(b).Participant with { Mode = UnitMode.Attack },
            });

        var actual = AttackPhase([a, b]).State.CombatProfiles!;

        Assert.Equal(expected.Pools[a.Id], actual[a.Id].Participant.Pool);
        Assert.Equal(expected.Pools[b.Id], actual[b.Id].Participant.Pool);
    }

    [Fact]
    public void 세부대의_협공은_방어자의_반격을_주대상과_부차대상으로_한번씩만_묶는다()
    {
        var a = Unit(1, 1, 0, 0, RenewalOrderMode.Standby);
        var enemies = new[]
        {
            Unit(2, 2, 1_000, 0, RenewalOrderMode.Attack, command: 1,
                target: RenewalTargetId.ForUnit(a.Id)),
            Unit(3, 2, 0, 1_000, RenewalOrderMode.Attack, command: 2,
                target: RenewalTargetId.ForUnit(a.Id)),
            Unit(4, 2, -1_000, 0, RenewalOrderMode.Attack, command: 3,
                target: RenewalTargetId.ForUnit(a.Id)),
        };

        var result = AttackPhase([a, .. enemies]);
        var pools = result.State.CombatProfiles!;
        var losses = enemies.Select(x => 10_000 - pools[x.Id].Participant.Pool.Active).ToArray();

        Assert.True(losses[0] > losses[1]);
        Assert.Equal(losses[1], losses[2]);
        Assert.Equal(4, result.Events.Count(x => x.Kind == RenewalAdvanceEventKind.AttackResolved));
        Assert.All(result.State.CombatProfiles!.Values,
            x => Assert.Equal(1, x.CombatGrowthAwards));
        Assert.Equal(4, result.Events.Count(
            x => x.Kind == RenewalAdvanceEventKind.CombatParticipationRecorded));
    }

    [Fact]
    public void 공격명령은_비지정공격자에게_반격하지않는다()
    {
        var b = Unit(2, 2, 1_000, 0, RenewalOrderMode.Standby);
        var a = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(b.Id));
        var c = Unit(3, 2, 0, 1_000, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(a.Id));

        var pools = AttackPhase([a, b, c]).State.CombatProfiles!;

        Assert.Equal(10_000, pools[c.Id].Participant.Pool.Active);
        Assert.True(pools[a.Id].Participant.Pool.Active < 10_000);
        Assert.True(pools[b.Id].Participant.Pool.Active < 10_000);
    }

    [Theory]
    [InlineData(CastleSize.Small, PortSize.None, 4, 3)]
    [InlineData(CastleSize.Medium, PortSize.None, 6, 5)]
    [InlineData(CastleSize.Large, PortSize.None, 8, 7)]
    [InlineData(CastleSize.Small, PortSize.Small, 4, 3)]
    [InlineData(CastleSize.Small, PortSize.Medium, 6, 5)]
    public void 공성참여권은_거점전체에서_규모별_상한을_지킨다(
        CastleSize size, PortSize port, int count, int expected)
    {
        var siteId = new RenewalTargetId(RenewalTargetKind.Site, 1);
        var units = Enumerable.Range(1, count).Select(id =>
            Unit(id, 1, id * 10, 0, RenewalOrderMode.Attack, command: id,
                target: siteId, reachedTick: id)).ToList();
        var site = new RenewalSiteCombatState(siteId, new FactionId(2),
            new ContinuousPosition(0, 0), new CastleState(100_000, 10_000), size, port);

        var result = AttackPhase(units, sites: [site]);

        Assert.Equal(count - expected,
            result.Events.Count(x => x.Kind == RenewalAdvanceEventKind.SiegeWaiting));
        Assert.Equal(count - expected,
            result.State.Units.Count(x => x.StopReason == RenewalStopReason.SiegeCapacity));
    }

    [Fact]
    public void 공성참여권은_도착틱_명령순번_ID순으로_선정한다()
    {
        var siteId = new RenewalTargetId(RenewalTargetKind.Site, 2);
        var units = new[]
        {
            Unit(1, 1, 0, 0, RenewalOrderMode.Attack, command: 9, target: siteId, reachedTick: 2),
            Unit(2, 1, 0, 0, RenewalOrderMode.Attack, command: 5, target: siteId, reachedTick: 1),
            Unit(3, 1, 0, 0, RenewalOrderMode.Attack, command: 3, target: siteId, reachedTick: 1),
            Unit(4, 1, 0, 0, RenewalOrderMode.Attack, command: 3, target: siteId, reachedTick: 1),
        };
        var site = new RenewalSiteCombatState(siteId, new FactionId(2),
            new ContinuousPosition(0, 0), new CastleState(100_000, 10_000));

        var result = AttackPhase(units, sites: [site]);

        Assert.Equal(new UnitId(1), Assert.Single(result.Events,
            x => x.Kind == RenewalAdvanceEventKind.SiegeWaiting).Unit);
        Assert.Equal(0, result.State.CombatProfiles![new UnitId(1)].CombatGrowthAwards);
        Assert.All(result.State.CombatProfiles.Where(x => x.Key.Value != 1),
            x => Assert.Equal(1, x.Value.CombatGrowthAwards));
    }

    [Fact]
    public void 실제교전은_공격턴마다_공용게이지와_성장판정을_한번만_올린다()
    {
        var defender = Unit(1, 1, 0, 0, RenewalOrderMode.Standby);
        var attackers = Enumerable.Range(2, 3).Select(id =>
            Unit(id, 2, 500, 0, RenewalOrderMode.Attack,
                target: RenewalTargetId.ForUnit(defender.Id))).ToList();
        var profiles = new[] { defender }.Concat(attackers).ToDictionary(x => x.Id, x =>
            Profile(x) with { CombatState = UnitCombatState.Create(60) });

        var result = AttackPhase([defender, .. attackers], profiles: profiles);
        var defended = result.State.CombatProfiles![defender.Id];

        Assert.Equal(1, defended.CombatGrowthAwards);
        Assert.Equal(1, defended.CombatState!.SharedActiveGauge.ElapsedDays);
        Assert.Single(result.Events, x =>
            x.Kind == RenewalAdvanceEventKind.CombatParticipationRecorded
            && x.Unit == defender.Id);
    }

    [Fact]
    public void 충전완료된_타격과_방어액티브는_방어우선으로_각각한번_발동한다()
    {
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(new UnitId(2)));
        var defender = Unit(2, 2, 500, 0, RenewalOrderMode.Standby);
        var strike = new ActiveSkill("peerless", "무쌍", ActiveType.Strike, "high",
            DamageMultPercent: 160);
        var defense = new ActiveSkill("iron_wall", "철벽", ActiveType.Defense, "high",
            DamageReductionPercent: 30);
        var profiles = new Dictionary<UnitId, RenewalCombatProfile>
        {
            [attacker.Id] = Profile(attacker) with
            {
                CombatState = UnitCombatState.Create(60, strike) with
                {
                    VanguardGauge = new ActiveGauge(5),
                    AdjutantGauge = new ActiveGauge(5),
                },
            },
            [defender.Id] = Profile(defender) with
            {
                CombatState = UnitCombatState.Create(60, defense) with
                {
                    VanguardGauge = new ActiveGauge(5),
                    AdjutantGauge = new ActiveGauge(5),
                },
            },
        };

        var result = AttackPhase([attacker, defender], profiles: profiles);

        Assert.Equal(2, result.Events.Count(x =>
            x.Kind == RenewalAdvanceEventKind.ActiveSkillFired));
        Assert.Contains(result.Events, x => x.Detail == "iron_wall");
        Assert.Contains(result.Events, x => x.Detail == "peerless");
        Assert.Equal(0, result.State.CombatProfiles![attacker.Id]
            .CombatState!.SharedActiveGauge.ElapsedDays);
        Assert.Equal(0, result.State.CombatProfiles[defender.Id]
            .CombatState!.SharedActiveGauge.ElapsedDays);
    }

    [Fact]
    public void 기존지속상태는_공격턴시작에_적용후감소하고_신규상태는_그날감소하지않는다()
    {
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(new UnitId(2)));
        var defender = Unit(2, 2, 500, 0, RenewalOrderMode.Standby);
        var armorBreak = new ActiveSkill("armor_break", "파갑", ActiveType.Strike, "high",
            DamageMultPercent: 110, DefenderDfReductionPercent: 20);
        var attackerState = UnitCombatState.Create(60, armorBreak) with
        {
            VanguardGauge = new ActiveGauge(5),
            AdjutantGauge = new ActiveGauge(5),
        };
        var defenderState = UnitCombatState.Create(60).AddStatus(
            new StatusEffect(StatusKind.Burn, 200, 1, true, PermanentLoss: true));
        var profiles = new Dictionary<UnitId, RenewalCombatProfile>
        {
            [attacker.Id] = Profile(attacker) with { CombatState = attackerState },
            [defender.Id] = Profile(defender) with { CombatState = defenderState },
        };

        var result = AttackPhase([attacker, defender], profiles: profiles);
        var statuses = result.State.CombatProfiles![defender.Id].CombatState!.Statuses;

        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.StatusTicked
            && x.Detail == StatusKind.Burn.ToString());
        Assert.DoesNotContain(statuses, x => x.Kind == StatusKind.Burn);
        Assert.Equal(3, Assert.Single(statuses,
            x => x.Kind == StatusKind.ArmorBreak).Remaining);
    }

    [Fact]
    public void 혼란은_해당공격턴의_공격과반격과액티브를_막고_이후만료된다()
    {
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(new UnitId(2)));
        var defender = Unit(2, 2, 500, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(attacker.Id));
        var skill = new ActiveSkill("peerless", "무쌍", ActiveType.Strike, "high", 160);
        var dazed = UnitCombatState.Create(60, skill) with
        {
            VanguardGauge = new ActiveGauge(5),
            AdjutantGauge = new ActiveGauge(5),
        };
        dazed = dazed.AddStatus(new StatusEffect(StatusKind.Daze, 0, 1, false));
        var profiles = new Dictionary<UnitId, RenewalCombatProfile>
        {
            [attacker.Id] = Profile(attacker),
            [defender.Id] = Profile(defender) with { CombatState = dazed },
        };

        var result = AttackPhase([attacker, defender], profiles: profiles);

        Assert.Equal(10_000, result.State.CombatProfiles![attacker.Id].Participant.Pool.Active);
        Assert.DoesNotContain(result.Events, x => x.Kind == RenewalAdvanceEventKind.ActiveSkillFired
            && x.Unit == defender.Id);
        Assert.DoesNotContain(result.State.CombatProfiles[defender.Id].CombatState!.Statuses,
            x => x.Kind == StatusKind.Daze);
    }

    [Fact]
    public void 공성참여자가_이탈하면_다음_공격턴에_대기부대가_승계한다()
    {
        var siteId = new RenewalTargetId(RenewalTargetKind.Site, 20);
        var units = Enumerable.Range(1, 4).Select(id =>
            Unit(id, 1, 0, 0, RenewalOrderMode.Attack, command: id,
                target: siteId, reachedTick: id)).ToList();
        var site = new RenewalSiteCombatState(siteId, new FactionId(2),
            new ContinuousPosition(0, 0), new CastleState(1_000_000, 100_000));

        var first = AttackPhase(units, sites: [site]);
        Assert.Equal(new UnitId(4), Assert.Single(first.Events,
            x => x.Kind == RenewalAdvanceEventKind.SiegeWaiting).Unit);

        var nextUnits = first.State.Units.Select(x => x.Id.Value == 1
            ? x with { IsActive = false }
            : x).ToList();
        var second = AttackPhase(nextUnits, sites: first.State.Sites,
            profiles: first.State.CombatProfiles);

        Assert.DoesNotContain(second.Events,
            x => x.Kind == RenewalAdvanceEventKind.SiegeWaiting && x.Unit == new UnitId(4));
        Assert.Equal(RenewalStopReason.TargetInRange,
            second.State.Units.Single(x => x.Id.Value == 4).StopReason);
    }

    [Fact]
    public void 공격턴에_수비가_전멸하면_공격후처리전에_거점소유권이_바뀐다()
    {
        var siteId = new RenewalTargetId(RenewalTargetKind.Site, 3);
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack, target: siteId);
        var site = new RenewalSiteCombatState(siteId, new FactionId(2),
            new ContinuousPosition(500, 0), new CastleState(0, 1));
        var profile = Profile(attacker, attack: 100);

        var result = AttackPhase([attacker], sites: [site],
            profiles: new Dictionary<UnitId, RenewalCombatProfile> { [attacker.Id] = profile });

        Assert.Equal(RenewalAdvancePhase.AttackAftermath, result.State.Phase);
        Assert.Equal(attacker.Owner, result.State.Sites!.Single().Owner);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.SiteCaptured);
    }

    [Fact]
    public void 부대와_건축물은_체력이_0이되는_공격턴에_즉시_제거된다()
    {
        var victim = Unit(2, 2, 500, 0, RenewalOrderMode.Standby);
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(victim.Id));
        var strong = Profile(attacker, attack: 100);
        var weak = Profile(victim, troops: 1, defense: 1);
        var unitResult = AttackPhase([attacker, victim], profiles:
            new Dictionary<UnitId, RenewalCombatProfile>
            {
                [attacker.Id] = strong,
                [victim.Id] = weak,
            });
        Assert.False(unitResult.State.Units.Single(x => x.Id == victim.Id).IsActive);
        Assert.Contains(unitResult.Events, x => x.Kind == RenewalAdvanceEventKind.UnitDefeated);

        var buildingId = new RenewalTargetId(RenewalTargetKind.Building, 9);
        attacker = attacker with { AssignedTarget = buildingId };
        var building = new RenewalStructureCombatState(buildingId, new FactionId(2),
            new ContinuousPosition(500, 0), 1, 1);
        var buildingResult = AttackPhase([attacker], [building], profiles:
            new Dictionary<UnitId, RenewalCombatProfile> { [attacker.Id] = strong });
        Assert.Equal(0, buildingResult.State.Structures!.Single().HitPoints);
        Assert.Contains(buildingResult.Events,
            x => x.Kind == RenewalAdvanceEventKind.StructureDestroyed);
    }

    [Fact]
    public void 괴멸한_부대의_금과_군량은_같은_공격턴의_공격자에게_이전된다()
    {
        var victim = Unit(2, 2, 500, 0, RenewalOrderMode.Standby);
        var attacker = Unit(1, 1, 0, 0, RenewalOrderMode.Attack,
            target: RenewalTargetId.ForUnit(victim.Id));
        var strong = Profile(attacker, attack: 100) with { CarryingGold = 5, Provisions = 7 };
        var weak = Profile(victim, troops: 1, defense: 1) with
            { CarryingGold = 40, Provisions = 60 };

        var result = AttackPhase([attacker, victim], profiles:
            new Dictionary<UnitId, RenewalCombatProfile>
            {
                [attacker.Id] = strong,
                [victim.Id] = weak,
            });

        Assert.Equal(45, result.State.CombatProfiles![attacker.Id].CarryingGold);
        Assert.Equal(67, result.State.CombatProfiles[attacker.Id].Provisions);
        Assert.Equal(0, result.State.CombatProfiles[victim.Id].CarryingGold);
        Assert.Equal(0, result.State.CombatProfiles[victim.Id].Provisions);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.LootTransferred
            && x.Unit == attacker.Id);
    }
}
