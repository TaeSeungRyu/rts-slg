namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class ArmyCorpsAiTests
{
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    [Fact]
    public void 군단은_배치된_거점만_AI로_운영하고_해산하면_직접_통제로_돌아간다()
    {
        var owner = new FactionId(1);
        var general = new General(new GeneralId(1), "군단장",
            new Dictionary<TroopClass, AptitudeGrade>(), 70, 60, 80);
        var first = new City(new CityId(1), "장안", new HexCoord(0, 0), owner, 5000,
            Gold: 1000);
        var second = new City(new CityId(2), "업", new HexCoord(8, 0), owner, 5000,
            Gold: 1000);
        var initial = new GameState(1, 190,
            [new Faction(owner, "위", general.Id, 0, "#ffffff")], [first, second], [general],
            Postings: [new GeneralPosting(general.Id, owner, first.Id)]);
        var saved = ArmyCorpsService.Save(initial, null, owner, general.Id,
            [first.Id], ArmyCorpsPolicy.Domestic);
        Assert.Throws<InvalidOperationException>(() => ArmyCorpsService.Save(saved, null,
            owner, general.Id, [first.Id], ArmyCorpsPolicy.Combat));
        var restored = SaveService.Deserialize(SaveService.Serialize(saved));
        var balance = new CommandBalance();
        var ai = new FactionAI(new CommandService(balance, Troops),
            new DeployService(balance, Troops,
                new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
                new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())));
        var planned = new ArmyCorpsAI(ai).PlanWeek(restored, owner);
        Assert.Equal(second, planned.Cities.Single(city => city.Id == second.Id));
        Assert.Equal(owner, planned.Cities.Single(city => city.Id == first.Id).Owner);
        Assert.Equal(1, planned.Cities.Single(city => city.Id == first.Id).AiLastGrowthDay);
        Assert.Empty(planned.Alliances);
        Assert.Empty(ArmyCorpsService.Disband(planned, 1, owner).Corps);
    }
}
