namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using Xunit;

public sealed class FieldBuildingLoaderTests
{
    [Fact]
    public void 야전건축물_정의는_확정된_5종_수치를_읽는다()
    {
        var definitions = new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());

        Assert.Equal(5, definitions.Count);
        var palisade = Assert.Single(definitions, x => x.Kind == FieldBuildingKind.Palisade);
        Assert.Equal((100, 500, 4, 7, 1),
            (palisade.GoldCost, palisade.MaxHitPoints, palisade.Defense, palisade.BuildDays, palisade.EffectRadius));

        var scout = Assert.Single(definitions, x => x.Kind == FieldBuildingKind.ScoutPost);
        Assert.Equal((10, 10, 5, 7, 2),
            (scout.GoldCost, scout.TroopCost, scout.ProvisionsCost, scout.BuildDays, scout.EffectRadius));
        Assert.False(scout.CanBeTargeted);

        var fort = Assert.Single(definitions, x => x.Kind == FieldBuildingKind.Fort);
        Assert.Equal((300, 1000, 14, 2),
            (fort.GoldCost, fort.MaxHitPoints, fort.BuildDays, fort.EffectRadius));
        Assert.True(fort.CanGarrison);

        var formation = Assert.Single(definitions, x => x.Kind == FieldBuildingKind.Formation);
        Assert.Equal((300, 500, 14, 1),
            (formation.GoldCost, formation.MaxHitPoints, formation.BuildDays, formation.EffectRadius));
        Assert.True(formation.CanGarrison);
    }

    [Fact]
    public void 중복된_야전건축물_코드는_거부한다()
    {
        const string json = """
        [
          { "code":"palisade", "name":"목책", "max_hit_points":500, "defense":4, "build_days":7, "effect_radius":1, "model_code":"a" },
          { "code":"palisade", "name":"목책2", "max_hit_points":500, "defense":4, "build_days":7, "effect_radius":1, "model_code":"b" }
        ]
        """;

        Assert.Throws<InvalidDataException>(() => new FieldBuildingLoader().LoadFromJson(json));
    }
}
