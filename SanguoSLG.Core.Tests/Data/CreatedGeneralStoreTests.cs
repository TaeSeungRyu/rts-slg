namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class CreatedGeneralStoreTests
{
    [Fact]
    public void 생성_장수는_새_시나리오의_영입후보에만_더해지고_기존_저장게임을_바꾸지_않는다()
    {
        var origin = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var aptitudes = new[] { "infantry", "archer", "cavalry", "elephant", "siege", "naval", "supply", "defense" }
            .ToDictionary(code => code, _ => "B");
        var entry = new CreatedGeneralRecord(new GeneralEditorRecord(10000, "신규 장수", aptitudes,
            80, 70, 60, null, [], [], 170, 0, "wei", "사용자 생성"), "female", 120,
            "res://assets/portraits/created/women3.png");
        var entries = CreatedGeneralStore.Load(CreatedGeneralStore.Save([entry]));
        var nextGame = CreatedGeneralStore.Apply(origin, entries);

        Assert.DoesNotContain(origin.Generals, general => general.Id.Value == 10000);
        Assert.Equal("female", nextGame.Generals.Single(general => general.Id.Value == 10000).Gender);
        Assert.Equal("res://assets/portraits/created/women3.png",
            nextGame.Generals.Single(general => general.Id.Value == 10000).PortraitPath);
        var hero = nextGame.HeroUnlockList.Single(candidate => candidate.General.Value == 10000);
        Assert.Equal(120, hero.RecruitPointCost);
        Assert.All(nextGame.Factions.Where(faction => nextGame.Cities.Any(city => city.Owner == faction.Id)),
            faction => Assert.True(new HeroUnlockService().IsSatisfied(GameState.FromScenario(nextGame), hero, faction.Id)));
        var newSave = SaveService.Deserialize(SaveService.Serialize(GameState.FromScenario(nextGame)));
        Assert.Equal(entry.PortraitPath,
            newSave.Generals.Single(general => general.Id.Value == 10000).PortraitPath);

        var existingSave = SaveService.Deserialize(SaveService.Serialize(GameState.FromScenario(origin)));
        Assert.DoesNotContain(existingSave.Generals, general => general.Id.Value == 10000);
        Assert.Throws<InvalidDataException>(() => CreatedGeneralStore.Apply(nextGame, entries));
        Assert.Throws<InvalidDataException>(() => CreatedGeneralStore.Save(
            [entry with { PortraitPath = "user://general-presets/portraits/../secret.png" }]));
    }
}
