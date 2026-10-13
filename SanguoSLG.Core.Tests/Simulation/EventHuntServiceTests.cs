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
        .LoadFromDirectory(TestData.DataDirectory()).Single();
    private static readonly City City = new(new(1), "발견성", new(0, 0), new(1), 1000);
    private static GameState World => new(1, 190, [], [City], [],
        ExplorationDiscoveries: [new(1, new(1), City.Id, new(1), ExplorationResultKind.DivineBeast,
            "divine_beast_trace")]);
    private static HexMap Map => new(-8, 8, -8, 8,
        new Dictionary<HexCoord, TerrainType> { [new(2, 0)] = TerrainType.Forest, [new(3, 0)] = TerrainType.Mountain });
    private static EventHuntService Service => new(Map, [Tiger]);

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
        Assert.Equal(new HexCoord(3, 0), Assert.Single(Service.SpawnDue(blocked, 8, out _).Armies).Field.Position);
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
    public void 숲과산이_있으면_가까운평지보다_우선해서_출현한다()
    {
        var map = new HexMap(-8, 8, -8, 8,
            new Dictionary<HexCoord, TerrainType> { [new(5, 0)] = TerrainType.Forest });
        var service = new EventHuntService(map, [Tiger]);
        var spawned = service.SpawnDue(service.ScheduleDiscoveries(World), 8, out _);
        Assert.Equal(new HexCoord(5, 0), Assert.Single(spawned.Armies).Field.Position);
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
        var waiting = engine.AdvanceWeek(discovered, out _);
        var appeared = engine.AdvanceWeek(waiting, out _);
        Assert.Single(appeared.Armies, unit => unit.Field.Owner == EventHuntService.EventFaction);
        Assert.Contains(engine.LastWorldEvents, e => e.Kind == WorldEventKind.EventUnitAppeared);
    }

    [Fact]
    public void 두도시가_단서를_찾아도_활동중인_대호는_한마리다()
    {
        var world = World with { ExplorationDiscoveries = World.Discoveries.Append(
            new(2, new(2), City.Id, new(2), ExplorationResultKind.DivineBeast, "divine_beast_trace")).ToList() };
        var spawned = Service.SpawnDue(Service.ScheduleDiscoveries(world), 9, out _);
        var next = Service.SpawnDue(spawned, 30, out _);
        Assert.Single(next.Armies);
        Assert.Single(next.Encounters, e => e.Unit is null);
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
            Assert.Equal(2, general.Level);
            Assert.Equal(125, general.Experience);
            Assert.Equal(10, general.AptitudeExperienceFor(TroopClass.Infantry));
        });
        Assert.Single(events);
        var restored = SaveService.Deserialize(SaveService.Serialize(rewarded));
        Assert.Single(Service.ResolveCombat(restored, state.Armies, turn, 11, out var duplicate).Treasures);
        Assert.Empty(duplicate);
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
        var first = engine.AdvanceWeek(World, out _);
        Assert.Empty(first.Armies);
        var appeared = engine.AdvanceWeek(first, out var spawnTurns);
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
