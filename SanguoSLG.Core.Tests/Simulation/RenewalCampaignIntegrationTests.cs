namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Game;

public sealed class RenewalCampaignIntegrationTests
{
    [Theory]
    [InlineData(UnitMode.March, UnitMode.March)]
    [InlineData(UnitMode.March, UnitMode.Attack)]
    [InlineData(UnitMode.Attack, UnitMode.March)]
    [InlineData(UnitMode.Attack, UnitMode.Attack)]
    public void 경로를_막은_적과는_모드와_세력에_관계없이_상호교전하고_원래명령을_보존한다(UnitMode firstMode, UnitMode secondMode)
    {
        var first = Army(1, new(0, 0), new(4, 0)) with
        { Field = Army(1, new(0, 0), new(4, 0)).Field with { Mode = firstMode } };
        var second = Army(2, new(1, 0), new(-4, 0)) with
        { Field = Army(2, new(1, 0), new(-4, 0)).Field with { Mode = secondMode, Owner = new(2) } };
        Engine(new HexMap(-6, 12, -4, 12)).AdvanceWeek(World(first, second) with
        {
            FieldBuildings = [new FieldBuilding(new(1), "fort", new(2), new(4, 0), 4000, 0, 0),
                new FieldBuilding(new(2), "fort", new(1), new(-4, 0), 4000, 0, 0)],
        }, out var turns);
        Assert.Contains(turns[0].FieldCombatExchanges, x => x.Attacker == first.Id && x.Target == second.Id);
        Assert.Contains(turns[0].FieldCombatExchanges, x => x.Attacker == second.Id && x.Target == first.Id);
        foreach (var original in new[] { first, second })
        {
            var actual = turns[0].Units.Single(x => x.Id == original.Id);
            Assert.Equal(original.Field.Mode, actual.Field.Mode);
            Assert.Equal(original.Field.Target, actual.Field.Target);
            Assert.True(turns[0].Combat!.DamageDealt[original.Id] > 0);
        }
    }

    [Fact]
    public void 부대지정공격도_다른적이_경로를막으면_교전하되_지정대상은_변경하지않는다()
    {
        var first = Army(1, new(0, 0), new(4, 0)) with
        { Field = Army(1, new(0, 0), new(4, 0)).Field with
            { Mode = UnitMode.Attack, AssignedUnitTarget = new(3), Detection = 10 } };
        var blocker = Army(2, new(1, 0), new(-4, 0)) with
        { Field = Army(2, new(1, 0), new(-4, 0)).Field with { Owner = new(2) } };
        var target = Army(3, new(4, 0), new(4, 0)) with
        { Field = Army(3, new(4, 0), new(4, 0)).Field with { Owner = new(2), Mode = UnitMode.Standby } };
        Engine(new HexMap(-6, 12, -4, 12)).AdvanceWeek(World(first, blocker, target), out var turns);
        Assert.Contains(turns[0].FieldCombatExchanges, x => x.Attacker == first.Id && x.Target == blocker.Id);
        Assert.Contains(turns[0].FieldCombatExchanges, x => x.Attacker == blocker.Id && x.Target == first.Id);
        var next = turns[0].Units.Single(x => x.Id == first.Id);
        Assert.Equal(first.Field.AssignedUnitTarget, next.Field.AssignedUnitTarget);
        Assert.Equal(first.Field.Target, next.Field.Target);
        Assert.Equal(UnitMode.Attack, next.Field.Mode);
    }

    [Fact]
    public void 행군은_경로를_막지않는_사거리내_적을_자동공격하지않는다()
    {
        var first = Army(1, new(0, 0), new(4, 0));
        var second = Army(2, new(0, 2), new(4, 2)) with
        { Field = Army(2, new(0, 2), new(4, 2)).Field with { Owner = new(2), AttackRange = 4 } };
        Engine(new HexMap(-6, 12, -4, 12)).AdvanceWeek(World(first, second), out var turns);
        Assert.Empty(turns[0].FieldCombatExchanges);
        Assert.NotEqual(first.RenewalPosition, turns[0].Units.Single(x => x.Id == first.Id).RenewalPosition);
    }

