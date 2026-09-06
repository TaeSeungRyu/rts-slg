namespace SanguoSLG.Core.Tests.AI;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>세력 AI 최소판 — v2 기준 출전·재조준 판단이 결정론적으로 나오는지.</summary>
public class FactionAiTests
{
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
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
        Might: 70, Intellect: 60, Politics: 80);

    private static City Town(int id, int owner, HexCoord pos, int ore = 5000) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 3000, CastleSize.Medium,
            Gold: 2000, Population: 100_000, Ore: ore);

    private static GeneralPosting At(int general, int faction, int city) =>
        new(new GeneralId(general), new FactionId(faction), new CityId(city));

    [Fact]
    public void 해금된_AI_위인은_금을_내고_자동_영입한다()
    {
        var hero = new HeroUnlockDefinition(new GeneralId(6), HeroUnlockType.Region,
            Conditions: [new HeroUnlockCondition("owned_cities", 1)], RecruitGold: 900);
        var s = new GameState(1, 1,
            new List<Faction> { new(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0") },
            new List<City> { Town(1, 1, new HexCoord(0, 0)) },
            new List<General> { Gen(1), Gen(6) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            HeroUnlockDefinitions: new List<HeroUnlockDefinition> { hero },
            HeroUnlockStates: new List<HeroUnlockState> { new(new GeneralId(6), HeroUnlockStatus.Locked) });

        var after = Ai().PlanWeek(s, new FactionId(1));

        Assert.Equal(HeroUnlockStatus.Recruited, after.HeroStates.Single().Status);
        Assert.Equal((new FactionId(1), new CityId(1)), (after.PostingOf(new GeneralId(6))!.Faction, after.PostingOf(new GeneralId(6))!.Location));
        Assert.Equal(1100, after.Cities.Single().Gold);
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
    public void 출전_대기병력이_문턱이상이고_장수가_남으면_최근접_적성으로_출전한다()
    {
        var enemy = Town(9, 2, new HexCoord(10, 0));
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)), enemy },
            new List<General> { Gen(1), Gen(2) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(2, 1, 1) }, // 장수 2명(1명 남김)
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 9000, 60) });

        var after = Ai().PlanWeek(s, new FactionId(1));

        var army = Assert.Single(after.Armies);
        Assert.Equal(new FactionId(1), army.Field.Owner);
        Assert.Equal(UnitMode.Attack, army.Field.Mode);
        Assert.Equal(enemy.Position, army.Field.Target);
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
