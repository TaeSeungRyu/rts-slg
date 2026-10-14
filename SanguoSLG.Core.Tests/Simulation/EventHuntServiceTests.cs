namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using Xunit;

public class EventHuntServiceTests
{
    private static readonly EventUnitDefinition Tiger = new EventUnitLoader()
        .LoadFromDirectory(TestData.DataDirectory()).Single(x => x.Code == "great_tiger");
    private static readonly City City = new(new(1), "발견성", new(0, 0), new(1), 1000);
    private static GameState World => new(1, 190, [], [City], [],
        ExplorationDiscoveries: [new(1, new(1), City.Id, new(1), ExplorationResultKind.DivineBeast,
            "divine_beast_trace")]);
    private static HexMap Map => new(-8, 8, -8, 8,
        new Dictionary<HexCoord, TerrainType> { [new(2, 0)] = TerrainType.Forest, [new(3, 0)] = TerrainType.Mountain });
    private static EventHuntService Service => new(Map, [Tiger]);

    [Theory]
    [InlineData("dragon_trace", "dragon")]
    [InlineData("phoenix_trace", "phoenix")]
    public void 용과_봉황_단서는_발견_도시_주변에_각각_출현하고_저장된다(string clue, string code)
    {
        var definitions = new EventUnitLoader().LoadFromDirectory(TestData.DataDirectory());
        var service = new EventHuntService(Map, definitions);
        var discovery = World.Discoveries[0] with { Code = clue };
        var scheduled = service.ScheduleDiscoveries(World with { ExplorationDiscoveries = [discovery] });
        var spawned = service.SpawnDue(scheduled, 8, out var events);
        var beast = Assert.Single(spawned.Armies);

        Assert.Equal(code, beast.TroopCode);
        Assert.Equal(City.Id, Assert.Single(spawned.Encounters).DiscoveryCity);
        Assert.InRange(beast.Field.Position.Distance(City.Position), 2, 6);
        Assert.Single(events);
        Assert.Equal(code, Assert.Single(SaveService.Deserialize(SaveService.Serialize(spawned)).Armies).TroopCode);
    }

    private static GameState Spawn()
        => Service.SpawnDue(Service.ScheduleDiscoveries(World), 8, out _);

    [Fact]
    public void 단서는_한번만_예약하고_7일후_한마리만_출현한다()
    {
        var scheduled = Service.ScheduleDiscoveries(World);
        Assert.Single(Service.ScheduleDiscoveries(scheduled).Encounters);
        Assert.Empty(Service.SpawnDue(scheduled, 7, out var early).Armies);
        Assert.Empty(early);
        var spawned = Service.SpawnDue(scheduled, 8, out var events);
        Assert.Equal(new HexCoord(2, 0), Assert.Single(spawned.Armies).Field.Position);
        Assert.Equal(WorldEventKind.EventUnitAppeared, Assert.Single(events).Kind);
        Assert.Single(Service.SpawnDue(spawned, 99, out var repeated).Armies);
        Assert.Empty(repeated);
        var restored = SaveService.Deserialize(SaveService.Serialize(spawned));
        Assert.Single(Service.SpawnDue(restored, 99, out _).Armies);
    }

    [Fact]
    public void 시설과_겹치는_장소는_건너뛰고_장소가_없으면_예약을_유지한다()
    {
        var blocked = Service.ScheduleDiscoveries(World) with
        { FacilityPlacements = [new(City.Id, new(2, 0), "farm", 1000)] };
        var alternative = Assert.Single(Service.SpawnDue(blocked, 8, out _).Armies).Field.Position;
        Assert.NotEqual(new HexCoord(2, 0), alternative);
        Assert.Equal(Tiger.SpawnMinDistance, alternative.Distance(City.Position));
        var sea = new HexMap(-8, 8, -8, 8);
        var noTerrain = new EventHuntService(new HexMap(-8, 8, -8, 8,
            sea.Tiles().ToDictionary(tile => tile, _ => TerrainType.WaterShallow)), [Tiger]);
        var waiting = noTerrain.SpawnDue(blocked, 8, out _);
        Assert.Empty(waiting.Armies);
        Assert.Null(Assert.Single(waiting.Encounters).Unit);
    }

