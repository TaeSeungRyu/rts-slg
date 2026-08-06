using SanguoSLG.Core.Data;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Spatial;

public class TileConditionTests
{
    [Fact]
    public void 지정하지_않은_타일은_정상이다()
    {
        var conditions = new TileConditionMap();

        Assert.Equal(TileCondition.Normal, conditions.At(new HexCoord(3, -2)));
        Assert.Equal(0, conditions.DamagedCount);
    }

    [Fact]
    public void 정상으로_되돌리면_항목이_제거된다()
    {
        var conditions = new TileConditionMap();
        var tile = new HexCoord(1, 1);

        conditions.Set(tile, TileCondition.Burning);
        Assert.Equal(1, conditions.DamagedCount);

        conditions.Set(tile, TileCondition.Normal);
        Assert.Equal(0, conditions.DamagedCount);
        Assert.Equal(TileCondition.Normal, conditions.At(tile));
    }

    [Fact]
    public void 손상_타일은_결정론적_순서로_열거된다()
    {
        var conditions = new TileConditionMap();
        // 일부러 뒤섞어 넣는다 — Dictionary 순서에 의존하지 않아야 한다.
        conditions.Set(new HexCoord(2, 5), TileCondition.Ruined);
        conditions.Set(new HexCoord(-1, 0), TileCondition.Burning);
        conditions.Set(new HexCoord(2, -3), TileCondition.Ruined);

        var order = conditions.Damaged().Select(pair => pair.Key).ToList();

        Assert.Equal(
            new[] { new HexCoord(-1, 0), new HexCoord(2, -3), new HexCoord(2, 5) },
            order);
    }

    [Fact]
    public void 시나리오에서_타일_상태를_로드한다()
    {
        var scenario = new ScenarioLoader().LoadFromJson(
            factionsJson: "[]",
            citiesJson: "[]",
            generalsJson: "[]",
            balanceJson: """{ "monthly_tax_per_city": 100 }""",
            mapJson: """
            {
              "min_q": 0, "max_q": 2, "min_r": 0, "max_r": 2,
              "conditions": [
                { "state": "ruined", "q": 1, "r": 0 },
                { "state": "burning", "q": 2, "r": 1 }
              ]
            }
            """);

        Assert.Equal(TileCondition.Ruined, scenario.Conditions.At(new HexCoord(1, 0)));
        Assert.Equal(TileCondition.Burning, scenario.Conditions.At(new HexCoord(2, 1)));
        Assert.Equal(TileCondition.Normal, scenario.Conditions.At(new HexCoord(0, 0)));
    }

    [Fact]
    public void 알_수_없는_상태는_예외다()
    {
        Assert.Throws<InvalidDataException>(() => new ScenarioLoader().LoadFromJson(
            factionsJson: "[]",
            citiesJson: "[]",
            generalsJson: "[]",
            balanceJson: """{ "monthly_tax_per_city": 100 }""",
            mapJson: """
            {
              "min_q": 0, "max_q": 1, "min_r": 0, "max_r": 1,
              "conditions": [ { "state": "melted", "q": 0, "r": 0 } ]
            }
            """));
    }

    [Fact]
    public void 실제_시나리오의_손상_타일은_맵_안에_있다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());

        Assert.All(scenario.Conditions.Damaged(), pair =>
            Assert.True(scenario.Map.Contains(pair.Key), $"손상 타일 {pair.Key}이 맵 밖이다."));
    }
}
