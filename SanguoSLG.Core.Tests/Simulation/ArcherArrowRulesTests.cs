using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

public sealed class ArcherArrowRulesTests
{
    [Theory]
    [InlineData(ArcherArrowMode.Normal, TerrainType.Plains, "cavalry", TroopClass.Cavalry, false, 100)]
    [InlineData(ArcherArrowMode.Fire, TerrainType.Forest, "swordsman", TroopClass.Infantry, false, 110)]
    [InlineData(ArcherArrowMode.Fire, TerrainType.Mountain, "deunggap", TroopClass.Infantry, false, 170)]
    [InlineData(ArcherArrowMode.Fire, TerrainType.Plains, "swordsman", TroopClass.Infantry, false, 90)]
    [InlineData(ArcherArrowMode.Fire, TerrainType.Plains, "cavalry", TroopClass.Cavalry, false, 80)]
    [InlineData(ArcherArrowMode.Fire, TerrainType.Forest, "cavalry", TroopClass.Cavalry, false, 100)]
    [InlineData(ArcherArrowMode.Fire, TerrainType.Mountain, "deunggap", TroopClass.Infantry, true, 10)]
    public void AppliesDamagePercent(ArcherArrowMode mode, TerrainType terrain,
        string targetCode, TroopClass targetClass, bool rain, int expected)
    {
        Assert.Equal(expected, ArcherArrowRules.DamagePercent(mode, TroopClass.Archer,
            terrain, targetCode, targetClass, rain));
    }

    [Fact]
    public void OtherTroopClassesIgnoreArrowMode()
    {
        Assert.Equal(100, ArcherArrowRules.DamagePercent(ArcherArrowMode.Fire,
            TroopClass.Infantry, TerrainType.Forest, "deunggap", TroopClass.Infantry, true));
    }
}
