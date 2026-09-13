namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>출전 — 대기 병력+장수 → 야전 부대(군량 휴대·훈련 게이트·장수 출전/복귀). 10b.</summary>
public class DeployServiceTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static readonly IReadOnlyList<ActiveSkill> Actives =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());

    private static readonly IReadOnlyList<PassiveSkill> Passives =
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());

    private static DeployService Service() => new(B, Troops, Actives, Passives);

    private static General Gen(int id) => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
        Might: 70, Intellect: 60, Politics: 80);

    private static City Town(int id, HexCoord pos, int provisions = 5000) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(1), provisions, CastleSize.Medium,
            Gold: 1000, Population: 100_000, Ore: 50_000);

    private static GameState State(
        IEnumerable<City> cities, IEnumerable<General> generals,
        IEnumerable<GarrisonForce>? garrisons = null, IEnumerable<GeneralPosting>? postings = null,
        IEnumerable<FactionAlliance>? alliances = null,
        IEnumerable<FactionResearch>? research = null) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList(),
            Postings: postings?.ToList(), GarrisonForces: garrisons?.ToList(),
            FactionAlliances: alliances?.ToList(), ResearchTracks: research?.ToList());

    private static GeneralPosting At(int general, int city) =>
        new(new GeneralId(general), new FactionId(1), new CityId(city));

    private static readonly IReadOnlyList<AdminSkill> AdminSkills =
        new AdminSkillLoader().LoadFromDirectory(TestData.DataDirectory());

    private static General Quartermaster(int id, int tier) => Gen(id) with
    {
        AdminPassives = new[] { new GeneralSkill("quartermaster", tier) },
    };

    [Fact]
    public void 병참_선봉이면_부대_군량소모_계수가_줄어든다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000);
        var deployer = new DeployService(B, Troops, Actives, Passives, AdminSkills);
        var s0 = State([city], [Quartermaster(1, 3)], // 병참 T3 = 소모 −15%
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)], postings: [At(1, 1)]);

        var r = deployer.Deploy(s0, new DeployRequest(
            new CityId(1), "swordsman", 10000, new GeneralId(1), Target: new HexCoord(6, 0)));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(85, r.State.Armies.Single().SupplyUpkeepPercent); // 100 − 15
    }

    [Fact]
    public void 병참이_없으면_소모계수는_100이다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000);
        var deployer = new DeployService(B, Troops, Actives, Passives, AdminSkills);
        var s0 = State([city], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)], postings: [At(1, 1)]);

        var r = deployer.Deploy(s0, new DeployRequest(
            new CityId(1), "swordsman", 10000, new GeneralId(1), Target: new HexCoord(6, 0)));

        Assert.Equal(100, r.State.Armies.Single().SupplyUpkeepPercent);
    }

    [Fact]
    public void 출전_대기병력과_군량을_꺼내_야전부대를_만든다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000);
        var s0 = State([city], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(
            new CityId(1), "swordsman", 10000, new GeneralId(1), Target: new HexCoord(6, 0)));

        Assert.True(r.Ok, r.Error);
        var u = r.State.Armies.Single();
        Assert.Equal(10000, u.Pool.Active);
        Assert.Equal("swordsman", u.TroopCode);
        Assert.Equal(60, u.Training);
        Assert.Equal(new GeneralId(1), u.VanguardId);
        Assert.Equal(city.Position, u.Field.Position);
        Assert.Equal(300, u.Provisions);
        Assert.Equal(4700, r.State.Cities.Single().Provisions);
        Assert.Empty(r.State.Garrisons);
        Assert.Null(r.State.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 출전_동맹_세력의_성은_공격할수없다()
    {
        var mine = Town(1, new HexCoord(2, 0), provisions: 5000);
        var ally = new City(new CityId(2), "ally", new HexCoord(6, 0), new FactionId(2), 3000);
        var s0 = State([mine, ally], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)],
            alliances: [FactionAlliance.Create(new FactionId(1), new FactionId(2), startDay: 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 10000, new GeneralId(1),
            Mode: UnitMode.Attack, Target: ally.Position));

        Assert.False(r.Ok);
        Assert.Contains("동맹", r.Error);
    }

    [Fact]
    public void 출전_담당자로_지정된_장수는_담당에서_해제된다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000) with
        {
            DomesticOfficer = new GeneralId(1),
            TrainingOfficer = new GeneralId(2),
        };
        var s0 = State([city], [Gen(1), Gen(2)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1), At(2, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(
            new CityId(1), "swordsman", 10000, new GeneralId(1), new GeneralId(2)));

        Assert.True(r.Ok, r.Error);
        var changed = r.State.Cities.Single();
        Assert.Null(changed.DomesticOfficer);
        Assert.Null(changed.TrainingOfficer);
    }

    [Fact]
    public void 출전_병력담당자가_나가면_자동생산_설정도_비운다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000) with
        {
            RecruitmentOfficer = new GeneralId(1),
            AutoRecruitTroopCode = "cavalry",
            AutoRecruitTroopCodes = "cavalry,archer",
        };
        var s0 = State([city], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(
            new CityId(1), "swordsman", 10000, new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        var changed = r.State.Cities.Single();
        Assert.Null(changed.RecruitmentOfficer);
        Assert.Equal(string.Empty, changed.AutoRecruitTroopCode);
        Assert.Equal(string.Empty, changed.AutoRecruitTroopCodes);
    }

    [Fact]
    public void 보급부대_출전_담당자로_지정된_장수는_담당에서_해제된다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000) with
        {
            SecurityOfficer = new GeneralId(1),
        };
        var s0 = State([city], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)]);

        var r = Service().DeploySupply(s0, new SupplyDeployRequest(
            new CityId(1), [new SupplyLine("swordsman", 5000)], new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        Assert.Null(r.State.Cities.Single().SecurityOfficer);
    }

    [Fact]
    public void 출전_군량_요청량을_지정하면_그만큼만_휴대한다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000);
        var s0 = State([city], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)], postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 10000, new GeneralId(1), Provisions: 120));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(120, r.State.Armies.Single().Provisions);   // 요청량만큼만
        Assert.Equal(4880, r.State.Cities.Single().Provisions);  // 성 비축에서 그만큼만 뺀다
    }

    [Fact]
    public void 출전_군량_적재상한을_넘겨_요청해도_상한까지만_휴대한다()
    {
        var city = Town(1, new HexCoord(2, 0), provisions: 5000);
        var s0 = State([city], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)], postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 10000, new GeneralId(1), Provisions: 99999));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(300, r.State.Armies.Single().Provisions); // 적재 상한(300)까지만
    }

    [Fact]
    public void 출전_병력_일부만_데려가면_나머지는_대기한다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 6000, new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(6000, r.State.Armies.Single().Pool.Active);
        Assert.Equal(4000, r.State.Garrisons.Single().Troops);
    }

    [Fact]
    public void 출전_훈련도가_기준미만이면_거부된다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 40)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 5000, new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("훈련도", r.Error);
    }

    [Fact]
    public void 출전_대기병력을_초과하면_거부된다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 3000, 60)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 5000, new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("부족", r.Error);
    }

    [Fact]
    public void 수송_병력과_금군량을_꺼내_행군부대를_만든다()
    {
        var source = Town(1, new HexCoord(0, 0), provisions: 3000) with { Gold = 900 };
        var destination = Town(2, new HexCoord(6, 0), provisions: 1000) with { Gold = 100 };
        var s0 = State([source, destination], [Gen(1)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 20000, 70),
                new GarrisonForce(new CityId(1), "archer", 10000, 60),
            ],
            postings: [At(1, 1)]);

        var r = Service().DeployTransport(s0, new TransportDeployRequest(
            new CityId(1),
            [new TransportLine("swordsman", 12000), new TransportLine("archer", 5000)],
            new CityId(2),
            new GeneralId(1),
            Gold: 400,
            Provisions: 800));

        Assert.True(r.Ok, r.Error);
        var unit = r.State.Armies.Single();
        Assert.True(unit.IsTransport);
        Assert.False(unit.CanInitiateCombat);
        Assert.Equal(UnitMode.March, unit.Field.Mode);
        Assert.Equal(2, unit.Field.Speed);
        Assert.Equal(destination.Position, unit.Field.Target);
        Assert.Equal(new GeneralId(1), unit.VanguardId);
        Assert.Equal(17_000, unit.Pool.Active);
        Assert.Equal(400, unit.CargoGold);
        Assert.Equal(800, unit.Provisions);
        Assert.Equal("transport", unit.TroopCode);
        Assert.Equal(500, r.State.Cities.Single(c => c.Id == source.Id).Gold);
        Assert.Equal(2200, r.State.Cities.Single(c => c.Id == source.Id).Provisions);
        Assert.Equal(8000, r.State.Garrisons.Single(g => g.TroopCode == "swordsman").Troops);
        Assert.Equal(5000, r.State.Garrisons.Single(g => g.TroopCode == "archer").Troops);
        Assert.Null(r.State.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 수송_병력은_최대_오만명까지만_편성된다()
    {
        var source = Town(1, new HexCoord(0, 0), provisions: 3000) with { Gold = 900 };
        var destination = Town(2, new HexCoord(6, 0), provisions: 1000);
        var s0 = State([source, destination], [Gen(1)],
            postings: [At(1, 1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 60000, 70)]);

        var r = Service().DeployTransport(s0, new TransportDeployRequest(
            new CityId(1), [new TransportLine("swordsman", 50001)], new CityId(2), new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("50000", r.Error);
    }

    [Fact]
    public void 수송_보유한_금군량을_초과하면_거부된다()
    {
        var source = Town(1, new HexCoord(0, 0), provisions: 300) with { Gold = 100 };
        var destination = Town(2, new HexCoord(6, 0), provisions: 1000);
        var s0 = State([source, destination], [Gen(1)],
            postings: [At(1, 1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 70)]);

        var tooMuchGold = Service().DeployTransport(s0, new TransportDeployRequest(
            new CityId(1), [new TransportLine("swordsman", 1000)], new CityId(2), new GeneralId(1), Gold: 101));
        var tooMuchProvisions = Service().DeployTransport(s0, new TransportDeployRequest(
            new CityId(1), [new TransportLine("swordsman", 1000)], new CityId(2), new GeneralId(1), Provisions: 301));

        Assert.False(tooMuchGold.Ok);
        Assert.Contains("금", tooMuchGold.Error);
        Assert.False(tooMuchProvisions.Ok);
        Assert.Contains("군량", tooMuchProvisions.Error);
    }

    [Fact]
    public void 수송_금군량은_병력수와_무관하게_도시_보유량까지_실을_수_있다()
    {
        var source = Town(1, new HexCoord(0, 0), provisions: 3000) with { Gold = 1200 };
        var destination = Town(2, new HexCoord(6, 0), provisions: 1000);
        var s0 = State([source, destination], [Gen(1)],
            postings: [At(1, 1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 70)]);

        var r = Service().DeployTransport(s0, new TransportDeployRequest(
            new CityId(1),
            [new TransportLine("swordsman", 1000)],
            new CityId(2),
            new GeneralId(1),
            Gold: 1200,
            Provisions: 3000));

        Assert.True(r.Ok, r.Error);
        var unit = r.State.Armies.Single();
        Assert.Equal(1000, unit.Pool.Active);
        Assert.Equal(1200, unit.CargoGold);
        Assert.Equal(3000, unit.Provisions);
        Assert.Equal(0, r.State.Cities.Single(c => c.Id == source.Id).Gold);
        Assert.Equal(0, r.State.Cities.Single(c => c.Id == source.Id).Provisions);
    }

    [Fact]
    public void 수송_목표가_없으면_출발하지_않는다()
    {
        var source = Town(1, new HexCoord(0, 0), provisions: 3000) with { Gold = 1200 };
        var destination = Town(2, new HexCoord(6, 0), provisions: 1000);
        var s0 = State([source, destination], [Gen(1)],
            postings: [At(1, 1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 70)]);

        var r = Service().DeployTransport(s0, new TransportDeployRequest(
            new CityId(1),
            [new TransportLine("swordsman", 1000)],
            null,
            new GeneralId(1),
            Gold: 100,
            Provisions: 100));

        Assert.False(r.Ok);
        Assert.Contains("목표", r.Error);
        Assert.Empty(r.State.Armies);
    }

    [Fact]
    public void 출전_일반부대는_최대_일만명까지만_편성된다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 50000, 60)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 15000, new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("최대", r.Error);
    }

    [Fact]
    public void 출전_통솔병력_연구단계만큼_일반부대_최대편성이_늘어난다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 50000, 60)],
            postings: [At(1, 1)],
            research: [new FactionResearch(new FactionId(1), FactionResearch.CommandTroopsCode, 1)]);

        var ok = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 11000, new GeneralId(1)));
        var blocked = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 11001, new GeneralId(1)));

        Assert.True(ok.Ok, ok.Error);
        Assert.Equal(11000, ok.State.Armies.Single().Pool.Active);
        Assert.False(blocked.Ok);
        Assert.Contains("11000", blocked.Error);
    }

    [Fact]
    public void 집단군_보병궁병공성_최소편성을_만족하면_삼만명까지_편성된다()
    {
        var city = Town(1, new HexCoord(0, 0), provisions: 5000);
        var s0 = State([city], [Gen(1), Gen(2)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 10000, 70),
                new GarrisonForce(new CityId(1), "archer", 10000, 60),
                new GarrisonForce(new CityId(1), "siege_tower", 10000, 80),
            ],
            postings: [At(1, 1), At(2, 1)]);

        var r = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 10000),
                new SupplyLine("archer", 10000),
                new SupplyLine("siege_tower", 10000),
            ],
            new GeneralId(1),
            new GeneralId(2),
            Target: new HexCoord(5, 0)));

        Assert.True(r.Ok, r.Error);
        var unit = r.State.Armies.Single();
        Assert.True(unit.IsArmyGroup);
        Assert.True(unit.CanInitiateCombat);
        Assert.Equal(new CityId(1), unit.OriginCity);
        Assert.Equal("army_group", unit.TroopCode);
        Assert.Equal(30000, unit.Pool.Active);
        Assert.Equal(30000, unit.MaxTroops);
        Assert.Equal(1, unit.Field.Speed);
        Assert.Equal(1, unit.Field.AttackRange);
        Assert.Equal(1, unit.Field.RangeCastle);
        Assert.Equal(10, unit.Stats.AtkStat);
        Assert.Equal(6, unit.Stats.DfStat);
        Assert.Equal(70, unit.Training);
        Assert.Equal(new GeneralId(1), unit.VanguardId);
        Assert.Equal(new GeneralId(2), unit.AdjutantId);
        Assert.Equal(3, unit.Cargo.Count);
        Assert.Empty(r.State.Garrisons);
        Assert.Equal(4100, r.State.Cities.Single().Provisions);
        Assert.Null(r.State.PostingOf(new GeneralId(1))!.Location);
        Assert.Null(r.State.PostingOf(new GeneralId(2))!.Location);
    }

    [Fact]
    public void 집단군_기본상한은_삼만명이다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 15000, 70),
                new GarrisonForce(new CityId(1), "archer", 10000, 70),
                new GarrisonForce(new CityId(1), "siege_tower", 6000, 70),
            ],
            postings: [At(1, 1)]);

        var r = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 15000),
                new SupplyLine("archer", 10000),
                new SupplyLine("siege_tower", 6000),
            ],
            new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("30000", r.Error);
    }

    [Fact]
    public void 집단군_연구를_완료하면_사만명까지_편성된다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0), provisions: 5000)], [Gen(1)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 15000, 70),
                new GarrisonForce(new CityId(1), "archer", 15000, 70),
                new GarrisonForce(new CityId(1), "siege_tower", 10001, 70),
            ],
            postings: [At(1, 1)],
            research: [new FactionResearch(new FactionId(1), FactionResearch.ArmyGroupCode, 10)]);

        var ok = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 15000),
                new SupplyLine("archer", 15000),
                new SupplyLine("siege_tower", 10000),
            ],
            new GeneralId(1)));
        var blocked = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 15000),
                new SupplyLine("archer", 15000),
                new SupplyLine("siege_tower", 10001),
            ],
            new GeneralId(1)));

        Assert.True(ok.Ok, ok.Error);
        Assert.Equal(40000, ok.State.Armies.Single().Pool.Active);
        Assert.False(blocked.Ok);
        Assert.Contains("40000", blocked.Error);
    }

    [Fact]
    public void 집단군_각_필수병과가_최소병력보다_적으면_거부된다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 20000, 70),
                new GarrisonForce(new CityId(1), "archer", 6000, 70),
                new GarrisonForce(new CityId(1), "siege_tower", 4000, 70),
            ],
            postings: [At(1, 1)]);

        var r = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 20000),
                new SupplyLine("archer", 6000),
                new SupplyLine("siege_tower", 4000),
            ],
            new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("각각 5000명", r.Error);
    }

    [Fact]
    public void 집단군은_보병궁병공성_외_병종을_편성할수없다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 10000, 70),
                new GarrisonForce(new CityId(1), "archer", 10000, 70),
                new GarrisonForce(new CityId(1), "cavalry", 10000, 70),
            ],
            postings: [At(1, 1)]);

        var r = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 10000),
                new SupplyLine("archer", 10000),
                new SupplyLine("cavalry", 10000),
            ],
            new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("보병·궁병·공성", r.Error);
    }

    [Fact]
    public void 집단군은_원점성_기준으로_동시에_하나만_보유한다()
    {
        var city = Town(1, new HexCoord(0, 0));
        var existing = new CombatUnit(
            new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(4, 0), 1, 1, 1,
                MovementDomain.Land, UnitMode.March, null, 1),
            new CombatStats(30000, 10, 6),
            new TroopPool(30000, 0),
            UnitCombatState.Create(60),
            TroopCode: "army_group",
            IsArmyGroup: true,
            OriginCity: city.Id);
        var s0 = State([city], [Gen(1)],
            garrisons:
            [
                new GarrisonForce(new CityId(1), "swordsman", 10000, 70),
                new GarrisonForce(new CityId(1), "archer", 10000, 70),
                new GarrisonForce(new CityId(1), "siege_tower", 10000, 70),
            ],
            postings: [At(1, 1)]) with { FieldArmies = [existing] };

        var r = Service().DeployArmyGroup(s0, new ArmyGroupDeployRequest(
            new CityId(1),
            [
                new SupplyLine("swordsman", 10000),
                new SupplyLine("archer", 10000),
                new SupplyLine("siege_tower", 10000),
            ],
            new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("이미", r.Error);
    }

    [Fact]
    public void 출전_내정명령에_잠긴_장수는_출전할수없다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)]);
        var locked = new CommandService(B, Troops)
            .Issue(s0, new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(1), Value: 30));
        Assert.True(locked.Ok);

        var r = Service().Deploy(locked.State, new DeployRequest(new CityId(1), "swordsman", 5000, new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("매여", r.Error);
    }

    [Fact]
    public void 출전_다른도시_주둔_장수는_거부된다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0)), Town(2, new HexCoord(9, 0))], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 2)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 5000, new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("주둔", r.Error);
    }

    [Fact]
    public void 출전_군량은_비축이_모자라면_있는만큼만_휴대한다()
    {
        var s0 = State([Town(1, new HexCoord(0, 0), provisions: 100)], [Gen(1)],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 10000, new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(100, r.State.Armies.Single().Provisions);
        Assert.Equal(0, r.State.Cities.Single().Provisions);
    }

    [Fact]
    public void 출전_부관도_함께_출전하고_스탯은_선봉기준이다()
    {
        var adjutant = new General(new GeneralId(2), "g2",
            new Dictionary<TroopClass, AptitudeGrade>(), Might: 95, Intellect: 90, Politics: 30);
        var s0 = State([Town(1, new HexCoord(0, 0))], [Gen(1), adjutant],
            garrisons: [new GarrisonForce(new CityId(1), "swordsman", 10000, 60)],
            postings: [At(1, 1), At(2, 1)]);

        var r = Service().Deploy(s0, new DeployRequest(
            new CityId(1), "swordsman", 10000, new GeneralId(1), Adjutant: new GeneralId(2)));

        Assert.True(r.Ok, r.Error);
        var u = r.State.Armies.Single();
        Assert.Equal(new GeneralId(2), u.AdjutantId);
        Assert.Equal(70, u.Might);
        Assert.Null(r.State.PostingOf(new GeneralId(2))!.Location);
    }

    [Fact]
    public void 풀사이클_모병_출전_행군_입성까지_한바퀴_돈다()
    {
        var home = Town(1, new HexCoord(2, 0));
        var dest = Town(2, new HexCoord(6, 0));
        var s = State([home, dest], [Gen(1)], postings: [At(1, 1)]);

        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100), B);
        var issue = new CommandService(B, Troops)
            .Issue(s, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(issue.Ok, issue.Error);
        s = issue.State;

        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -5, 8), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var campaign = new CampaignEngine(field, world);

        s = campaign.AdvanceWeek(s, out _);
        var recruited = s.Garrisons.Single(g => g.City == new CityId(1));
        Assert.True(recruited.Troops > 0, "모병이 정산되어 대기 병력이 생긴다");
        Assert.Equal(new CityId(1), s.PostingOf(new GeneralId(1))!.Location);

        var deploy = Service().Deploy(s, new DeployRequest(
            new CityId(1), "swordsman", 0, new GeneralId(1), Target: dest.Position));
        Assert.True(deploy.Ok, deploy.Error);
        s = deploy.State;
        Assert.Null(s.PostingOf(new GeneralId(1))!.Location);

        s = campaign.AdvanceWeek(s, out _);

        Assert.Empty(s.Armies);
        var arrived = s.Garrisons.Single(g => g.City == new CityId(2));
        Assert.Equal(("swordsman", recruited.Troops, recruited.TrainingLevel),
            (arrived.TroopCode, arrived.Troops, arrived.TrainingLevel));
        Assert.Equal(new CityId(2), s.PostingOf(new GeneralId(1))!.Location);
    }
}
