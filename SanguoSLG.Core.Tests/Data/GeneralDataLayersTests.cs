namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class GeneralDataLayersTests
{
    private static General Origin() => new(new GeneralId(1), "원본",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.C },
        70, 80, 90, "peerless", [new GeneralSkill("guard", 1)], [new GeneralSkill("builder", 2)],
        Birth: 155, UnlockYear: 190, Region: "origin", Desc: "보존 설명");

    [Fact]
    public void 프리셋은_ID를_유지하고_편집필드만_캠페인사본에_적용한다()
    {
        var origin = Origin();
        var preset = GeneralPresetStore.Load("""
            [{"id":1,"might":99,"aptitudes":{"infantry":"S"},"battle_passives":[{"code":"guard","tier":3}]}]
            """);

        var campaign = Assert.Single(GeneralDataLayers.CreateCampaign([origin], preset));

        Assert.Equal(origin.Id, campaign.Id);
        Assert.Equal(99, campaign.Might);
        Assert.Equal(AptitudeGrade.S, campaign.AptitudeFor(TroopClass.Infantry));
        Assert.Equal(3, Assert.Single(campaign.Passives).Tier);
        Assert.Equal((origin.Intellect, origin.Politics, origin.Birth, origin.Region, origin.Desc),
            (campaign.Intellect, campaign.Politics, campaign.Birth, campaign.Region, campaign.Desc));
        Assert.Equal(70, origin.Might);
        Assert.Equal(AptitudeGrade.C, origin.AptitudeFor(TroopClass.Infantry));
    }

    [Fact]
    public void 서로다른_캠페인과_저장슬롯은_장수상태를_공유하지않는다()
    {
        var origin = Origin();
        var first = GeneralDataLayers.CreateCampaign([origin]);
        var second = GeneralDataLayers.CreateCampaign([origin]);
        var grown = first[0] with { Level = 12, Experience = 345 };
        var firstState = new GameState(10, 190, [], [], [grown]);
        var secondState = new GameState(20, 190, [], [], second);

        var firstRound = SaveService.Deserialize(SaveService.Serialize(firstState));
        var secondRound = SaveService.Deserialize(SaveService.Serialize(secondState));

        Assert.Equal(12, firstRound.Generals.Single().Level);
        Assert.Equal(1, secondRound.Generals.Single().Level);
        Assert.Equal(1, origin.Level);
        Assert.NotSame(first[0].Aptitudes, second[0].Aptitudes);
    }

    [Fact]
    public void 이전_원시_GameState_세이브도_계속_불러온다()
    {
        var legacy = System.Text.Json.JsonSerializer.Serialize(new GameState(33, 190, [], [], [Origin()]));

        var loaded = SaveService.Deserialize(legacy);

        Assert.Equal(33, loaded.Day);
        Assert.Equal(new GeneralId(1), loaded.Generals.Single().Id);
    }

    [Fact]
    public void 없는_ID나_중복_ID_프리셋은_조용히_다른장수를_바꾸지않는다()
    {
        var unknown = new GeneralPresetOverride(new GeneralId(2), Might: 99);
        var duplicate = new GeneralPresetOverride(new GeneralId(1), Might: 88);

        Assert.Throws<InvalidDataException>(() => GeneralDataLayers.CreateCampaign([Origin()], [unknown]));
        Assert.Throws<InvalidDataException>(() => GeneralDataLayers.CreateCampaign([Origin()], [duplicate, duplicate]));
    }
}
