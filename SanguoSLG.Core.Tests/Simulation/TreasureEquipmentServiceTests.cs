namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class TreasureEquipmentServiceTests
{
    private static readonly TreasureEquipmentService Service = new(
        new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()));

    private static GameState State => new(10, 190, [],
        [new City(new(1), "장안", new HexCoord(0, 0), new(1), 1000)],
        [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 70, 60),
            new General(new(2), "장비", new Dictionary<TroopClass, AptitudeGrade>(), 90, 50, 40)],
        Postings: [new(new(1), new(1), new(1)), new(new(2), new(1), new(1))],
        FactionTreasures: [new("hunt:1", "tiger_tally", "호부", new(1), 9, "hunt"),
            new("hunt:2", "tiger_fang", "호아", new(1), 9, "hunt")]);

    [Fact]
    public void 보물_9종과_장수당_한칸_장착_해제_저장을_검증한다()
    {
        Assert.Equal(9, new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()).Count);
        var equipped = Service.Equip(State, "hunt:1", new(1));
        Assert.True(equipped.Ok, equipped.Error);
        Assert.False(Service.Equip(equipped.State, "hunt:2", new(1)).Ok);
        Assert.False(Service.Equip(equipped.State, "hunt:1", new(2)).Ok);
        var restored = SaveService.Deserialize(SaveService.Serialize(equipped.State));
        Assert.Equal(new GeneralId(1), restored.Treasures.Single(x => x.Id == "hunt:1").EquippedBy);
        var unequipped = Service.Unequip(restored, "hunt:1");
        Assert.True(unequipped.Ok, unequipped.Error);
        Assert.Null(unequipped.State.Treasures.Single(x => x.Id == "hunt:1").EquippedBy);
    }

    [Fact]
    public void 야전이나_타세력_장수는_장착할수없다()
    {
        var field = State with { Postings = [new(new(1), new(1), null)] };
        Assert.False(Service.Equip(field, "hunt:1", new(1)).Ok);
        var foreign = State with { Postings = [new(new(1), new(2), new(1))] };
        Assert.False(Service.Equip(foreign, "hunt:1", new(1)).Ok);
    }

    [Fact]
    public void 장수가_다른_세력으로_이동하면_장착_보물은_원_세력_창고로_돌아온다()
    {
        var equipped = Service.Equip(State, "hunt:1", new(1)).State;
        var moved = equipped with { Postings = [new(new(1), new(2), new(1))] };
        var reconciled = TreasureEquipmentService.ReconcileOwners(moved);
        Assert.Null(reconciled.Treasures.Single(x => x.Id == "hunt:1").EquippedBy);
        Assert.Equal(new FactionId(1), reconciled.Treasures.Single(x => x.Id == "hunt:1").Faction);
    }

    [Fact]
    public void 봉황깃은_모든_적성의_A미만만_전투와_표시에서_A로_보정한다()
    {
        var general = State.Generals[0] with { Aptitudes = new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Infantry] = AptitudeGrade.D,
            [TroopClass.Archer] = AptitudeGrade.APlus,
        } };
        var feather = new FactionTreasure("phoenix:1", "phoenix_feather", "봉황 깃", new(1), 10,
            "phoenix", EquippedBy: general.Id);
        Assert.Equal(AptitudeGrade.A, TreasureAptitude.Effective(general, TroopClass.Infantry, [feather]));
        Assert.Equal(AptitudeGrade.APlus, TreasureAptitude.Effective(general, TroopClass.Archer, [feather]));
        Assert.Equal(AptitudeGrade.D, TreasureAptitude.Effective(general, TroopClass.Infantry, []));
    }
}