    [Fact]
    public void 항구주변에_숲과산이_없어도_기존예약이_해안평지에_출현한다()
    {
        var port = City with { Position = new(-6, -2), Port = PortSize.Medium, Castle = CastleSize.Medium };
        var sea = new HexMap(-12, 14, -5, 11);
        var terrain = sea.Tiles().Where(tile => tile.R < 0)
            .ToDictionary(tile => tile, _ => TerrainType.WaterShallow);
        var map = new HexMap(-12, 14, -5, 11, terrain);
        var service = new EventHuntService(map, [Tiger]);
        var old = SaveService.Deserialize(SaveService.Serialize(service.ScheduleDiscoveries(World with { Cities = [port] })));
        var spawned = service.SpawnDue(old, 36, out var events);
        var tiger = Assert.Single(spawned.Armies);
        Assert.Equal(TerrainType.Plains, map.TerrainAt(tiger.Field.Position));
        Assert.InRange(tiger.Field.Position.Distance(port.Position), Tiger.SpawnMinDistance, Tiger.SpawnMaxDistance);
        Assert.DoesNotContain(tiger.Field.Position, CastleFootprint.TilesFor(port));
        Assert.Single(events);
    }

    [Fact]
    public void 먼숲보다_탐색한성_가까운_평지에_출현한다()
    {
        var map = new HexMap(-8, 8, -8, 8,
            new Dictionary<HexCoord, TerrainType> { [new(5, 0)] = TerrainType.Forest });
        var service = new EventHuntService(map, [Tiger]);
        var spawned = service.SpawnDue(service.ScheduleDiscoveries(World), 8, out _);
        Assert.Equal(Tiger.SpawnMinDistance,
            Assert.Single(spawned.Armies).Field.Position.Distance(City.Position));
    }

    [Fact]
    public void 좌중성_탐색_대호는_장안이_아니라_좌중성_가까이에_출현한다()
    {
        var left = City with { Id = new(5), Name = "좌중성", Position = new(-5, 1) };
        var jangAn = City with { Name = "장안", Position = new(1, 2) };
        var discovery = new ExplorationDiscovery(1, left.Owner, left.Id, new(1),
            ExplorationResultKind.DivineBeast, "divine_beast_trace");
        var world = World with { Cities = [jangAn, left], ExplorationDiscoveries = [discovery] };

        var tiger = Assert.Single(Service.SpawnDue(Service.ScheduleDiscoveries(world), 8, out _).Armies);
        Assert.Equal(Tiger.SpawnMinDistance, tiger.Field.Position.Distance(left.Position));
        Assert.True(tiger.Field.Position.Distance(jangAn.Position) > tiger.Field.Position.Distance(left.Position));
        Assert.Equal(left.Id, Assert.Single(Service.ScheduleDiscoveries(world).Encounters).DiscoveryCity);
    }

