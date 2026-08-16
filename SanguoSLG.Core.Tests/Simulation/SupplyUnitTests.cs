namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>보급부대 10b — 혼합 편성(2만·최하 스탯)·균일 피해 분배·병력보충·입성 병종별 편입.</summary>
public class SupplyUnitTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static readonly IReadOnlyDictionary<string, TroopTemplate> T = Troops.ToDictionary(x => x.Code);

    private static DeployService Service() => new(B, Troops,
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));

    private static AdvanceOrchestrator Orchestrator() => new(
        new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -5, 8), [], [])),
        new CombatPhaseResolver(new BattleResolver(60), 70));

    private static General Gen(int id) => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
        Might: 70, Intellect: 60, Politics: 80);

    private static CombatUnit Army(int id, int owner, HexCoord pos, UnitMode mode, HexCoord? target,
        int troops = 10000, string code = "swordsman", int training = 50, int maxTroops = 0)
    {
        var t = T[code];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, mode, target, id, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, maxTroops == 0 ? troops : maxTroops, t.Class, TroopCode: code, Training: training);
    }

    private static CombatUnit Supply(int id, int owner, HexCoord pos,
        IReadOnlyList<SupplyComponent> cargo, UnitId? reinforce = null,
        UnitMode mode = UnitMode.Advance, HexCoord? target = null)
    {
        var total = cargo.Sum(c => c.Troops);
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            Speed: 1, Detection: 1, AttackRange: 1, MovementDomain.Land, mode, target, id, RangeCastle: 0);
        return new CombatUnit(field, new CombatStats(total, 8, 8), new TroopPool(total, 0),
            UnitCombatState.Create(60), 60, 60, total, TroopClass.Infantry,
            IsSupply: true, SupplyCargo: cargo, ReinforceTarget: reinforce);
    }

    // ── 편성 ──

    [Fact]
    public void 편성_혼합병종_최하스탯_속도1_적재가중()
    {
        var city = new City(new CityId(1), "성", new HexCoord(2, 0), new FactionId(1), 10000, CastleSize.Medium);
        var s0 = new GameState(1, 1, new List<Faction>(), [city], [Gen(1)],
            Postings: [new GeneralPosting(new GeneralId(1), new FactionId(1), new CityId(1))],
            GarrisonForces:
            [
                new GarrisonForce(new CityId(1), "swordsman", 12000, 60),
                new GarrisonForce(new CityId(1), "cavalry", 6000, 80),
            ]);

        var r = Service().DeploySupply(s0, new SupplyDeployRequest(new CityId(1),
            [new SupplyLine("swordsman", 12000), new SupplyLine("cavalry", 6000)], new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        var u = r.State.Armies.Single();
        Assert.True(u.IsSupply);
        Assert.Equal(18000, u.Pool.Active);
        Assert.Equal(1, u.Field.Speed);
        Assert.Equal(0, u.Field.RangeCastle);
        Assert.Equal(8, u.Stats.AtkStat);   // min(검병 8, 기병 12)
        Assert.Equal(10, u.Stats.DfStat);   // min(검병 10, 기병 12)
        Assert.Equal(100, u.Stats.AptitudePercent);
        Assert.Equal(["cavalry", "swordsman"], u.Cargo.Select(c => c.TroopCode));
        // 적재 = 가중 평균 능력(283) × 병력 비례 × 5 = 2545 — 비축(10000)이 넉넉하니 상한까지.
        Assert.Equal(2545, u.Provisions);
        Assert.Empty(r.State.Garrisons);
        Assert.Null(r.State.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 편성_총원_2만을_넘으면_거부된다()
    {
        var city = new City(new CityId(1), "성", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Large);
        var s0 = new GameState(1, 1, new List<Faction>(), [city], [Gen(1)],
            GarrisonForces: [new GarrisonForce(new CityId(1), "swordsman", 25000, 60)]);

        var r = Service().DeploySupply(s0, new SupplyDeployRequest(new CityId(1),
            [new SupplyLine("swordsman", 21000)], new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("최대", r.Error);
    }

    [Fact]
    public void 편성_훈련도_미달_병종은_거부된다()
    {
        var city = new City(new CityId(1), "성", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Medium);
        var s0 = new GameState(1, 1, new List<Faction>(), [city], [Gen(1)],
            GarrisonForces:
            [
                new GarrisonForce(new CityId(1), "swordsman", 5000, 60),
                new GarrisonForce(new CityId(1), "archer", 5000, 30),
            ]);

        var r = Service().DeploySupply(s0, new SupplyDeployRequest(new CityId(1),
            [new SupplyLine("swordsman", 5000), new SupplyLine("archer", 5000)], new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("훈련도", r.Error);
    }

    // ── 균일 피해 ──

    [Fact]
    public void 균일피해_손실이_병종_구성에_비례_분배된다()
    {
        var supply = Supply(1, 1, new HexCoord(5, 0),
            [new SupplyComponent("archer", 5000, 60), new SupplyComponent("swordsman", 10000, 60)]);
        var enemy = Army(2, 2, new HexCoord(6, 0), UnitMode.Attack, new HexCoord(5, 0));

        var turn = Orchestrator().Run([supply, enemy], maxDays: 1);

        var after = turn.Units.Single(u => u.Id.Value == 1);
        var loss = 15000 - after.Pool.Active;
        Assert.True(loss > 0, "교전 피해가 있어야 한다");
        Assert.Equal(after.Pool.Active, after.Cargo.Sum(c => c.Troops));
        var archer = after.Cargo.Single(c => c.TroopCode == "archer");
        var sword = after.Cargo.Single(c => c.TroopCode == "swordsman");
        Assert.True(archer.Troops > 0 && sword.Troops > 0, "한 병종만 갈려나가지 않는다");
        // 비례 분배: 검병(2/3)이 궁병(1/3)의 약 2배를 잃는다.
        Assert.InRange(10000 - sword.Troops, (5000 - archer.Troops) * 2 - 2, (5000 - archer.Troops) * 2 + 2);
    }

    // ── 병력보충 ──

    [Fact]
    public void 병력보충_인접_같은병종에_20퍼센트_충원한다()
    {
        var target = Army(1, 1, new HexCoord(5, 0), UnitMode.Advance, null, troops: 5000, maxTroops: 10000, training: 50);
        var supply = Supply(2, 1, new HexCoord(6, 0),
            [new SupplyComponent("swordsman", 10000, 100)], reinforce: new UnitId(1));

        var turn = Orchestrator().Run([target, supply], maxDays: 1);

        Assert.Equal(2000, turn.Reinforced[new UnitId(1)]);
        var t = turn.Units.Single(u => u.Id.Value == 1);
        Assert.Equal(7000, t.Pool.Active);
        Assert.Equal(64, t.Training); // 가중 평균 (50×5000 + 100×2000) / 7000
        var s = turn.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(8000, s.Pool.Active);
        Assert.Equal(8000, s.Cargo.Single().Troops);
    }

    [Fact]
    public void 병력보충_대상_총원을_넘길수없다()
    {
        var target = Army(1, 1, new HexCoord(5, 0), UnitMode.Advance, null, troops: 9500, maxTroops: 10000);
        var supply = Supply(2, 1, new HexCoord(6, 0),
            [new SupplyComponent("swordsman", 10000, 60)], reinforce: new UnitId(1));

        var turn = Orchestrator().Run([target, supply], maxDays: 1);

        Assert.Equal(500, turn.Reinforced[new UnitId(1)]);
        Assert.Equal(10000, turn.Units.Single(u => u.Id.Value == 1).Pool.Active);
    }

    [Fact]
    public void 병력보충_다른병종이거나_멀면_충원하지_않는다()
    {
        var archer = Army(1, 1, new HexCoord(5, 0), UnitMode.Advance, null, troops: 5000, code: "archer", maxTroops: 10000);
        var far = Army(3, 1, new HexCoord(9, 0), UnitMode.Advance, null, troops: 5000, maxTroops: 10000);
        var supply = Supply(2, 1, new HexCoord(6, 0),
            [new SupplyComponent("swordsman", 10000, 60)], reinforce: new UnitId(1));
        var supply2 = Supply(4, 1, new HexCoord(12, 0),
            [new SupplyComponent("swordsman", 10000, 60)], reinforce: new UnitId(3));

        var turn = Orchestrator().Run([archer, supply, far, supply2], maxDays: 1);

        Assert.Empty(turn.Reinforced);
    }

    // ── 입성 ──

    [Fact]
    public void 입성_보급부대는_병종별로_대기병력에_편입된다()
    {
        var home = new City(new CityId(1), "성", new HexCoord(5, 0), new FactionId(1), 0);
        var supply = Supply(1, 1, new HexCoord(3, 0),
            [new SupplyComponent("archer", 4000, 70), new SupplyComponent("swordsman", 8000, 60)],
            mode: UnitMode.March, target: new HexCoord(5, 0));
        var s = new GameState(1, 1, new List<Faction>(), [home], new List<General>(),
            FieldArmies: [supply]);

        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100));
        var after = new CampaignEngine(Orchestrator(), world).AdvanceWeek(s, out _);

        Assert.Empty(after.Armies);
        Assert.Equal(2, after.Garrisons.Count);
        Assert.Equal(("archer", 4000, 70),
            after.Garrisons[0] is { } a ? (a.TroopCode, a.Troops, a.TrainingLevel) : default);
        Assert.Equal(("swordsman", 8000, 60),
            after.Garrisons[1] is { } b ? (b.TroopCode, b.Troops, b.TrainingLevel) : default);
    }
}
