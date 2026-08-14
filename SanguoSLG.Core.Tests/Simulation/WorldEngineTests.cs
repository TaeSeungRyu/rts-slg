using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

/// <summary>일 단위 세계 시계 + 월말 세금 틱(도시 금고) — design-administration "시간 축".</summary>
public class WorldEngineTests
{
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 100);

    private static GameState InitialState()
    {
        var factions = new List<Faction>
        {
            new(new FactionId(1), "위", new GeneralId(1), Gold: 1000, Color: "#2d5fd0"),
            new(new FactionId(2), "촉", new GeneralId(2), Gold: 800, Color: "#2c8c46"),
        };
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 5000, Gold: 500),
            new(new CityId(2), "업", new HexCoord(1, -1), new FactionId(1), 4200, Gold: 300),
            new(new CityId(3), "성도", new HexCoord(5, 2), new FactionId(2), 6000, Gold: 400),
        };
        return new GameState(1, 1, factions, cities, new List<General>());
    }

    private static int CityGold(GameState s, int cityId) =>
        s.Cities.Single(c => c.Id == new CityId(cityId)).Gold;

    [Fact]
    public void 달력_1일은_1년1월1일이고_360일이_지나면_2년이다()
    {
        var s = InitialState();
        Assert.Equal((1, 1, 1), (s.Year, s.Month, s.DayOfMonth));

        var engine = new WorldEngine(Balance);
        var d30 = engine.AdvanceDays(s, 29);
        Assert.Equal((1, 1, 30), (d30.Year, d30.Month, d30.DayOfMonth));

        var y2 = engine.AdvanceDays(s, 360);
        Assert.Equal((2, 1, 1), (y2.Year, y2.Month, y2.DayOfMonth));
    }

    [Fact]
    public void 월말_30일에_도시_금고로_세금이_들어온다()
    {
        var engine = new WorldEngine(Balance);

        var d29 = engine.AdvanceDays(InitialState(), 28); // 1월 29일
        Assert.Equal(500, CityGold(d29, 1));              // 아직 없음

        var d30 = engine.AdvanceDays(InitialState(), 29); // 1월 30일 — 월말 틱
        Assert.Equal(600, CityGold(d30, 1));
        Assert.Equal(400, CityGold(d30, 2));
        Assert.Equal(500, CityGold(d30, 3));
    }

    [Fact]
    public void 열두달을_돌리면_도시_세금이_12번_쌓인다()
    {
        var end = new WorldEngine(Balance).AdvanceDays(InitialState(), 360);
        Assert.Equal(500 + 12 * 100, CityGold(end, 1));
    }

    [Fact]
    public void 월말에_인구가_치안_비례로_성장한다()
    {
        // 인구 100,000·치안 100 → +1% = +1,000. 치안 50 → +0.5% = +500.
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Medium, Population: 100_000),
            new(new CityId(2), "업", new HexCoord(1, -1), new FactionId(1), 4200, CastleSize.Medium, Population: 100_000, Security: 50),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);

        Assert.Equal(101_000, after.Cities.Single(c => c.Id.Value == 1).Population);
        Assert.Equal(100_500, after.Cities.Single(c => c.Id.Value == 2).Population);
    }

    [Fact]
    public void 인구는_성곽_등급별_최대치를_넘지_않는다()
    {
        // 소성 최대 100,000 직전에서 성장해도 최대치에서 멈춘다. 대성은 500,000까지 여유.
        var cities = new List<City>
        {
            new(new CityId(1), "소성", new HexCoord(0, 0), new FactionId(1), 0, CastleSize.Small, Population: 99_900),
            new(new CityId(2), "대성", new HexCoord(1, 0), new FactionId(1), 0, CastleSize.Large, Population: 99_900),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);

        Assert.Equal(100_000, after.Cities.Single(c => c.Id.Value == 1).Population);
        Assert.Equal(100_899, after.Cities.Single(c => c.Id.Value == 2).Population);
    }

    [Fact]
    public void 나눠_진행해도_한번에_진행한_것과_같다()
    {
        var engine = new WorldEngine(Balance);
        var whole = engine.AdvanceDays(InitialState(), 90);

        var split = InitialState();
        for (var i = 0; i < 30; i++)
        {
            split = engine.AdvanceDays(split, 3);
        }

        Assert.Equal(whole.Day, split.Day);
        Assert.Equal(whole.Cities, split.Cities);
    }

    [Fact]
    public void 입력_순서가_달라도_결과와_저장순서는_동일하다()
    {
        var engine = new WorldEngine(Balance);
        var normal = InitialState();
        var reversed = normal with
        {
            Factions = normal.Factions.Reverse().ToList(),
            Cities = normal.Cities.Reverse().ToList(),
        };

        var a = engine.AdvanceDays(normal, 30);
        var b = engine.AdvanceDays(reversed, 30);

        Assert.Equal(a.Cities, b.Cities);
        Assert.Equal(new[] { 1, 2, 3 }, a.Cities.Select(c => c.Id.Value));
    }
}
