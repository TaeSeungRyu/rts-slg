namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class GateTests
{
    [Fact]
    public void CapacityIncludesWoundedAndRejectsWholeExcessUnit()
    {
        var gate = new Gate(7, "관문", new HexCoord(1, 1), GateSize.Small,
            new FactionId(1), 2_000, 0, Garrison:
            [new GateGarrison(new UnitId(1), "infantry", 96_000, 1_000, new GeneralId(1))]);
        Assert.True(gate.CanEnter(3_000));
        Assert.False(gate.CanEnter(4_000));
        Assert.False(gate.CanEnter(2_000, 1_001));
        Assert.Equal(1_500, gate.DailyProvisionUse(3_000));
        Assert.Equal(1_501, gate.DailyProvisionUse(3_001));
        Assert.Equal(2_000, gate.MaxIntegrity);
        Assert.Equal(3_000, (gate with { Size = GateSize.Medium }).MaxIntegrity);
    }

    [Fact]
    public void PresetGateSurvivesSaveAndInitialStateSnapshot()
    {
        var preset = new ScenarioMapPreset(1, 4, 3, ["PPPP", "PPPP", "PPPP"],
        [new MapEditorSite(1, "장안", "city", "small", 0, 0),
            new MapEditorSite(9, "함곡관", "gate", "medium", 2, 1, 3)]);
        preset.Validate();
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var scenario = ScenarioMapPresetApplier.Apply(source, preset);
        var game = GameState.FromScenario(scenario);
        Assert.Equal("함곡관", Assert.Single(game.Gates).Name);
        Assert.Equal(3, game.Gates[0].Direction);
        Assert.Equal(3_000, game.Gates[0].Integrity);
        Assert.Equal("gate", game.MapPresetSnapshot!.Sites[1].Kind);
    }

    [Fact]
    public void PresetGateRejectsWaterAndOverlap()
    {
        var water = new ScenarioMapPreset(1, 3, 2, ["PPP", "PWP"],
            [new MapEditorSite(1, "수문", "gate", "small", 1, 1)]);
        Assert.Throws<InvalidDataException>(water.Validate);
        Assert.Throws<InvalidDataException>(() => (water with
        {
            Rows = ["PPP", "PPP"],
            Sites = [new MapEditorSite(1, "관문", "gate", "large", 1, 1)]
        }).Validate());
    }

    [Fact]
    public void MediumGateOccupiesTwoRotatedTiles()
    {
        var preset = new ScenarioMapPreset(1, 5, 4, Enumerable.Repeat("PPPPP", 4).ToArray(),
            [new MapEditorSite(1, "중형", "gate", "medium", 1, 1, 0),
                new MapEditorSite(2, "겹침", "gate", "small", 2, 2)]);
        Assert.Throws<InvalidDataException>(preset.Validate);
    }
}