    [Fact]
    public void 실제캠페인_건축물액티브는_공격턴보고와_게이지초기화에_반영된다()
    {
        var map = new HexMap(-4, 12, -4, 12);
        var target = new HexCoord(1, 0);
        var active = new ActiveSkill("crush", "분쇄", ActiveType.Strike, "high", 180, BuildingOnly: true);
        var unit = Army(1, default, target) with
        {
            Field = Army(1, default, target).Field with { Mode = UnitMode.Attack, RangeCastle = 1 },
            State = UnitCombatState.Create(60, active).AdvanceField(5),
        };
        var building = new FieldBuilding(new FieldBuildingId(9), "fort", new FactionId(2),
            target, 1000000, 0, 0);
        Engine(map).AdvanceWeek(World(unit) with { FieldBuildings = [building] }, out var turns);
        var firing = turns.Where(turn => turn.FiredActives.ContainsKey(unit.Id)).ToArray();
        Assert.Equal(2, firing.Length);
        Assert.All(firing, turn =>
        {
            Assert.Equal(active, turn.FiredActives[unit.Id]);
            Assert.NotEmpty(turn.FieldBuildingExchanges);
            Assert.Equal(0, turn.Units.Single(x => x.Id == unit.Id).State.SharedActiveGauge.ElapsedDays);
        });
    }

    [Fact]
    public void 실제캠페인_대기부대는_성을향하던_적과_접촉하면_교전한다()
    {
        var map = new HexMap(-4, 12, -4, 12);
        var ally = Army(1, new(0, 0), new(0, 0)) with
        {
            Field = Army(1, new(0, 0), new(0, 0)).Field with { Mode = UnitMode.Standby },
        };
        var enemy = Army(2, new(1, 0), new(8, 0)) with
        {
            Field = Army(2, new(1, 0), new(8, 0)).Field with { Owner = new(2), Mode = UnitMode.Attack },
        };
        Engine(map).AdvanceWeek(World(ally, enemy), out var turns);
        Assert.Contains(turns[0].FieldCombatExchanges, x => x.Attacker == ally.Id && x.Target == enemy.Id);
    }

    [Fact]
    public void 서로_건축물을_공격하러_가던_부대가_맞닥뜨리면_건축물보다_교전을_우선한다()
    {
        var map = new HexMap(-4, 12, -4, 12);
        var first = Army(1, new(0, 0), new(4, 0)) with
        {
            Field = Army(1, new(0, 0), new(4, 0)).Field with { Mode = UnitMode.Attack, RangeCastle = 1 },
        };
        var second = Army(2, new(1, 0), new(-3, 0)) with
        {
            Field = Army(2, new(1, 0), new(-3, 0)).Field with
                { Owner = new(2), Mode = UnitMode.Attack, RangeCastle = 1 },
        };
        var buildings = new[]
        {
            new FieldBuilding(new FieldBuildingId(1), "fort", new FactionId(2), new HexCoord(4, 0), 4000, 0, 0),
            new FieldBuilding(new FieldBuildingId(2), "fort", new FactionId(1), new HexCoord(-3, 0), 4000, 0, 0),
        };

        var after = Engine(map).AdvanceWeek(World(first, second) with { FieldBuildings = buildings }, out var turns);

        Assert.Contains(turns[0].FieldCombatExchanges, exchange => exchange.Attacker == first.Id && exchange.Target == second.Id);
        Assert.Contains(turns[0].FieldCombatExchanges, exchange => exchange.Attacker == second.Id && exchange.Target == first.Id);
        Assert.True(turns[0].Combat?.DamageTaken.GetValueOrDefault(first.Id) > 0);
        Assert.True(turns[0].Combat?.DamageTaken.GetValueOrDefault(second.Id) > 0);
        Assert.Equal(buildings[0].Position, after.Armies.Single(unit => unit.Id == first.Id).Field.Target);
        Assert.Equal(buildings[1].Position, after.Armies.Single(unit => unit.Id == second.Id).Field.Target);
    }

    [Fact]
    public void 건축물공격부대는_행군중인_적에게_막히면_행군병력을_공격한다()
    {
        var map = new HexMap(-4, 12, -4, 12);
        var attacker = Army(1, new(0, 0), new(4, 0)) with
        {
            Field = Army(1, new(0, 0), new(4, 0)).Field with { Mode = UnitMode.Attack, RangeCastle = 1 },
        };
        var marcher = Army(2, new(1, 0), new(1, 0)) with
        {
            Field = Army(2, new(1, 0), new(1, 0)).Field with { Owner = new(2) },
        };
        var building = new FieldBuilding(new FieldBuildingId(1), "fort", new FactionId(2),
            new HexCoord(4, 0), 4000, 0, 0);

        Engine(map).AdvanceWeek(World(attacker, marcher) with { FieldBuildings = [building] }, out var turns);

        Assert.Contains(turns[0].FieldCombatExchanges, exchange => exchange.Attacker == attacker.Id && exchange.Target == marcher.Id);
        Assert.Contains(turns[0].FieldCombatExchanges, exchange => exchange.Attacker == marcher.Id && exchange.Target == attacker.Id);
    }

