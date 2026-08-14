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
    public void 수입은_성규모_기본치에_시설_가산이_붙는다()
    {
        // 대성(금 300·군량 2000) + 마을 2(금 +100) + 논 2(군량 +600) + 밭 1(군량 +150)
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Large,
                Gold: 0, Paddies: 2, Farms: 1, Villages: 2),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);
        var city = after.Cities.Single();

        Assert.Equal(300 + 100, city.Gold);
        Assert.Equal(1000 + 2000 + 600 + 150, city.Provisions);
    }

    [Fact]
    public void 자원은_산출_도시에서만_매월_는다()
    {
        var cities = new List<City>
        {
            new(new CityId(1), "산출", new HexCoord(0, 0), new FactionId(1), 0,
                Ore: 100, Horses: 10, Elephants: 1,
                ProducesOre: true, ProducesHorses: true, ProducesElephants: true),
            new(new CityId(2), "무산출", new HexCoord(1, 0), new FactionId(1), 0,
                Ore: 100, Horses: 10, Elephants: 1),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);

        var yes = after.Cities.Single(c => c.Id.Value == 1);
        Assert.Equal((600, 110, 3), (yes.Ore, yes.Horses, yes.Elephants));

        var no = after.Cities.Single(c => c.Id.Value == 2);
        Assert.Equal((100, 10, 1), (no.Ore, no.Horses, no.Elephants));
    }

    [Fact]
    public void 세율이_수입_배율과_치안_변동을_정한다()
    {
        // 기준 20% = 1배·변동 없음 / 50%(최대) = 2.5배·치안 −10 / 10% = 0.5배·치안 +2 / 0% = 수입 없음·치안 +4
        var cities = new List<City>
        {
            new(new CityId(1), "기준", new HexCoord(0, 0), new FactionId(1), 0, Gold: 0, Security: 80),
            new(new CityId(2), "가혹", new HexCoord(1, 0), new FactionId(1), 0, Gold: 0, Security: 80, TaxRate: 50),
            new(new CityId(3), "선정", new HexCoord(2, 0), new FactionId(1), 0, Gold: 0, Security: 80, TaxRate: 10),
            new(new CityId(4), "면세", new HexCoord(3, 0), new FactionId(1), 0, Gold: 0, Security: 80, TaxRate: 0),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);
        City C(int id) => after.Cities.Single(c => c.Id.Value == id);

        Assert.Equal((100, 80), (C(1).Gold, C(1).Security));   // 소성 기본 100 × 1.0
        Assert.Equal((250, 70), (C(2).Gold, C(2).Security));   // × 2.5, −10
        Assert.Equal((50, 82), (C(3).Gold, C(3).Security));    // × 0.5, +2
        Assert.Equal((0, 84), (C(4).Gold, C(4).Security));     // × 0, +4
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
