namespace SanguoSLG.Core.Tests.Data;

using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using Xunit;

public class TroopTypeLoaderTests
{
    private static readonly System.Collections.Generic.IReadOnlyList<TroopTemplate> Templates =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    [Fact]
    public void Load_병종11종을_읽는다()
    {
        Assert.Equal(11, Templates.Count);
    }

    [Fact]
    public void Load_도검병_8_8_10_보병()
    {
        var sword = Templates.Single(t => t.Code == "swordsman");
        Assert.Equal("도검병", sword.Name);
        Assert.Equal(TroopClass.Infantry, sword.Class);
        Assert.Equal((8, 8, 10), (sword.AtkUnit, sword.AtkBuilding, sword.Df));
    }

    [Fact]
    public void Load_거북선_16_18_16_해상()
    {
        var turtle = Templates.Single(t => t.Code == "turtleship");
        Assert.Equal(TroopClass.Naval, turtle.Class);
        Assert.Equal((16, 18, 16), (turtle.AtkUnit, turtle.AtkBuilding, turtle.Df));
    }

    [Fact]
    public void Load_이동스탯_속도_탐지_사거리를_읽는다()
    {
        // spec-unit.md 확정값: 기병 속도3·탐지3·사거리1, 궁병 사거리2, 투석기 속도1·사거리2
        var cav = Templates.Single(t => t.Code == "cavalry");
        Assert.Equal((3, 3, 1), (cav.MovementPerDay, cav.Detection, cav.RangeUnit));
        Assert.Equal(2, Templates.Single(t => t.Code == "archer").RangeUnit);
        var cat = Templates.Single(t => t.Code == "catapult");
        Assert.Equal((1, 1, 2, 2, 2), (cat.MovementPerDay, cat.Detection, cat.RangeUnit, cat.RangeBuilding, cat.RangeCastle));
        var sword = Templates.Single(t => t.Code == "swordsman");
        Assert.Equal((2, 2, 1), (sword.MovementPerDay, sword.Detection, sword.RangeUnit));
    }

    [Fact]
    public void Load_알수없는분류면_예외()
    {
        Assert.Throws<System.IO.InvalidDataException>(
            () => new TroopTypeLoader().LoadFromJson("""[{"code":"x","name":"x","class":"dragon","atk_unit":1,"atk_building":1,"df":1}]"""));
    }
}