    [Fact]
    public void 야전교전은_실제_공격자와_피격자의_연속좌표를_보존한다()
    {
        var map = new HexMap(-4, 10, -4, 10);
        var attacker = Army(1, new(0, 0), new(0, 0)) with
        {
            Field = Army(1, new(0, 0), new(0, 0)).Field with { Mode = UnitMode.Advance, ContinuousTarget = new(100, 100) },
            RenewalPosition = new ContinuousPosition(100, 100),
        };
        var defender = Army(2, new(1, 0), new(1, 0)) with
        {
            Field = Army(2, new(1, 0), new(1, 0)).Field with
                { Owner = new FactionId(2), Mode = UnitMode.Standby },
            RenewalPosition = new ContinuousPosition(1000, 100),
        };

        Engine(map).AdvanceWeek(World(attacker, defender), out var turns);

        var exchange = Assert.Single(turns[0].FieldCombatExchanges,
            value => value.Attacker == attacker.Id && value.Target == defender.Id);
        Assert.Equal(attacker.RenewalPosition, exchange.AttackerPosition);
        Assert.Equal(defender.RenewalPosition, exchange.TargetPosition);
        Assert.True(exchange.IsPrimaryTarget);
    }

    [Fact]
    public void 전진부대가_발견한_적성에_접적하면_같은날_공성하고_영구정지하지않는다()
    {
        var map = new HexMap(-4, 12, -4, 12);
        var city = new City(new CityId(3), "한중", new HexCoord(4, 6),
            new FactionId(2), 0, CastleSize.Small, Wall: 1200);
        var guanYu = Army(1, new HexCoord(3, 6), new HexCoord(5, 4), 3) with
        {
            Field = Army(1, new HexCoord(3, 6), new HexCoord(5, 4), 3).Field with
                { Mode = UnitMode.Advance, RangeCastle = 1 },
        };
        var world = World(guanYu) with
        {
            Cities = [city],
            GarrisonForces = [new GarrisonForce(city.Id, "swordsman", 10000, 60)],
        };

        var after = Engine(map).AdvanceWeek(world, out _, out var sieges);

        Assert.Contains(sieges, exchange => exchange.City == city.Id);
        Assert.True(after.Cities.Single().Wall < city.Wall);
    }

    [Fact]
    public void 실제캠페인에서_같은위치의_적군은_첫이동전에_분리된다()
    {
        var map = new HexMap(-4, 12, -4, 12);
        var first = Army(1, new HexCoord(2, 2), new HexCoord(6, 2), 2);
        var second = Army(2, new HexCoord(2, 2), new HexCoord(0, 2), 2) with
        {
            Field = Army(2, new HexCoord(2, 2), new HexCoord(0, 2), 2).Field with
                { Owner = new FactionId(2) },
        };

        var after = Engine(map).AdvanceWeek(World(first, second), out _);

        var positions = after.Armies.OrderBy(unit => unit.Id.Value)
            .Select(unit => unit.RenewalPosition!.Value).ToArray();
        Assert.True(positions[0].DistanceTo(positions[1])
            >= RenewalAdvanceSimulator.UnitCollisionRadius * 2);
    }

