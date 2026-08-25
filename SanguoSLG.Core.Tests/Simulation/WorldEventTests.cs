namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>진행 이벤트 리포트(WorldEngine.LastEvents) — 명령 완료·이간이 표현 계층 보고용으로 수집된다.</summary>
public class WorldEventTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    // 고정 난수(범위 무시, 값 순환) — 성공 판정·하락폭을 결정론적으로.
    private sealed class FixedRandom(params int[] values) : IRandomSource
    {
        private readonly int[] _v = values.Length > 0 ? values : new[] { 0 };
        private int _i;
        public int Next(int minInclusive, int maxExclusive) => _v[_i++ % _v.Length];
    }

    private static General Pol(int id, int politics) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: politics);

    private static City Town(int id) =>
        new(new CityId(id), $"c{id}", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Medium,
            Gold: 1000, Population: 100_000, Ore: 100_000);

    [Fact]
    public void 모병_완료가_이벤트로_수집된다()
    {
        var svc = new CommandService(B, Troops);
        var s0 = new GameState(1, 1, new List<Faction>(), new List<City> { Town(1) }, new List<General> { Pol(1, 90) });
        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(issued.Ok, issued.Error);
        var amount = issued.State.Commands.Single().Amount;

        var world = new WorldEngine(Bal, B);
        world.AdvanceDays(issued.State, B.CommandDays); // 완료까지

        var ev = Assert.Single(world.LastEvents, e => e.Kind == WorldEventKind.Recruit);
        Assert.Equal(new FactionId(1), ev.Faction);
        Assert.Equal(new CityId(1), ev.City);
        Assert.Equal("swordsman", ev.Code);
        Assert.Equal(amount, ev.Amount);
    }

    [Fact]
    public void 이간_성공이_이벤트로_수집된다()
    {
        var svc = new CommandService(B, Troops);
        var caster = new City(new CityId(1), "c1", new HexCoord(0, 0), new FactionId(1), 0, CastleSize.Medium, Gold: 1000);
        var enemy = new City(new CityId(2), "c2", new HexCoord(3, 0), new FactionId(2), 0, CastleSize.Medium, Gold: 1000);
        var s0 = new GameState(1, 1, new List<Faction>(), new List<City> { caster, enemy },
            new List<General> { Pol(1, 90), Pol(9, 50) with { Loyalty = 80 } },
            Postings: new List<GeneralPosting>
            {
                new(new GeneralId(1), new FactionId(1), new CityId(1)),
                new(new GeneralId(9), new FactionId(2), new CityId(2)),
            },
            ScoutedCities: new List<CityIntel> { new(new FactionId(1), new CityId(2)) });

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.CityStratagem, new GeneralId(1),
            Facility: "sow_discord", TargetCity: new CityId(2)));
        Assert.True(issued.Ok, issued.Error);
        var days = issued.State.Commands.Single().CompletionDay - issued.State.Day;

        var world = new WorldEngine(Bal, B, random: new FixedRandom(0, 10)); // 성공 판정 0(성공)·하락폭 10
        world.AdvanceDays(issued.State, days);

        var ev = Assert.Single(world.LastEvents, e => e.Kind == WorldEventKind.Discord);
        Assert.Equal(new FactionId(2), ev.Faction);       // 피해 세력(적)
        Assert.Equal(new GeneralId(9), ev.General);        // 이간당한 장수
        Assert.Equal(10, ev.Amount);                       // 하락폭(고정 10)
    }
}
