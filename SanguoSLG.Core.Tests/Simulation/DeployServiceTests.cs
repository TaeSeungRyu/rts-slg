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
        IEnumerable<FactionAlliance>? alliances = null) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList(),
            Postings: postings?.ToList(), GarrisonForces: garrisons?.ToList(),
            FactionAlliances: alliances?.ToList());

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