    private static CampaignEngine Engine(HexMap map, bool legacy = false,
        Action<RenewalCampaignTraceEntry>? trace = null)
    {
        var rules = new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var definitions = new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());
        return new CampaignEngine(legacy ? rules : new RenewalCampaignAdvanceRunner(map, rules, trace),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)),
            new CampaignSiege(new BattleResolver(60), troops), new CityCapture(),
            ruinCombat: new RuinCombat(new BattleResolver(60)),
            fieldBuildingCombat: new FieldBuildingCombat(new BattleResolver(60), definitions, troops),
            fieldBuildingDefinitions: definitions);
    }

    private static CombatUnit Army(int id, HexCoord start, HexCoord goal, int speed = 1) => new(
        new FieldUnit(new UnitId(id), new FactionId(1), start, speed, 3, 1,
            MovementDomain.Land, UnitMode.March, goal, id,
            ContinuousTarget: RenewalHexSpace.Center(goal)),
        new CombatStats(10000, 20, 20), new TroopPool(10000, 0), UnitCombatState.Create(60),
        TroopCode: "swordsman", RenewalPosition: RenewalHexSpace.Center(start));

    private static GameState World(params CombatUnit[] units) => new(1, 190, [], [], [], FieldArmies: units);

    [Theory]
    [InlineData("combat")]
    [InlineData("supply")]
    [InlineData("transport")]
    [InlineData("group")]
    [InlineData("ai")]
    public void 실제_캠페인_14일과_저장재개에서_모든편성의_정확목표를_보존한다(string kind)
    {
        var map = new HexMap(-4, 30, -4, 12);
        var unit = Army(1, new(0, 0), new(20, 0));
        var center = RenewalHexSpace.Center(new(20, 0));
        var exact = center with { Y = center.Y + 130 };
        unit = unit with
        {
            Field = unit.Field with { ContinuousTarget = exact, Owner = new(kind == "ai" ? 2 : 1) },
            IsSupply = kind == "supply", IsTransport = kind == "transport", IsArmyGroup = kind == "group",
        };
        var trace = new List<RenewalCampaignTraceEntry>();
        var engine = Engine(map, trace: trace.Add);
        var first = engine.AdvanceWeek(World(unit), out var turns);
        Assert.Equal(8, first.Day);
        Assert.Equal(7, turns.Count);
        var saved = SaveService.Deserialize(SaveService.Serialize(first));
        var second = engine.AdvanceWeek(first, out _);
        var restored = Engine(map).AdvanceWeek(saved, out _);
        Assert.Equal(15, second.Day);
        Assert.Equal(Assert.Single(second.Armies).RenewalPosition, Assert.Single(restored.Armies).RenewalPosition);
        Assert.Equal(exact, Assert.Single(restored.Armies).Field.ContinuousTarget);
        Assert.True(Assert.Single(second.Armies).RenewalPosition!.Value.X > Assert.Single(first.Armies).RenewalPosition!.Value.X,
            $"first={first.Armies[0].RenewalPosition} second={second.Armies[0].RenewalPosition} trace={trace[^1]}");
    }

    [Fact]
    public void 경유지는_먼저도달하고_다음날_소비된경유지로_되돌아가지않는다()
    {
        var engine = Engine(new HexMap(-5, 20, -5, 15));
        var unit = Army(1, new(0, 0), new(8, 0));
        var waypoint = new HexCoord(0, 2);
        unit = unit with { Field = unit.Field with { Waypoints = [waypoint],
            ContinuousWaypoints = [RenewalHexSpace.Center(waypoint)] } };
        var after = engine.AdvanceWeek(World(unit), out var turns);
        var all = turns.SelectMany(turn => turn.Movement.Ticks).Select(tick => tick.ContinuousPositions[unit.Id]).ToList();
        Assert.True(all.Any(position => position.DistanceTo(RenewalHexSpace.Center(waypoint)) <= 50),
            $"last={all[^1]} remaining={string.Join(',', after.Armies[0].Field.ContinuousWaypoints ?? [])}");
        Assert.Empty(Assert.Single(after.Armies).Field.ContinuousWaypoints!);
        Assert.True(all[^1].X > RenewalHexSpace.Center(waypoint).X);
    }

    [Theory]
    [InlineData(CastleSize.Small)]
    [InlineData(CastleSize.Medium)]
    [InlineData(CastleSize.Large)]
    public void 같은방향_두부대는_같은날_출격하고_적봉쇄는_예약에남는다(CastleSize size)
    {
        var map = new HexMap(-8, 25, -8, 15);
        var city = new City(new(1), "출격성", new(0, 0), new(1), 1000, size);
        var exit = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.East)!.Value;
        var units = Enumerable.Range(1, 2).Select(id => Army(id, city.Position, new(20, 0), 2) with
        { OriginCity = city.Id, EgressDirection = DeploymentDirection.East, EgressExit = exit, AwaitingEgress = true }).ToArray();
        var state = World(units) with { Cities = [city] };
        Engine(map).AdvanceWeek(state, out var turns);
        Assert.Equal(2, turns[0].Deployments.Count);
        Assert.All(turns[0].Deployments, unit => Assert.Equal(exit, unit.Field.Position));
        var enemy = Army(99, exit, exit, 0) with { Field = Army(99, exit, exit, 0).Field with { Owner = new(2) } };
        var blocked = Engine(map).AdvanceWeek(state with { FieldArmies = [.. units, enemy] }, out var blockedTurns);
        Assert.All(blockedTurns, turn => Assert.Empty(turn.Deployments));
        Assert.Equal(2, blocked.Armies.Count(unit => unit.IsWaitingEgress));
        Engine(map, legacy: true).AdvanceWeek(state, out var legacyTurns);
        Assert.Single(legacyTurns[0].Deployments);
    }

    [Theory]
    [InlineData(CastleSize.Small, PortSize.None)]
    [InlineData(CastleSize.Medium, PortSize.None)]
    [InlineData(CastleSize.Small, PortSize.Small)]
    [InlineData(CastleSize.Medium, PortSize.Medium)]
    public void 지정성항구_입성은_이동후_공격전에_자원과병종을_한번만합산한다(CastleSize size, PortSize port)
    {
        var city = new City(new(1), "목적지", new(4, 0), new(1), 0, size, Port: port);
        var transport = port != PortSize.None;
        var unit = Army(1, new(0, 0), city.Position, 2) with
        { IsTransport = transport, CargoGold = 123, IsArmyGroup = !transport,
            SupplyCargo = [new("swordsman", 5000, 50), new("archer", 5000, 50)] };
        var engine = Engine(new HexMap(-8, 15, -8, 10));
        var after = engine.AdvanceWeek(World(unit) with { Cities = [city] }, out var turns);
        Assert.Empty(after.Armies);
        Assert.Single(turns.SelectMany(turn => turn.EnteredCastle));
        var baseline = Engine(new HexMap(-8, 15, -8, 10)).AdvanceWeek(World() with { Cities = [city] }, out _);
        Assert.Equal(baseline.Cities.Single().Gold + 123, after.Cities.Single().Gold);
        Assert.Equal(10000, after.Garrisons.Sum(g => g.Troops));
        Assert.DoesNotContain(after.Garrisons, g => g.TroopCode == "army_group");
        Assert.Empty(engine.AdvanceWeek(after, out _).Armies);
    }

    [Fact]
    public void 유적_지정공격은_실제캠페인에서_접근하여_교전하고_관통하지않는다()
    {
        var goal = new HexCoord(4, 0);
        var unit = Army(1, new(0, 0), goal, 2);
        unit = unit with { Field = unit.Field with { Mode = UnitMode.Attack } };
        var state = World(unit) with { RuinDefinitions = [new("qa", "검수유적", goal, "swordsman", 100000)],
            RuinStates = [new("qa", 100000)] };
        var after = Engine(new HexMap(-6, 15, -6, 10)).AdvanceWeek(state, out var turns);
        Assert.True(turns.Any(turn => turn.RuinExchanges.Count > 0),
            $"positions={string.Join(';', turns.SelectMany(turn => turn.Units).Select(unit => unit.RenewalPosition))}");
        Assert.DoesNotContain(turns.SelectMany(turn => turn.Movement.Ticks).SelectMany(tick => tick.Units),
            field => field.Position == goal);
        Assert.True(after.RuinStatus.Single().Defenders < 100000);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void 실제캠페인_매일_연속재생종점이_공격시작좌표와_일치한다(int fps)
    {
        var unit = Army(1, new(0, 0), new(20, 0));
        var after = Engine(new HexMap(-8, 25, -8, 10)).AdvanceWeek(World(unit), out var turns);
        var playback = new MovementPlayback(new() { [1] = unit.Field.Position },
            new() { [1] = unit.RenewalPosition!.Value });
        for (var day = 0; day < turns.Count; day++) playback.Append(turns[day].Movement, day, 2.5, .5, 1.5);
        Assert.Empty(playback.Moves);
        Assert.Equal(7, playback.ContinuousTracks.Count);
        for (var day = 0; day < 7; day++)
        {
            var track = playback.ContinuousTracks[day];
            var endpoint = RenewalPlaybackSampler.Sample(track.Points, 1_500_000, 1_500_000);
            for (var frame = 0; frame <= (int)(1.5 * fps); frame++)
                RenewalPlaybackSampler.Sample(track.Points, frame * 1_000_000L / fps, 1_500_000);
            Assert.Equal(turns[day].Units.Single().RenewalPosition, endpoint);
        }
        Assert.Equal(after.Armies.Single().RenewalPosition, playback.ContinuousTracks[^1].Points[^1]);
    }

    [Fact]
    public void 해상부대는_14일_바다경로만따라_정확목표에도달한다()
    {
        var water = new HexMap(-5, 25, -5, 5).Tiles().ToDictionary(hex => hex, _ => TerrainType.WaterDeep);
        var map = new HexMap(-5, 25, -5, 5, water);
        var unit = Army(1, new(0, 0), new(20, 0), 2);
        unit = unit with { Field = unit.Field with { Domain = MovementDomain.DeepWater }, Class = TroopClass.Naval };
        var engine = Engine(map);
        var first = engine.AdvanceWeek(World(unit), out _);
        var after = engine.AdvanceWeek(first, out var turns);
        Assert.Equal(unit.Field.ContinuousTarget, Assert.Single(after.Armies).RenewalPosition);
        Assert.All(turns.SelectMany(turn => turn.Movement.Ticks).SelectMany(tick => tick.Units),
            field => Assert.Equal(TerrainType.WaterDeep, map.TerrainAt(field.Position)));
    }

    [Fact]
    public void 타일이_인접해도_연속사거리_밖이면_공격과충전이_발생하지않는다()
    {
        var attacker = Army(1, new(0, 0), new(1, 0), 0);
        attacker = attacker with { Field = attacker.Field with { Mode = UnitMode.Attack } };
        var enemy = Army(2, new(1, 0), new(1, 0), 0);
        enemy = enemy with { Field = enemy.Field with { Owner = new(2), Mode = UnitMode.Standby },
            RenewalPosition = new ContinuousPosition(1300, 500) };
        var after = Engine(new HexMap(-4, 10, -4, 10)).AdvanceWeek(World(attacker, enemy), out var turns);
        Assert.All(turns, turn => Assert.Null(turn.Combat));
        Assert.All(after.Armies, unit => Assert.Equal(10000, unit.Pool.Active));
    }

    [Fact]
    public void 이동중인_지정적군은_타일을바꿔도_ID로_추적한다()
    {
        var map = new HexMap(-5, 20, -5, 10);
        var runner = new RenewalCampaignAdvanceRunner(map, new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), 70)));
        var attacker = Army(1, new(0, 0), new(3, 0), 2);
        attacker = attacker with { Field = attacker.Field with { Mode = UnitMode.Attack } };
        var enemy = Army(2, new(3, 0), new(15, 0), 1);
        enemy = enemy with { Field = enemy.Field with { Owner = new(2) } };
        var first = runner.Run([attacker, enemy], 1);
        Assert.Equal(enemy.Id, first.Units.First(unit => unit.Id == attacker.Id).Field.AssignedUnitTarget);
        var second = runner.Run(first.Units, 1);
        Assert.Equal(enemy.Id, second.Units.First(unit => unit.Id == attacker.Id).Field.AssignedUnitTarget);
        Assert.NotEqual(first.Units.First(unit => unit.Id == attacker.Id).RenewalPosition,
            second.Units.First(unit => unit.Id == attacker.Id).RenewalPosition);
    }

    [Fact]
    public void 혼란중에는_이동하지않고_만료뒤_원래명령으로_이동한다()
    {
        var unit = Army(1, new(0, 0), new(15, 0), 2);
        unit = unit with { State = unit.State.AddStatus(new(StatusKind.Daze, 0, 1, false)) };
        Engine(new HexMap(-5, 20, -5, 10)).AdvanceWeek(World(unit), out var turns);
        Assert.Equal(unit.RenewalPosition, turns[0].Units.Single().RenewalPosition);
        Assert.NotEqual(unit.RenewalPosition, turns[1].Units.Single().RenewalPosition);
    }

    [Fact]
    public void 실제교전_괴멸은_그날_제거되고_다음날_재공격하지않는다()
    {
        var attacker = Army(1, new(0, 0), new(0, 1), 0);
        attacker = attacker with { Field = attacker.Field with { Mode = UnitMode.Attack },
            Stats = new CombatStats(10000, 500, 100) };
        var defender = Army(2, new(0, 1), new(0, 1), 0);
        defender = defender with { Field = defender.Field with { Owner = new(2), Mode = UnitMode.Standby },
            Pool = new TroopPool(100, 0), Stats = new CombatStats(100, 1, 1) };
        var after = Engine(new HexMap(-5, 20, -5, 10)).AdvanceWeek(World(attacker, defender), out var turns);
        Assert.NotNull(turns[0].Combat);
        Assert.DoesNotContain(turns[0].Units, unit => unit.Id == defender.Id);
        Assert.All(turns.Skip(1), turn => Assert.Null(turn.Combat));
        Assert.Single(after.Armies);
    }

    [Fact]
    public void 인접한_아군_두부대가_같은_적을_공격하면_둘다_교전한다()
    {
        var enemy = Army(3, new(0, 1), new(0, 1), 0) with
        {
            Field = Army(3, new(0, 1), new(0, 1), 0).Field with
                { Owner = new(2), Mode = UnitMode.March },
        };
        var left = Army(1, new(0, 0), enemy.Field.Position, 0) with
        {
            Field = Army(1, new(0, 0), enemy.Field.Position, 0).Field with
                { Mode = UnitMode.Attack, AssignedUnitTarget = enemy.Id },
        };
        var right = Army(2, new(1, 0), enemy.Field.Position, 0) with
        {
            Field = Army(2, new(1, 0), enemy.Field.Position, 0).Field with
                { Mode = UnitMode.Attack, AssignedUnitTarget = enemy.Id },
        };
        Engine(new HexMap(-5, 20, -5, 10)).AdvanceWeek(World(left, right, enemy), out var turns);
        var attackers = turns[0].FieldCombatExchanges.Where(exchange => exchange.Target == enemy.Id)
            .Select(exchange => exchange.Attacker).ToHashSet();
        Assert.Contains(left.Id, attackers);
        Assert.Contains(right.Id, attackers);
        Assert.True(turns[0].Combat?.DamageDealt.GetValueOrDefault(left.Id) > 0);
        Assert.True(turns[0].Combat?.DamageDealt.GetValueOrDefault(right.Id) > 0);
    }

    [Fact]
    public void 건축물과_경로상다른성은_통과하거나_잘못입성하지않는다()
    {
        var city = new City(new(1), "경로중성", new(2, 0), new(1), 0);
        var building = new FieldBuilding(new(1), "palisade", new(1), new(4, 0), 1500, 0, 0);
        var unit = Army(1, new(0, 0), new(10, 0), 2);
        Engine(new HexMap(-5, 20, -5, 10)).AdvanceWeek(World(unit) with
            { Cities = [city], FieldBuildings = [building] }, out var turns);
        Assert.All(turns, turn => Assert.Empty(turn.EnteredCastle));
        Assert.DoesNotContain(turns.SelectMany(turn => turn.Movement.Ticks).SelectMany(tick => tick.Units),
            field => field.Position == city.Position || field.Position == building.Position);
    }

    [Theory]
    [InlineData(PortSize.None)]
    [InlineData(PortSize.Small)]
    public void 성항구_괴멸직후_점령입성과_집단군분해를_같은공격후처리에반영한다(PortSize port)
    {
        var city = new City(new(2), "적거점", new(4, 0), new(2), 0, Wall: 0, Port: port);
        var unit = Army(1, new(0, 0), city.Position, 2);
        unit = unit with { Field = unit.Field with { Mode = UnitMode.Attack },
            IsArmyGroup = true, TroopCode = "army_group", SupplyCargo = [new("swordsman", 5000, 50), new("archer", 5000, 50)] };
        var state = World(unit) with { Cities = [city], GarrisonForces = [new(city.Id, "swordsman", 1, 50)] };
        var after = Engine(new HexMap(-5, 20, -5, 10)).AdvanceWeek(state, out var turns, out _, out var captures);
        Assert.Single(captures);
        Assert.Equal(unit.Field.Owner, after.Cities.Single().Owner);
        Assert.Empty(after.Armies);
        Assert.DoesNotContain(after.Garrisons, force => force.TroopCode == "army_group");
        Assert.DoesNotContain(turns.Last().Units, survivor => survivor.Id == unit.Id);
    }

    [Fact]
    public void 야전건축물_공격은_이동종점에서_피해를_계산한다()
    {
        var building = new FieldBuilding(new(1), "palisade", new(2), new(4, 0), 100, 0, 0);
        var unit = Army(1, new(0, 0), building.Position, 2);
        unit = unit with { Field = unit.Field with { Mode = UnitMode.Attack } };
        var after = Engine(new HexMap(-5, 20, -5, 10)).AdvanceWeek(World(unit) with
            { FieldBuildings = [building] }, out var turns);
        Assert.Contains(turns.SelectMany(turn => turn.FieldBuildingExchanges), exchange => exchange.Destroyed);
        Assert.Empty(after.Buildings);
        Assert.DoesNotContain(turns.SelectMany(turn => turn.Movement.Ticks).SelectMany(tick => tick.Units),
            field => field.Position == building.Position);
    }

    [Fact]
    public void 실제맵_장안_남동출격은_공격불가정찰대에_14일간_멈추지않는다()
    {
        var map = new HexMap(-8, 20, -5, 16);
        var scout = new FieldBuilding(new(800006), "scout_post", new(2), new(1, 5), 0, 0, 0);
        var palisade = new FieldBuilding(new(800007), "palisade", new(2), new(1, 6), 1500, 0, 0);
        var fort = new FieldBuilding(new(800009), "fort", new(2), new(1, 7), 4000, 0, 0);
        var army = Army(1, new(1, 4), fort.Position, 3) with
        {
            Field = Army(1, new(1, 4), fort.Position, 3).Field with { Mode = UnitMode.Advance },
        };
        var traces = new List<RenewalCampaignTraceEntry>();
        var engine = Engine(map, trace: traces.Add);
        var first = engine.AdvanceWeek(World(army) with
            { FieldBuildings = [scout, palisade, fort] }, out var firstTurns);
        var second = engine.AdvanceWeek(first, out var secondTurns);
        Assert.DoesNotContain(traces, entry => entry.PursuitTarget ==
            new RenewalTargetId(RenewalTargetKind.Building, scout.Id.Value));
        Assert.NotEqual(army.RenewalPosition,
            Assert.Single(first.Armies).RenewalPosition);
        Assert.DoesNotContain(traces, entry => entry.StopReason == RenewalStopReason.NoPath
            && entry.PursuitTarget == new RenewalTargetId(RenewalTargetKind.Building, palisade.Id.Value));
        Assert.True(firstTurns.Concat(secondTurns).SelectMany(turn => turn.FieldBuildingExchanges)
            .Any(exchange => exchange.Building == palisade.Id || exchange.Building == fort.Id),
            "건축물 추격이 시야 경계에서 반복되지 않고 실제 공격까지 이어져야 합니다.");
        Assert.True(firstTurns.Concat(secondTurns).SelectMany(turn => turn.Movement.Ticks)
            .Any(tick => tick.ContinuousPositions.TryGetValue(army.Id, out var position)
                && position != army.RenewalPosition),
            $"unit={second.Armies.FirstOrDefault()?.RenewalPosition} trace={traces.LastOrDefault()}");
    }

    [Fact]
    public void 정찰대만_있어도_전진은_원래_지점으로_진행한다()
    {
        var scout = new FieldBuilding(new(800006), "scout_post", new(2), new(1, 5), 0, 0, 0);
        var goal = RenewalHexSpace.Center(new HexCoord(1, 8)) with { X = 920 };
        var unit = Army(1, new(1, 4), new(1, 8), 2) with
        { Field = Army(1, new(1, 4), new(1, 8), 2).Field with
            { Mode = UnitMode.Advance, ContinuousTarget = goal } };
        var first = Engine(new HexMap(-8, 20, -5, 16)).AdvanceWeek(World(unit) with
            { FieldBuildings = [scout] }, out var turns);
        Assert.True(Assert.Single(first.Armies).RenewalPosition!.Value.DistanceTo(goal)
            < unit.RenewalPosition!.Value.DistanceTo(goal));
        Assert.Equal(goal, Assert.Single(first.Armies).Field.ContinuousTarget);
        var firstDayPositions = turns[0].Movement.Ticks
            .Select(tick => tick.ContinuousPositions.TryGetValue(unit.Id, out var position)
                ? position : (ContinuousPosition?)null)
            .OfType<ContinuousPosition>().ToList();
        Assert.NotEmpty(firstDayPositions);
        Assert.All(firstDayPositions, position =>
        {
            Assert.True(position.Y >= unit.RenewalPosition!.Value.Y,
                $"정찰대를 피해 북쪽으로 우회: {position}");
            Assert.InRange(position.X, 800, 1000);
        });
        Assert.Contains(scout.Id, turns.SelectMany(turn => turn.RemovedScoutPostIds ?? []));
        Assert.DoesNotContain(first.Buildings, building => building.Id == scout.Id);
    }

    [Fact]
    public void 중립유적은_전진의_자동추격대상이_아니지만_지정공격은_가능하다()
    {
        var ruin = new RuinDefinition("war_elephant_01", "상병 유적", new(2, 4),
            "war_elephant", 50000);
        var status = new RuinState(ruin.Id, 50000);
        var advance = Army(1, new(1, 4), new(1, 8), 2) with
        { Field = Army(1, new(1, 4), new(1, 8), 2).Field with
            { Mode = UnitMode.Advance } };
        var trace = new List<RenewalCampaignTraceEntry>();
        var world = World(advance) with { RuinDefinitions = [ruin], RuinStates = [status] };
        var after = Engine(new HexMap(-8, 20, -5, 16), trace: trace.Add)
            .AdvanceWeek(world, out _);
        Assert.DoesNotContain(trace, entry => entry.PursuitTarget is
            { Kind: RenewalTargetKind.Building, Value: -1 });
        Assert.NotEqual(advance.RenewalPosition, Assert.Single(after.Armies).RenewalPosition);
        var attack = advance with { Field = advance.Field with
            { Mode = UnitMode.Attack, Target = ruin.Position,
                ContinuousTarget = RenewalHexSpace.Center(ruin.Position) } };
        Engine(new HexMap(-8, 20, -5, 16)).AdvanceWeek(World(attack) with
            { RuinDefinitions = [ruin], RuinStates = [status] }, out var turns);
        Assert.Contains(turns, turn => turn.RuinExchanges.Count > 0);
    }

    [Fact]
    public void 같은타일의_서로다른_지점은_각각의_목표에서_멈춘다()
    {
        var center = RenewalHexSpace.Center(new HexCoord(4, 0));
        var goals = new[] { center with { X = center.X - 120 }, center with { X = center.X + 120 } };
        var engine = Engine(new HexMap(-5, 12, -5, 10));
        var units = goals.Select((goal, index) => Army(index + 1, new(0, 0), new(4, 0), 2) with
        { Field = Army(index + 1, new(0, 0), new(4, 0), 2).Field with
            { ContinuousTarget = goal } }).ToArray();
        var after = engine.AdvanceWeek(World(units), out _);
        Assert.Equal(goals[0], after.Armies.Single(unit => unit.Id.Value == 1).RenewalPosition);
        Assert.Equal(goals[1], after.Armies.Single(unit => unit.Id.Value == 2).RenewalPosition);
    }
}
