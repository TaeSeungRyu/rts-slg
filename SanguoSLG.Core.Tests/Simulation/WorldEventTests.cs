namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>진행 이벤트 리포트(WorldEngine.LastEvents) — 명령 완료가 표현 계층 보고용으로 수집된다.</summary>
public class WorldEventTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

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

}
