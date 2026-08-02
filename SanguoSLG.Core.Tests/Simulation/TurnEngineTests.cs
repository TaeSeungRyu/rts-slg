using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

public class TurnEngineTests
{
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 100);

    // 위(F1): 도시 2개, 자금 1000 / 촉(F2): 도시 2개, 자금 800
    private static GameState InitialState(int startMonth = 1)
    {
        var factions = new List<Faction>
        {
            new(new FactionId(1), "위", new GeneralId(1), Gold: 1000),
            new(new FactionId(2), "촉", new GeneralId(2), Gold: 800),
        };
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 5000),
            new(new CityId(2), "업", new HexCoord(1, -1), new FactionId(1), 4200),
            new(new CityId(3), "성도", new HexCoord(5, 2), new FactionId(2), 6000),
            new(new CityId(4), "형주", new HexCoord(3, 1), new FactionId(2), 3800),
        };
        return new GameState(1, startMonth, factions, cities, new List<General>());
    }

    private static int GoldOf(GameState state, int factionId) =>
        state.Factions.Single(f => f.Id == new FactionId(factionId)).Gold;

    [Fact]
    public void AdvanceMonth_보유_도시수_곱하기_계수만큼_세수를_징수한다()
    {
        var next = new TurnEngine(Balance).AdvanceMonth(InitialState());

        Assert.Equal(1000 + 2 * 100, GoldOf(next, 1));
        Assert.Equal(800 + 2 * 100, GoldOf(next, 2));
    }

    [Fact]
    public void AdvanceMonth_다음_달로_넘어간다()
    {
        var next = new TurnEngine(Balance).AdvanceMonth(InitialState(startMonth: 5));

        Assert.Equal(1, next.Year);
        Assert.Equal(6, next.Month);
    }

    [Fact]
    public void AdvanceMonth_12월이면_익년_1월로_롤오버한다()
    {
        var next = new TurnEngine(Balance).AdvanceMonth(InitialState(startMonth: 12));

        Assert.Equal(2, next.Year);
        Assert.Equal(1, next.Month);
    }

    [Fact]
    public void AdvanceMonth_세력_입력순서가_달라도_결과는_동일하다()
    {
        var engine = new TurnEngine(Balance);
        var normal = InitialState();

        // 세력 순서를 뒤집은 상태
        var reversed = normal with { Factions = normal.Factions.Reverse().ToList() };

        var a = engine.AdvanceMonth(normal);
        var b = engine.AdvanceMonth(reversed);

        Assert.Equal(GoldOf(a, 1), GoldOf(b, 1));
        Assert.Equal(GoldOf(a, 2), GoldOf(b, 2));
        // 저장 순서도 FactionId 오름차순으로 정규화된다.
        Assert.Equal(new[] { new FactionId(1), new FactionId(2) }, a.Factions.Select(f => f.Id));
        Assert.Equal(new[] { new FactionId(1), new FactionId(2) }, b.Factions.Select(f => f.Id));
    }

    [Fact]
    public void AdvanceMonth_12개월을_돌리면_1년이_지나고_누적_세수가_반영된다()
    {
        var engine = new TurnEngine(Balance);
        var state = InitialState();

        for (var i = 0; i < 12; i++)
        {
            state = engine.AdvanceMonth(state);
        }

        Assert.Equal(2, state.Year);
        Assert.Equal(1, state.Month);
        Assert.Equal(1000 + 12 * 2 * 100, GoldOf(state, 1));
    }
}
