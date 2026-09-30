namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class PortAdministrationTests
{
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 0,
        GoldBaseSmall: 300, ProvisionsBaseSmall: 1500,
        GoldBaseMedium: 0, GoldBaseLarge: 0,
        ProvisionsBaseMedium: 0, ProvisionsBaseLarge: 0,
        PopulationGrowthPercent: 0, SecurityNaturalRecovery: 0);
    private static readonly CommandBalance Commands = new() { AutoOfficerSystemEnabled = true };
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopCatalogLoader().LoadFromDirectory(TestData.DataDirectory());

    private static General Officer(int id, int might = 100, int politics = 80) => new(
        new GeneralId(id), $"담당{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: might, Intellect: 80, Politics: politics);

    private static City Place(int id, PortSize port, General[] officers, bool allDuties = true) => new(
        new CityId(id), $"거점{id}", new HexCoord(id * 2, 0), new FactionId(1), 1000,
        CastleSize.Small, Gold: 10_000, Security: 80,
        SecurityOfficer: allDuties ? officers[0].Id : null,
        DomesticOfficer: officers[1].Id,
        RecruitmentOfficer: allDuties ? officers[2].Id : null,
        TrainingOfficer: allDuties ? officers[3].Id : null,
        AutoRecruitTroopCodes: "swordsman", Port: port);

    [Theory]
    [InlineData(PortSize.Small, 50)]
    [InlineData(PortSize.Medium, 75)]
    public void 항구_네담당효과는_규모효율로_실결산된다(PortSize size, int percent)
    {
        var officers = new[] { Officer(1), Officer(2), Officer(3), Officer(4) };
        var port = Place(1, size, officers);
        var state = new GameState(1, 190, [], [port], officers,
            Postings: officers.Select(g => new GeneralPosting(g.Id, port.Owner, port.Id)).ToList(),
            GarrisonForces: [new(port.Id, "archer", 1000, 40)]);

        var after = new WorldEngine(Balance, Commands).AdvanceDays(state, 7);
        var result = after.Cities.Single();

        var domesticGoldMonthly = Commands.AutoDomesticGoldBase + 80 * Commands.AutoDomesticGoldPoliticsMultiplier;
        var domesticFoodMonthly = Commands.AutoDomesticProvisionsBase + 80 * Commands.AutoDomesticProvisionsPoliticsMultiplier;
        var portGold = size == PortSize.Small ? WorldEngine.PortSmallWeeklyGold : WorldEngine.PortSmallWeeklyGold * 2;
        var portFood = size == PortSize.Small ? WorldEngine.PortSmallWeeklyProvisions : WorldEngine.PortSmallWeeklyProvisions * 2;
        var recruit = (Commands.AutoRecruitTroopsBase + 100 * Commands.AutoRecruitTroopsMightMultiplier) * percent / 100;
        var recruitCost = Commands.AutoRecruitGoldCost("swordsman", recruit);
        Assert.Equal(10_000 + FirstWeeklyShare(domesticGoldMonthly * percent / 100) + portGold - recruitCost, result.Gold);
        Assert.Equal(1000 + FirstWeeklyShare(domesticFoodMonthly * percent / 100) + portFood, result.Provisions);
        Assert.Equal(recruit, after.Garrisons.Single(g => g.TroopCode == "swordsman").Troops);
        Assert.Equal(40 + 4 * percent / 100, after.Garrisons.Single(g => g.TroopCode == "archer").TrainingLevel);
        var expectedSecurity = 80 + 3 * percent / 100 + (-3 * percent / 100);
        Assert.Equal(expectedSecurity, result.Security);
    }

    [Fact]
    public void 항구는_소형성_기본수입을_중복으로_받지않는다()
    {
        var officers = new[] { Officer(1), Officer(2), Officer(3), Officer(4) };
        var city = Place(1, PortSize.None, officers, allDuties: false);
        var port = Place(2, PortSize.Small, officers, allDuties: false) with
        {
            DomesticOfficer = null,
            Gold = 0,
            Provisions = 0,
        };
        var state = new GameState(1, 190, [], [city with { DomesticOfficer = null, Gold = 0, Provisions = 0 }, port], officers);

        var after = new WorldEngine(Balance, Commands).AdvanceDays(state, 7);

        // 일반 성의 기존 수입 계산 결과와 무관하게 항구에는 전용 주간 수입만 들어와야 한다.
        Assert.True(after.Cities.Single(c => c.Id == city.Id).Gold > 0);
        Assert.True(after.Cities.Single(c => c.Id == city.Id).Provisions > 0);
        Assert.Equal(WorldEngine.PortSmallWeeklyGold, after.Cities.Single(c => c.Id == port.Id).Gold);
        Assert.Equal(WorldEngine.PortSmallWeeklyProvisions, after.Cities.Single(c => c.Id == port.Id).Provisions);
    }

    [Fact]
    public void 항구는_모든연구와_시설수리를_거부하고_성벽수리만_허용한다()
    {
        var officer = Officer(1);
        var port = new City(new CityId(1), "항구", new HexCoord(0, 0), new FactionId(1), 1000,
            CastleSize.Small, Gold: 5000, Wall: 0, Port: PortSize.Small, RuinedVillages: 1);
        var state = new GameState(1, 190, [], [port], [officer]);
        var service = new CommandService(Commands, Troops, Balance);

        var doctrine = service.Issue(state, new(port.Id, CommandKind.Research, officer.Id, TroopCode: "swordsman"));
        var general = service.Issue(state, new(port.Id, CommandKind.Research, officer.Id, TroopCode: FactionResearch.CommerceCode));
        var wallResearch = service.Issue(state, new(port.Id, CommandKind.Research, officer.Id, TroopCode: FactionResearch.WallCode));
        var major = service.Issue(state, new(port.Id, CommandKind.SelectMajorTroop, officer.Id, TroopCode: "swordsman"));
        var facilityRepair = service.Issue(state, new(port.Id, CommandKind.Repair, officer.Id, Facility: "village"));
        var wallRepair = service.Issue(state, new(port.Id, CommandKind.Repair, officer.Id, TroopCode: FactionResearch.WallCode));

        Assert.All(new[] { doctrine, general, wallResearch, major, facilityRepair }, result => Assert.False(result.Ok));
        Assert.True(wallRepair.Ok, wallRepair.Error);
        Assert.Equal(CommandKind.Repair, wallRepair.State.Commands.Single().Kind);
    }

    private static int FirstWeeklyShare(int monthly) => monthly / 4 + (monthly % 4 > 0 ? 1 : 0);
}
