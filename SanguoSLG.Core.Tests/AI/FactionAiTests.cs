namespace SanguoSLG.Core.Tests.AI;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>세력 AI 최소판 — v2 기준 출전·재조준 판단이 결정론적으로 나오는지.</summary>
public class FactionAiTests
{
    [Fact]
    public void AI_첫출전_공격명령은_출격한_다음_주부터_전진으로_전환한다()
    {
        var field = new FieldUnit(new UnitId(91), new FactionId(1), default, 2, 3, 1,
            MovementDomain.Land, UnitMode.Attack, new HexCoord(4, 0), 1);
        var army = new CombatUnit(field, new CombatStats(1000, 10, 10),
            new TroopPool(1000, 0), UnitCombatState.Create(60), AiOpeningAttack: true);
        var waiting = army with { Field = field with { Id = new UnitId(92) }, AwaitingEgress = true };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [army, waiting]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(state, new FactionId(1));

        Assert.Equal(UnitMode.Advance, after.Armies.Single(unit => unit.Id == army.Id).Field.Mode);
        Assert.False(after.Armies.Single(unit => unit.Id == army.Id).AiOpeningAttack);
        Assert.Equal(UnitMode.Attack, after.Armies.Single(unit => unit.Id == waiting.Id).Field.Mode);
        Assert.True(after.Armies.Single(unit => unit.Id == waiting.Id).AiOpeningAttack);
    }

    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static FactionAI Ai(AiConfig? config = null) => new(
        new CommandService(B, Troops),
        new DeployService(B, Troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
        config);

    private static General Gen(int id) => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Infantry] = AptitudeGrade.A,
            [TroopClass.Supply] = AptitudeGrade.C,
        },
        Might: 70, Intellect: 60, Politics: 80);

    private static General SupplyGen(int id, AptitudeGrade grade) => Gen(id) with
    {
        Aptitudes = new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Infantry] = AptitudeGrade.A,
            [TroopClass.Supply] = grade,
        },
    };

    private static General ArmyGroupGen(int id, AptitudeGrade infantry = AptitudeGrade.A, AptitudeGrade archer = AptitudeGrade.A, AptitudeGrade siege = AptitudeGrade.A, AptitudeGrade cavalry = AptitudeGrade.C) => Gen(id) with
    {
        Aptitudes = new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Infantry] = infantry,
            [TroopClass.Archer] = archer,
            [TroopClass.Siege] = siege,
            [TroopClass.Cavalry] = cavalry,
            [TroopClass.Supply] = AptitudeGrade.C,
        },
    };

    private static City Town(int id, int owner, HexCoord pos, int ore = 5000) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 3000, CastleSize.Medium,
            Gold: 2000, Population: 100_000, Ore: ore);

    private static GeneralPosting At(int general, int faction, int city) =>
        new(new GeneralId(general), new FactionId(faction), new CityId(city));

    [Fact]
    public void AI는_모든_도시의_태수와_최고지력_군사를_임명하고_기존_태수를_유지한다()
    {
        var cities = new[] { Town(1, 1, new HexCoord(0, 0)), Town(2, 1, new HexCoord(3, 0)), Town(3, 2, new HexCoord(6, 0)) };
        var generals = new[] { Gen(1), Gen(2) with { Intellect = 95 }, Gen(3), Gen(4) };
        var state = new GameState(1, 190, [], cities, generals,
            Postings: [At(1, 1, 1), At(2, 1, 1), At(3, 1, 2), At(4, 2, 3)]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue, GeneralResearchReserveGold: -1,
            ExplorationReserveGold: int.MaxValue)).PlanWeek(state, new FactionId(1));
        var first = after.Cities.Single(c => c.Id == new CityId(1));
        var second = after.Cities.Single(c => c.Id == new CityId(2));
        Assert.Contains(first.Governor, new GeneralId?[] { new(1), new(2) });
        Assert.Equal(new GeneralId(2), first.Strategist);
        Assert.Equal(new GeneralId(3), second.Governor);
        Assert.Equal(new GeneralId(3), second.Strategist);
        Assert.Null(after.Cities.Single(c => c.Id == new CityId(3)).Governor);

        var next = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(after with { Day = 8 }, new FactionId(1));
        Assert.Equal(first.Governor, next.Cities.Single(c => c.Id == first.Id).Governor);
    }

    [Fact]
    public void AI는_치안_내정_병력담당을_서로_다른_장수로_임명하고_S급_병종을_생산한다()
    {
        var cavalry = Gen(3) with
        {
            Aptitudes = new Dictionary<TroopClass, AptitudeGrade>
            {
                [TroopClass.Infantry] = AptitudeGrade.A,
                [TroopClass.Cavalry] = AptitudeGrade.S,
            }
        };
        var state = new GameState(1, 190, [], [Town(1, 1, new HexCoord(0, 0))],
            [Gen(1) with { Might = 95 }, Gen(2) with { Politics = 95 }, cavalry],
            Postings: [At(1, 1, 1), At(2, 1, 1), At(3, 1, 1)]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue, GeneralResearchReserveGold: -1,
            ExplorationReserveGold: int.MaxValue)).PlanWeek(state, new FactionId(1));
        var city = Assert.Single(after.Cities);
        Assert.Equal(new GeneralId(1), city.SecurityOfficer);
        Assert.Equal(new GeneralId(2), city.DomesticOfficer);
        Assert.Equal(new GeneralId(3), city.RecruitmentOfficer);
        Assert.Equal("cavalry", city.AutoRecruitTroopCode);
    }

    [Fact]
    public void AI는_여유장수가_있으면_담당자를_탐색에_차출하지_않는다()
    {
        var city = Town(1, 1, new HexCoord(0, 0)) with
        {
            SecurityOfficer = new GeneralId(1), DomesticOfficer = new GeneralId(2),
            RecruitmentOfficer = new GeneralId(3), AutoRecruitTroopCode = "swordsman",
            AutoRecruitTroopCodes = "swordsman"
        };
        var state = new GameState(1, 190, [], [city],
            [Gen(1) with { Intellect = 95 }, Gen(2) with { Intellect = 90 },
                Gen(3) with { Intellect = 85 }, Gen(4) with { Intellect = 60 }],
            Postings: [At(1, 1, 1), At(2, 1, 1), At(3, 1, 1), At(4, 1, 1)]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue, GeneralResearchReserveGold: -1))
            .PlanWeek(state, new FactionId(1));
        Assert.Equal(new GeneralId(4), Assert.Single(after.Commands, c => c.Kind == CommandKind.Explore).Main);
        Assert.Equal(new GeneralId(1), Assert.Single(after.Cities).SecurityOfficer);
    }

    [Fact]
    public void 해금된_AI_위인은_금을_내고_자동_영입한다()
    {
        var hero = new HeroUnlockDefinition(new GeneralId(6), HeroUnlockType.Faction,
            Faction: new FactionId(1),
            RecruitGold: 900, RecruitPointCost: 100);
        var s = new GameState(1, 1,
            new List<Faction> { new(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0") },
            new List<City> { Town(1, 1, new HexCoord(0, 0)) },
            new List<General> { Gen(1), Gen(6) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            HeroUnlockDefinitions: new List<HeroUnlockDefinition> { hero },
            HeroUnlockStates: new List<HeroUnlockState> { new(new GeneralId(6), HeroUnlockStatus.Locked) },
            RecruitmentPoints: [new(new FactionId(1), 100)]);

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Equal(HeroUnlockStatus.Recruited, after.HeroStates.Single().Status);
        Assert.Equal((new FactionId(1), new CityId(1)), (after.PostingOf(new GeneralId(6))!.Faction, after.PostingOf(new GeneralId(6))!.Location));
        Assert.Equal(1100, after.Cities.Single().Gold);
        Assert.Equal(0, RecruitmentPointBank.Balance(after, new FactionId(1)));
    }

    [Fact]
    public void AI_영입_불가_위인은_해금되어도_자동_영입하지_않는다()
    {
        var hero = new HeroUnlockDefinition(new GeneralId(6), HeroUnlockType.Region,
            Conditions: [new HeroUnlockCondition("owned_cities", 1)], RecruitGold: 900, AiCanRecruit: false);
        var s = new GameState(1, 1,
            new List<Faction> { new(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0") },
            new List<City> { Town(1, 1, new HexCoord(0, 0)) },
            new List<General> { Gen(1), Gen(6) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            HeroUnlockDefinitions: new List<HeroUnlockDefinition> { hero },
            HeroUnlockStates: new List<HeroUnlockState> { new(new GeneralId(6), HeroUnlockStatus.Locked) });

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Equal(HeroUnlockStatus.Unlocked, after.HeroStates.Single().Status);
        Assert.Null(after.PostingOf(new GeneralId(6)));
    }

    [Fact]
    public void v2_대기병력이_적으면_수동모집하지_않는다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), Town(9, 2, new HexCoord(12, 0)) },
            new List<General> { Gen(1) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) });

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Empty(after.Commands);
        Assert.False(after.IsGeneralBusy(new GeneralId(1)));
    }

    [Fact]
    public void AI는_안전한_성에_여유_장수가_있으면_탐색을_한번만_발행한다()
    {
        var s = new GameState(1, 1,
            new List<Faction> { new(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0") },
            new List<City> { Town(1, 1, new HexCoord(0, 0)), Town(9, 2, new HexCoord(12, 0)) },
            new List<General> { Gen(1), Gen(2) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1) });

        s = Ai().PlanWeek(s, new FactionId(1));
        Assert.Equal(new GeneralId(1), Assert.Single(s.Commands, command => command.Kind == CommandKind.Explore).Main);
        s = Ai().PlanWeek(s with { Day = 8 }, new FactionId(1));
        Assert.Single(s.Commands, command => command.Kind == CommandKind.Explore);
    }

    [Fact]
    public void AI의_기존_탐색과_플레이어_탐색을_모두_유지한다()
    {
        var state = new GameState(1, 190, [],
            [Town(1, 1, default), Town(2, 2, new(6, 0))], [Gen(1), Gen(2)],
            Postings: [At(1, 1, 1), At(2, 2, 2)]);
        var commands = new CommandService(B, Troops);
        var playerOrder = commands.Issue(state, new(new(1), CommandKind.Explore, new(1)));
        Assert.True(playerOrder.Ok, playerOrder.Error);
        var aiOrder = commands.Issue(playerOrder.State, new(new(2), CommandKind.Explore, new(2)));
        Assert.True(aiOrder.Ok, aiOrder.Error);

        var after = Ai().PlanWeek(aiOrder.State, new(2));

        Assert.Equal(2, after.Commands.Count);
        Assert.True(after.IsGeneralBusy(new(1)));
        Assert.True(after.IsGeneralBusy(new(2)));
    }

    [Fact]
    public void AI는_적_성_근처나_금이_부족한_성에서는_탐색하지_않는다()
    {
        var state = new GameState(1, 1, [],
            [Town(1, 1, new(0, 0)), Town(2, 1, new(12, 0)) with { Gold = 500 },
             Town(3, 2, new(2, 0))],
            [Gen(1), Gen(2), Gen(3), Gen(4)],
            Postings: [At(1, 1, 1), At(2, 1, 1), At(3, 1, 2), At(4, 1, 2)]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(state, new(1));

        Assert.DoesNotContain(after.Commands, c => c.Kind == CommandKind.Explore);
    }

    [Fact]
    public void AI는_금과_장수_여유가_있으면_일반연구를_결정론적으로_시작한다()
    {
        var rich = Town(1, 1, new HexCoord(0, 0)) with { Gold = 5000, Security = 60 };
        var s = new GameState(1, 1,
            [new Faction(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0")],
            [rich, Town(9, 2, new HexCoord(12, 0))],
            [Gen(1), Gen(2)],
            Postings: [At(1, 1, 1), At(2, 1, 1)]);

        var after = Ai().PlanWeek(s, new FactionId(1));

        var research = Assert.Single(after.Commands);
        Assert.Equal(CommandKind.Research, research.Kind);
        Assert.Equal(FactionResearch.PublicOrderCode, research.TroopCode);
        Assert.Equal(new GeneralId(1), research.Main);
        Assert.Equal(4800, after.Cities.Single(c => c.Id == rich.Id).Gold);
    }

    [Fact]
    public void AI는_별도연구선에서_가장낮은_계략연구를_결정론적으로_시작한다()
    {
        var rich = Town(1, 1, new HexCoord(0, 0)) with { Gold = 10_000, Security = 60 };
        var state = new GameState(1, 1,
            [new Faction(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0")],
            [rich, Town(9, 2, new HexCoord(12, 0))],
            [Gen(1), Gen(2), Gen(3)],
            Postings: [At(1, 1, 1), At(2, 1, 1), At(3, 1, 1)]);

        var after = Ai().PlanWeek(state, new FactionId(1));

        Assert.Equal(2, after.Commands.Count);
        Assert.Contains(after.Commands, command => FactionResearch.IsGeneralResearch(command.TroopCode));
        var stratagem = Assert.Single(after.Commands, command => FactionResearch.IsStratagemResearch(command.TroopCode));
        Assert.Equal(FactionResearch.ScoutStratagemCode, stratagem.TroopCode);
        Assert.Equal(new GeneralId(2), stratagem.Main);
        Assert.Equal(7800, after.Cities.Single(city => city.Id == rich.Id).Gold);
    }

    [Fact]
    public void 병력이_적으면_기존_단독_부대_출전을_하지_않는다()
    {
        var enemy = Town(9, 2, new HexCoord(10, 0));
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), enemy },
            new List<General> { Gen(1), Gen(2) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1) }, // 장수 2명(1명 남김)
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 9000, 60) });

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Empty(after.Armies);
    }

    [Fact]
    public void 출전_장수가_1명뿐이면_출전하지않고_수동모집도_하지않는다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), Town(9, 2, new HexCoord(10, 0)) },
            new List<General> { Gen(1) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 9000, 60) });

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Empty(after.Armies);
        Assert.Empty(after.Commands);
    }

    [Fact]
    public void 보급부대는_독립_출전하지_않고_공세_묶음과_동행한다()
    {
        var enemy = Town(9, 2, new HexCoord(10, 0));
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), enemy },
            new List<General> { SupplyGen(1, AptitudeGrade.C), SupplyGen(2, AptitudeGrade.S) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1) },
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 12000, 60) });

        var after = Ai(new AiConfig(SupplyDeployTarget: 10000, SupplyDeploySize: 5000)).PlanWeek(s, new FactionId(1));

        Assert.Empty(after.Armies);
    }

    [Fact]
    public void 집단군은_상대병력_오만_미만이면_편성하지_않는다()
    {
        var enemy = Town(9, 2, new HexCoord(10, 0));
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0), ore: 0) with { Provisions = 5000 }, enemy },
            new List<General> { ArmyGroupGen(1), ArmyGroupGen(2), ArmyGroupGen(3), ArmyGroupGen(4) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1), At(3, 1, 1), At(4, 1, 1) },
            GarrisonForces: new List<GarrisonForce>
            {
                new(new CityId(1), "swordsman", 20000, 60),
                new(new CityId(1), "archer", 20000, 60),
                new(new CityId(1), "catapult", 20000, 60),
                new(new CityId(9), "swordsman", 25000, 60),
            });

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(s, new FactionId(1));

        Assert.DoesNotContain(after.Armies, u => u.IsArmyGroup);
    }

    [Fact]
    public void AI는_자유장수가_네명보다_적으면_집단군을_편성하지_않는다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0), ore: 0) with { Provisions = 5000 }, Town(9, 2, new HexCoord(10, 0)) },
            new List<General> { ArmyGroupGen(1), ArmyGroupGen(2), ArmyGroupGen(3) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1), At(3, 1, 1) },
            GarrisonForces: new List<GarrisonForce>
            {
                new(new CityId(1), "swordsman", 20000, 60),
                new(new CityId(1), "archer", 20000, 60),
                new(new CityId(1), "catapult", 20000, 60),
                new(new CityId(9), "swordsman", 25000, 60),
            });

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(s, new FactionId(1));

        Assert.DoesNotContain(after.Armies, a => a.IsArmyGroup);
    }

    [Fact]
    public void 집단군은_병력_후보_필터가_불리하면_출전하지_않는다()
    {
        var enemy = Town(9, 2, new HexCoord(10, 0));
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0), ore: 0) with { Provisions = 5000 }, enemy },
            new List<General>
            {
                ArmyGroupGen(1, cavalry: AptitudeGrade.S),
                ArmyGroupGen(2, infantry: AptitudeGrade.B, archer: AptitudeGrade.B, siege: AptitudeGrade.B),
                ArmyGroupGen(3),
                ArmyGroupGen(4),
            },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1), At(3, 1, 1), At(4, 1, 1) },
            GarrisonForces: new List<GarrisonForce>
            {
                new(new CityId(1), "swordsman", 20000, 60),
                new(new CityId(1), "archer", 20000, 60),
                new(new CityId(1), "catapult", 20000, 60),
                new(new CityId(9), "swordsman", 25000, 60),
            });

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(s, new FactionId(1));

        Assert.DoesNotContain(after.Armies, u => u.IsArmyGroup);
    }

    [Fact]
    public void 재조준_멈춘_공격부대를_가장_가까운_적성으로_돌린다()
    {
        var t = Troops.First(x => x.Code == "swordsman");
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(5, 0),
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack,
            new HexCoord(99, 99), 1, t.RangeCastle); // 무효 목표
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, 8000);
        var army = new CombatUnit(field, stats, new TroopPool(8000, 0), UnitCombatState.Create(60),
            70, 60, 8000, t.Class, TroopCode: "swordsman");
        var enemy = Town(9, 2, new HexCoord(6, 0));
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), enemy },
            new List<General>(), FieldArmies: new List<CombatUnit> { army });

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Equal(enemy.Position, after.Armies.Single().Field.Target);
    }

    [Fact]
    public void AI는_유효한_공격목표와_연속좌표를_임의로_덮어쓰지_않는다()
    {
        var t = Troops.First(x => x.Code == "swordsman");
        var objective = new HexCoord(8, 0);
        var field = new FieldUnit(new UnitId(1), new FactionId(1), default,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack,
            objective, 1, t.RangeCastle) with
        {
            ContinuousTarget = RenewalHexSpace.Center(objective),
        };
        var army = new CombatUnit(field,
            CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.Plains, 8000),
            new TroopPool(8000, 0), UnitCombatState.Create(60), 70, 60, 8000,
            t.Class, TroopCode: t.Code);
        var state = new GameState(1, 190, [],
            [Town(9, 2, new HexCoord(2, 0)), Town(10, 2, objective)], [],
            FieldArmies: [army]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(state, new FactionId(1));

        Assert.Equal(objective, Assert.Single(after.Armies).Field.Target);
        Assert.Equal(field.ContinuousTarget, Assert.Single(after.Armies).Field.ContinuousTarget);
    }

    [Fact]
    public void AI는_유적을_노릴때_기존_공격명령을_탈취하지_않고_유휴부대만_배정한다()
    {
        var t = Troops.First(x => x.Code == "swordsman");
        CombatUnit MakeArmy(int id, UnitMode mode, HexCoord? target, int troops) => new(
            new FieldUnit(new UnitId(id), new FactionId(1), default,
                t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land,
                mode, target, id, t.RangeCastle),
            CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.Plains, troops),
            new TroopPool(troops, 0), UnitCombatState.Create(60), 70, 60, troops,
            t.Class, TroopCode: t.Code);
        var enemyCity = Town(9, 2, new HexCoord(8, 0));
        var busy = MakeArmy(1, UnitMode.Attack, enemyCity.Position, 20_000);
        var idle = MakeArmy(2, UnitMode.Standby, null, 20_000);
        var ruin = new RuinDefinition("r1", "극병 유적", new HexCoord(3, 0),
            "geukbyeong", 15_000);
        var state = new GameState(1, 190, [], [enemyCity], [],
            FieldArmies: [busy, idle], RuinDefinitions: [ruin],
            RuinStates: [new RuinState("r1", 15_000)]);

        var after = Ai(new AiConfig(DeployTarget: int.MaxValue)).PlanWeek(state, new FactionId(1));

        Assert.Equal(enemyCity.Position, after.Armies.Single(army => army.Id == busy.Id).Field.Target);
        Assert.Equal(ruin.Position, after.Armies.Single(army => army.Id == idle.Id).Field.Target);
    }

    [Fact]
    public void AI는_가까운_적_야전건축물을_표적으로_삼고_정찰대는_무시한다()
    {
        var t = Troops.First(x => x.Code == "swordsman");
        var field = new FieldUnit(new UnitId(1), new FactionId(1), default,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack,
            new HexCoord(99, 99), 1, t.RangeCastle);
        var army = new CombatUnit(field,
            CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.Plains, 8000),
            new TroopPool(8000, 0), UnitCombatState.Create(60), 70, 60, 8000,
            t.Class, TroopCode: t.Code);
        var scout = new FieldBuilding(new FieldBuildingId(1), "scout_post", new FactionId(2),
            new HexCoord(1, 0), 0, 0, 1, 61);
        var watchtower = new FieldBuilding(new FieldBuildingId(2), "watchtower", new FactionId(2),
            new HexCoord(3, 0), 500, 0, 1);
        var state = new GameState(1, 190, [], [Town(9, 2, new HexCoord(10, 0))], [],
            FieldArmies: [army], FieldBuildings: [scout, watchtower]);

        var after = Ai().PlanWeek(state, new FactionId(1));

        Assert.Equal(watchtower.Position, after.Armies.Single().Field.Target);
    }

    [Fact]
    public void 결정론_같은_상태는_같은_결정을_낸다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), Town(9, 2, new HexCoord(10, 0)) },
            new List<General> { Gen(1) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) });

        var a = Ai().PlanWeek(s, new FactionId(1));
        var b = Ai().PlanWeek(s, new FactionId(1));

        Assert.Equal(a.Commands.Count, b.Commands.Count);
        Assert.Empty(a.Commands);
    }
}
