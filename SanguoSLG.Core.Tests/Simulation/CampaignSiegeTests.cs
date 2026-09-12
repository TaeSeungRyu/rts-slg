namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>캠페인 공성(10b) — 성벽 타격·수비 손실·반격. 소유 전환·함락은 다음 단계.</summary>
public class CampaignSiegeTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static CampaignSiege Siege() =>
        new(new BattleResolver(60), T.Values.ToList());

    private static CampaignEngine Engine()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100));
        return new CampaignEngine(field, world, Siege());
    }

    private static CombatUnit Army(int id, int owner, HexCoord pos, HexCoord target,
        int troops = 10000, string code = "swordsman")
    {
        var t = T[code];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack, target, id, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, t.Class, TroopCode: code, Training: 50);
    }

    private static City Town(int id, int owner, HexCoord pos, int wall, CastleSize size = CastleSize.Medium) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 0, size, Wall: wall);

    // ── 단위 정산(CampaignSiege) ──

    [Fact]
    public void 공성_성벽이_서있으면_성벽을_깎고_반격을_받는다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0)); // 인접(사거리 1) — 반격 받음
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([sword], [city], garr);

        var ex = Assert.Single(r.Exchanges);
        Assert.True(ex.WallStanding);
        Assert.True(ex.WallDamage > 0, "성벽이 깎여야 한다");
        Assert.Equal(6000 - ex.WallDamage, r.Cities.Single().Wall);
        Assert.Equal(10000, r.Garrisons.Single().Troops); // 성벽이 버텨 수비 무손실
        Assert.True(r.Armies.Single().Pool.Active < 10000, "인접 공격 부대는 반격을 받는다");
    }

    [Fact]
    public void 공성_사거리2_공성병기는_반격을_받지않는다()
    {
        var catapult = Army(1, 1, new HexCoord(3, 0), new HexCoord(5, 0), troops: 5000, code: "catapult"); // 거리 2
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([catapult], [city], garr);

        Assert.True(r.Cities.Single().Wall < 6000, "투석기가 성벽을 깎는다");
        Assert.Equal(5000, r.Armies.Single().Pool.Active); // 반격 없음
    }

    [Fact]
    public void 공성_성벽이_무너지면_수비병력이_직격당한다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 20000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0); // 이미 붕괴
        var garr = new List<GarrisonForce>
        {
            new(new CityId(9), "swordsman", 6000, 60),
            new(new CityId(9), "archer", 4000, 60),
        };

        var r = Siege().Resolve([sword], [city], garr);

        var ex = Assert.Single(r.Exchanges);
        Assert.False(ex.WallStanding);
        Assert.True(ex.TroopDamage > 0, "붕괴 후 수비 병력이 깎인다");
        // 손실이 병종에 병력 비례 분배: 검병(6/10)·궁병(4/10) 합이 총손실과 같다.
        var remaining = r.Garrisons.Sum(g => g.Troops);
        Assert.Equal(10000 - ex.TroopDamage, remaining);
        Assert.All(r.Garrisons, g => Assert.True(g.Troops > 0 || g.Troops == 0));
    }

    [Fact]
    public void 공성_사거리밖_행군모드_부대는_공성하지_않는다()
    {
        var marching = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0)) with
        {
            Field = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0)).Field with { Mode = UnitMode.March },
        };
        var far = Army(2, 1, new HexCoord(1, 0), new HexCoord(5, 0)); // 사거리 밖
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([marching, far], [city], garr);

        Assert.Empty(r.Exchanges);
        Assert.Equal(6000, r.Cities.Single().Wall);
    }

    [Fact]
    public void 공성_빈성_붕괴수비0은_교환이_없다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0);

        var r = Siege().Resolve([sword], [city], []);

        Assert.Empty(r.Exchanges); // 함락(점거)은 다음 단계
    }

    [Fact]
    public void 공성_태수무력이_높으면_성반격이_강해진다()
    {
        City city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        // 태수 없음(반격 100%).
        var baseArmy = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var noGov = Siege().Resolve([baseArmy], [city], garr);

        // 태수 무력 100(위력 배수 140%).
        var strongArmy = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var withGov = Siege().Resolve([strongArmy], [city], garr, _ => StatScale.Percent(100));

        var lossNoGov = 10000 - noGov.Armies.Single().Pool.Active;
        var lossWithGov = 10000 - withGov.Armies.Single().Pool.Active;
        Assert.True(lossWithGov > lossNoGov, $"태수 무력 반격이 더 커야 한다: {lossWithGov} > {lossNoGov}");
    }

    // ── 캠페인 통합(CampaignEngine) ──

    [Fact]
    public void 캠페인_한주진행이면_공성이_여러번_누적된다()
    {
        var sword = Army(1, 1, new HexCoord(2, 0), new HexCoord(5, 0), troops: 20000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000, size: CastleSize.Medium);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>(),
            GarrisonForces: garr, FieldArmies: new List<CombatUnit> { sword });

        var after = Engine().AdvanceWeek(s, out _, out var sieges);

        Assert.True(sieges.Count >= 1, "한 주 동안 공성 교환이 일어난다");
        var c = after.Cities.Single();
        Assert.True(c.Wall < 6000, $"성벽이 깎였다: {c.Wall}");
        Assert.Equal(new CityId(9), c.Id);
    }

    [Fact]
    public void 캠페인_공성방어는_태수_수성적성과_패시브만_적용한다()
    {
        var attacker = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 12000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0, size: CastleSize.Medium) with { Governor = new GeneralId(10) };
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };
        var passives = new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToList();
        var governor = new General(new GeneralId(10), "태수", new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Defense] = AptitudeGrade.S,
        }, Might: 100, Intellect: 80, Politics: 70,
            BattlePassives: [new GeneralSkill("castle_defender", 3)]);
        var other = new General(new GeneralId(11), "주둔명장", new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Defense] = AptitudeGrade.SSS,
        }, Might: 100, Intellect: 100, Politics: 70,
            BattlePassives: [new GeneralSkill("turtle_stance", 3), new GeneralSkill("castle_defender", 3)]);
        var postings = new List<GeneralPosting>
        {
            new(governor.Id, city.Owner, city.Id),
            new(other.Id, city.Owner, city.Id),
        };
        var state = new GameState(1, 190, [], [city], [governor, other], Postings: postings,
            GarrisonForces: garr, FieldArmies: [attacker]);

        var withGovernorOnly = new CampaignEngine(
            new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], [])), new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)), Siege(), passives: passives)
            .AdvanceWeek(state, out _, out var siegeReports);

        var noSkillCity = city with { Governor = null };
        var noSkillState = state with { Cities = [noSkillCity], Postings = [], FieldArmies = [attacker] };
        var noSkill = new CampaignEngine(
            new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], [])), new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)), Siege(), passives: passives)
            .AdvanceWeek(noSkillState, out _, out _);

        Assert.NotEmpty(siegeReports);
        Assert.True(withGovernorOnly.Garrisons.Sum(g => g.Troops) > noSkill.Garrisons.Sum(g => g.Troops),
            "태수의 수성 적성과 성 주둔 패시브가 수비 피해를 줄여야 한다.");
    }


    [Fact]
    public void 수성액티브는_5일_충전되면_공성방어에_적용된다()
    {
        var attacker = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 12000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0, size: CastleSize.Medium) with { Governor = new GeneralId(10) };
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };
        var passives = new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToList();
        var actives = new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToList();
        var governor = new General(new GeneralId(10), "태수", new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Defense] = AptitudeGrade.C,
        }, Might: 100, Intellect: 80, Politics: 70, BattleActive: "hold_the_line");
        var state = new GameState(1, 190, [], [city], [governor], Postings: [new GeneralPosting(governor.Id, city.Owner, city.Id)],
            GarrisonForces: garr, FieldArmies: [attacker],
            DefenseCharges: [new CityDefenseCharge(city.Id, governor.Id, 4)]);

        var active = new CampaignEngine(
            new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], [])), new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)), Siege(), passives: passives, actives: actives)
            .AdvanceWeek(state, out _, out _);

        var notReady = new CampaignEngine(
            new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], [])), new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)), Siege(), passives: passives, actives: actives)
            .AdvanceWeek(state with { FieldArmies = [attacker], DefenseCharges = [new CityDefenseCharge(city.Id, governor.Id, 0)] }, out _, out _);

        Assert.True(active.Garrisons.Sum(g => g.Troops) > notReady.Garrisons.Sum(g => g.Troops),
            "5일 충전된 방어 액티브가 수비 피해를 더 줄여야 한다.");
        Assert.All(active.SiegeDefenseCharges, c => Assert.InRange(c.ChargeDays, 0, CityDefenseCharge.RequiredDays));
    }

    [Fact]
    public void 수성액티브_충전은_공격이_없으면_초기화된다()
    {
        var farBase = Army(1, 1, new HexCoord(0, 0), new HexCoord(5, 0), troops: 12000);
        var far = farBase with { Field = farBase.Field with { Mode = UnitMode.March } };
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000, size: CastleSize.Medium) with { Governor = new GeneralId(10) };
        var governor = new General(new GeneralId(10), "태수", new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Defense] = AptitudeGrade.C,
        }, Might: 80, Intellect: 80, Politics: 70, BattleActive: "hold_the_line");
        var state = new GameState(1, 190, [], [city], [governor], Postings: [new GeneralPosting(governor.Id, city.Owner, city.Id)],
            GarrisonForces: [new GarrisonForce(city.Id, "swordsman", 10000, 60)], FieldArmies: [far],
            DefenseCharges: [new CityDefenseCharge(city.Id, governor.Id, 4)]);

        var after = new CampaignEngine(
            new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], [])), new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)), Siege(),
            actives: new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToList())
            .AdvanceWeek(state, out _, out var sieges);

        Assert.Empty(sieges);
        Assert.Empty(after.SiegeDefenseCharges);
    }


    [Fact]
    public void 공성_수비손실은_일부_도시부상병으로_쌓인다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 20000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([sword], [city], garr);

        var ex = Assert.Single(r.Exchanges);
        Assert.True(ex.TroopDamage > 0);
        Assert.Equal(ex.TroopDamage * 70 / 100, r.CityWounded.Sum(w => w.Troops));
    }

    [Fact]
    public void 수성중인_성의_부상병은_회복되지_않고_공격이_끊기면_회복된다()
    {
        var attacker = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 12000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0, size: CastleSize.Medium);
        var state = new GameState(1, 190, [], [city], [],
            GarrisonForces: [new GarrisonForce(city.Id, "swordsman", 10000, 60)],
            CityWoundedForces: [new CityWoundedForce(city.Id, "swordsman", 1000, 60)],
            FieldArmies: [attacker]);

        var underSiege = Engine().AdvanceWeek(state, out _, out var sieges);
        Assert.NotEmpty(sieges);
        Assert.True(underSiege.CityWounded.Sum(w => w.Troops) >= 1000,
            "공격받은 진행에서는 기존 도시 부상병이 회복되지 않아야 한다.");

        var quiet = Engine().AdvanceWeek(underSiege with { FieldArmies = [attacker with { Field = attacker.Field with { Mode = UnitMode.March } }] }, out _, out var quietSieges);
        Assert.Empty(quietSieges);
        Assert.True(quiet.CityWounded.Sum(w => w.Troops) < underSiege.CityWounded.Sum(w => w.Troops),
            "공격이 끊긴 진행부터 도시 부상병 회복이 재개되어야 한다.");
    }

}