    private sealed class TraceRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    [Fact]
    public void 실제항구탐색명령은_발견후_7일이_지나면_대호를_출현시킨다()
    {
        var port = City with { Port = PortSize.Small, Gold = 1000 };
        var general = new General(new(1), "탐색장", new Dictionary<TroopClass, AptitudeGrade>(), 70, 70, 70);
        var initial = new GameState(1, 190, [], [port], [general], Postings: [new(general.Id, port.Owner, port.Id)]);
        var command = new CommandService(new CommandBalance()).Issue(initial,
            new(port.Id, CommandKind.Explore, general.Id));
        Assert.True(command.Ok, command.Error);
        var map = new HexMap(-8, 8, -8, 8);
        var rules = new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(new RenewalCampaignAdvanceRunner(map, rules),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), random: new TraceRandom()),
            eventHunts: new(map, [Tiger]));
        var discovered = engine.AdvanceWeek(command.State, out _);
        Assert.Equal("divine_beast_trace", Assert.Single(discovered.Discoveries).Code);
        Assert.Single(discovered.Encounters);
        var appeared = engine.AdvanceWeek(discovered, out _);
        Assert.Single(appeared.Armies, unit => unit.Field.Owner == EventHuntService.EventFaction);
        Assert.Contains(engine.LastWorldEvents, e => e.Kind == WorldEventKind.EventUnitAppeared);
    }

    [Fact]
    public void 도시별_탐색과_같은날_중복탐색도_서로_다른_위치에_각각_출현한다()
    {
        var otherCity = City with { Id = new(2), Name = "다른 성", Position = new(-5, 0) };
        var discoveries = World.Discoveries.Concat([
            World.Discoveries[0],
            new ExplorationDiscovery(1, new(1), otherCity.Id, new(1), ExplorationResultKind.DivineBeast, "divine_beast_trace")]).ToList();
        var world = World with { Cities = [City, otherCity], ExplorationDiscoveries = discoveries };
        var scheduled = Service.ScheduleDiscoveries(world);
        Assert.Equal(3, scheduled.Encounters.Count);
        Assert.Equal(3, Service.ScheduleDiscoveries(scheduled).Encounters.Count);
        var spawned = Service.SpawnDue(scheduled, 8, out var events);
        Assert.Equal(3, spawned.Armies.Count);
        Assert.Equal(3, spawned.Armies.Select(unit => unit.Field.Position).Distinct().Count());
        Assert.Equal(3, events.Count);
        Assert.All(spawned.Encounters, encounter => Assert.NotNull(encounter.Unit));
        Assert.All(spawned.Encounters, encounter => Assert.InRange(
            spawned.Armies.Single(unit => unit.Id == encounter.Unit).Field.Position.Distance(
                world.Cities.Single(city => city.Id == encounter.DiscoveryCity).Position),
            Tiger.SpawnMinDistance, Tiger.SpawnMaxDistance));
    }

    [Fact]
    public void 대호는_약한육상부대를_추격하고_해상과_주둔부대는_무시한다()
    {
        var spawned = Spawn();
        var tiger = Assert.Single(spawned.Armies);
        CombatUnit Target(int id, int troops) => tiger with
        { Field = tiger.Field with { Id = new(id), Owner = new(1), Position = new(3, 0) },
            Pool = new(troops, 0), RenewalPosition = RenewalHexSpace.Center(new(3, 0)), TroopCode = "swordsman" };
        var strong = Target(2, 10000);
        var weak = Target(3, 5000);
        var naval = Target(4, 1) with { Field = Target(4, 1).Field with { Domain = MovementDomain.DeepWater } };
        var sheltered = Target(5, 1);
        var state = spawned with { FieldArmies = [tiger, strong, weak, naval, sheltered],
            FieldBuildings = [new(new(1), "fort", new(1), new(3, 0), 4000, 0, 0, GarrisonUnit: sheltered.Id)] };
        var planned = Service.PlanDaily(state).Armies.Single(u => u.Id == tiger.Id);
        Assert.Equal(weak.Id, planned.Field.AssignedUnitTarget);
        var idle = Service.PlanDaily(spawned).Armies.Single();
        Assert.Equal(UnitMode.Standby, idle.Field.Mode);
        Assert.Null(idle.Field.Target);
    }

    private static (GameState State, CombatUnit Tiger, CombatUnit Attacker) CombatFixture()
    {
        var state = Spawn();
        var tiger = state.Armies.Single();
        var attacker = tiger with { Field = tiger.Field with { Id = new(2), Owner = new(1), CommandOrder = 2 },
            TroopCode = "swordsman", VanguardId = new(1), AdjutantId = new(2), Class = TroopClass.Infantry };
        var generals = new[] { 1, 2 }.Select(id => new General(new(id), $"사냥장수{id}",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.D }, 70, 70, 70)).ToList();
        return (state with { FieldArmies = [tiger, attacker], Generals = generals }, tiger, attacker);
    }

    private static AdvanceTurn HuntTurn(CombatUnit tiger, CombatUnit attacker, bool killed, bool participated)
        => new(killed ? [attacker] : [tiger, attacker],
            new AdvanceResult([], [tiger.Field, attacker.Field], StopReason.MaxDays, 1),
            participated ? new(new Dictionary<UnitId, int> { [tiger.Id] = tiger.Pool.Active },
                new Dictionary<UnitId, int> { [attacker.Id] = tiger.Pool.Active },
                new Dictionary<UnitId, TroopPool>()) : null,
            new Dictionary<UnitId, ActiveSkill>(), new Dictionary<UnitId, Stratagem>(),
            new Dictionary<UnitId, int>(), new Dictionary<UnitId, int>(),
            FieldCombatExchangeResults: participated
                ? [new(attacker.Id, tiger.Id, attacker.RenewalPosition!.Value, tiger.RenewalPosition!.Value, true)] : []);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 실제처치는_동시괴멸에서도_경험치와_호부를_한번만_지급한다(bool mutualDestruction)
    {
        var (state, tiger, attacker) = CombatFixture();
        var turn = HuntTurn(tiger, attacker, true, true);
        if (mutualDestruction) turn = turn with { Units = [] };
        var rewarded = Service.ResolveCombat(state, state.Armies, turn, 10, out var events);
        Assert.Equal(new FactionId(1), Assert.Single(rewarded.Treasures).Faction);
        Assert.All(rewarded.Generals, general =>
        {
            Assert.Equal(4, general.Level);
            Assert.Equal(150, general.Experience);
            Assert.Equal(50, general.AptitudeExperienceFor(TroopClass.Infantry));
        });
        Assert.Single(events);
        var restored = SaveService.Deserialize(SaveService.Serialize(rewarded));
        Assert.Single(Service.ResolveCombat(restored, state.Armies, turn, 11, out var duplicate).Treasures);
        Assert.Empty(duplicate);
    }

    [Fact]
    public void 여러부대_기여는_실제대호피해로_나누고_저장후에도_유지된다()
    {
        var (state, tiger, first) = CombatFixture();
        tiger = tiger with { Pool = new(10000, 0) };
        var second = first with { Field = first.Field with { Id = new(3), CommandOrder = 3 },
            VanguardId = new(3), AdjutantId = null };
        state = state with { FieldArmies = [tiger, first, second], Generals = state.Generals.Append(
            new General(new(3), "추가장수", new Dictionary<TroopClass, AptitudeGrade>
                { [TroopClass.Infantry] = AptitudeGrade.D }, 70, 70, 70)).ToList() };
        var hit = HuntTurn(tiger, first, false, true) with
        {
            Units = [tiger, first, second],
            Combat = new(new Dictionary<UnitId, int> { [tiger.Id] = 3000 },
                new Dictionary<UnitId, int> { [first.Id] = 3000 }, new Dictionary<UnitId, TroopPool>(),
                [new UnitDamage(first.Id, tiger.Id, 3000)]),
        };
        var persisted = SaveService.Deserialize(SaveService.Serialize(
            Service.ResolveCombat(state, state.Armies, hit, 9, out _)));
        var finish = hit with
        {
            Units = [first, second],
            Combat = new(new Dictionary<UnitId, int> { [tiger.Id] = 7000 },
                new Dictionary<UnitId, int> { [second.Id] = 7000 }, new Dictionary<UnitId, TroopPool>(),
                [new UnitDamage(second.Id, tiger.Id, 7000)]),
            FieldCombatExchangeResults = [new(second.Id, tiger.Id,
                second.RenewalPosition!.Value, tiger.RenewalPosition!.Value, true)],
        };
        var rewarded = Service.ResolveCombat(persisted, persisted.Armies, finish, 10, out _);
        Assert.Equal(3000, rewarded.Encounters.Single().DamageContributions!.Single(x => x.Unit == first.Id).Damage);
        Assert.Equal(7000, rewarded.Encounters.Single().DamageContributions!.Single(x => x.Unit == second.Id).Damage);
        Assert.Equal(22, rewarded.Generals.Single(x => x.Id == first.VanguardId).AptitudeExperienceFor(TroopClass.Infantry));
        Assert.Equal(38, rewarded.Generals.Single(x => x.Id == second.VanguardId).AptitudeExperienceFor(TroopClass.Infantry));
    }

    [Fact]
    public void 대호_보상선택은_저장후에도_유지하고_한번만_확정한다()
    {
        var service = new EventHuntService(Map, [Tiger],
            new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()));
        var (state, tiger, attacker) = CombatFixture();
        var defeated = service.ResolveCombat(state, state.Armies,
            HuntTurn(tiger, attacker, true, true), 10, out _);
        var restored = SaveService.Deserialize(SaveService.Serialize(defeated));
        Assert.True(restored.Encounters.Single().RewardChoicePending);
        Assert.False(service.ChooseReward(restored, restored.Encounters.Single().Id, new(2), "tiger_cub").Ok);
        var chosen = service.ChooseReward(restored, restored.Encounters.Single().Id, new(1), "tiger_cub");
        Assert.True(chosen.Ok, chosen.Error);
        Assert.Equal("tiger_cub", Assert.Single(chosen.State.Treasures).Code);
        Assert.False(chosen.State.Encounters.Single().RewardChoicePending);
        Assert.False(service.ChooseReward(chosen.State, restored.Encounters.Single().Id, new(1), "tiger_fang").Ok);
        Assert.Single(SaveService.Deserialize(SaveService.Serialize(chosen.State)).Treasures);
    }

    [Fact]
    public void 교전기여가_없는_주변부대는_처치보상을_받지않는다()
    {
        var (state, tiger, attacker) = CombatFixture();
        var result = Service.ResolveCombat(state, state.Armies, HuntTurn(tiger, attacker, true, false), 10, out var events);
        Assert.Empty(result.Treasures);
        Assert.Empty(events);
        Assert.Equal(10, Assert.Single(result.Encounters).DefeatedDay);
    }

    [Fact]
    public void 낙뢰로_평타전에_괴멸하여도_실제시전자에게_처치보상을_준다()
    {
        var (state, tiger, attacker) = CombatFixture();
        var lightning = new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())
            .Single(skill => skill.Code == "lightning");
        var turn = HuntTurn(tiger, attacker, true, false) with
        {
            FiredActives = new Dictionary<UnitId, ActiveSkill> { [attacker.Id] = lightning },
            StratagemDamage = new Dictionary<UnitId, int> { [tiger.Id] = tiger.Pool.Active },
        };
        Assert.Single(Service.ResolveCombat(state, state.Armies, turn, 10, out _).Treasures);
    }

    [Fact]
    public void 저장이후_지속피해로_괴멸하면_이전_실제교전기여자에게_지급한다()
    {
        var (state, tiger, attacker) = CombatFixture();
        var hit = Service.ResolveCombat(state, state.Armies, HuntTurn(tiger, attacker, false, true), 9, out _);
        var restored = SaveService.Deserialize(SaveService.Serialize(hit));
        var burned = HuntTurn(tiger, attacker, true, false) with
        { StatusDamage = new Dictionary<UnitId, int> { [tiger.Id] = tiger.Pool.Active } };
        Assert.Single(Service.ResolveCombat(restored, restored.Armies, burned, 10, out _).Treasures);
    }

    [Fact]
    public void 캠페인은_빈야전에서도_대호를_출현시키고_실제전투처치를_정산한다()
    {
        var service = new EventHuntService(Map, [Tiger with { Troops = 50 }]);
        var legacy = new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(Map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(new RenewalCampaignAdvanceRunner(Map, legacy),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)), eventHunts: service);
        var appeared = engine.AdvanceWeek(World, out var spawnTurns);
        var tiger = Assert.Single(appeared.Armies);
        Assert.Contains(spawnTurns.SelectMany(turn => turn.Deployments), unit => unit.Id == tiger.Id);
        var attacker = tiger with { Field = tiger.Field with { Id = new(2), Owner = new(1),
            Position = new(1, 0), Speed = 0, Mode = UnitMode.Attack, Target = tiger.Field.Position,
            AssignedUnitTarget = tiger.Id }, RenewalPosition = RenewalHexSpace.Center(new(1, 0)),
            Stats = new(10000, 100, 100), Pool = new(10000, 0), TroopCode = "swordsman", VanguardId = new(1) };
        var hunted = engine.AdvanceWeek(appeared with { FieldArmies = [tiger, attacker],
            Generals = [new(new(1), "사냥장수", new Dictionary<TroopClass, AptitudeGrade>
                { [TroopClass.Cavalry] = AptitudeGrade.D }, 70, 70, 70)] }, out var fights);
        Assert.DoesNotContain(hunted.Armies, unit => unit.Id == tiger.Id);
        Assert.NotEmpty(fights.SelectMany(turn => turn.FieldCombatExchanges));
        Assert.Single(hunted.Treasures);
        Assert.Contains(engine.LastWorldEvents, e => e.Kind == WorldEventKind.EventUnitDefeated);
    }

    [Fact]
    public void 부대추격중인_대호가_빈성_옆에있어도_성을_점령하지않는다()
    {
        var spawned = Spawn();
        var tiger = spawned.Armies.Single();
        var empty = City with { Position = new(1, 0), Wall = 0 };
        tiger = tiger with { Field = tiger.Field with { Mode = UnitMode.Attack, Target = new(3, 0) } };
        var result = new CityCapture().ResolveAll(spawned with { Cities = [empty], FieldArmies = [tiger] },
            new SeededRandomSource(1), out var captures);
        Assert.Empty(captures);
        Assert.Equal(City.Owner, result.Cities.Single().Owner);
    }

    [Fact]
    public void 대호는_대기중_인접한_건축물을_자동공격하지않는다()
    {
        var spawned = Spawn();
        var definitions = new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());
        var fort = new FieldBuilding(new(1), "fort", new(1), new(3, 0), 4000, 0, 0);
        var state = spawned with { FieldBuildings = [fort] };
        var combat = new FieldBuildingCombat(new BattleResolver(60), definitions);
        var result = combat.Resolve(state, state.Armies);
        Assert.Empty(result.Exchanges);
        Assert.Equal(fort.HitPoints, Assert.Single(result.State.Buildings).HitPoints);
    }
}
