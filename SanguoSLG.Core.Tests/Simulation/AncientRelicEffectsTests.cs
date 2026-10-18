namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public class AncientRelicEffectsTests
{
    [Fact]
    public void 장착자와_세력이_맞을_때만_최대_5퍼센트를_준다()
    {
        var definitions = new[]
        {
            new AncientRelicDefinition("blade", "검편", "blade_clue", "파편", "attack_percent", 5),
        };
        var state = new GameState(1, 190, [], [], [], FactionTreasures:
            [new("item", "blade", "검편", new(1), 1, "relic", EquippedBy: new(4))]);

        Assert.Equal(5, AncientRelicEffects.Bonus(state, definitions, new(4), new(1), "attack_percent"));
        Assert.Equal(0, AncientRelicEffects.Bonus(state, definitions, new(4), new(2), "attack_percent"));
        Assert.Equal(0, AncientRelicEffects.Bonus(state, definitions, new(5), new(1), "attack_percent"));
    }

    [Fact]
    public void 병종_적성_유물은_맞는_병종에서만_발동한다()
    {
        var definitions = new[]
        {
            new AncientRelicDefinition("bow", "비결", "bow_clue", "비문", "aptitude_percent", 5, "Archer"),
        };
        var state = new GameState(1, 190, [], [], [], FactionTreasures:
            [new("item", "bow", "비결", new(1), 1, "relic", EquippedBy: new(4))]);

        Assert.Equal(5, AncientRelicEffects.Bonus(state, definitions, new(4), new(1), "aptitude_percent", TroopClass.Archer));
        Assert.Equal(0, AncientRelicEffects.Bonus(state, definitions, new(4), new(1), "aptitude_percent", TroopClass.Cavalry));
    }
}
