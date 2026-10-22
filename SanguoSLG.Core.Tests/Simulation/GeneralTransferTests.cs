namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class GeneralTransferTests
{
    private static readonly CityId Source = new(1);
    private static readonly CityId Destination = new(2);
    private static readonly GeneralId Officer = new(2);

    private static GameState State() => new(1, 190,
        [new Faction(new(1), "위", new(1), 0, "red"), new Faction(new(2), "촉", new(3), 0, "blue")],
        [new City(Source, "장안", new HexCoord(0, 0), new(1), 1000, CastleSize.Small,
                Governor: Officer),
            new City(Destination, "업", new HexCoord(4, 0), new(1), 1000, CastleSize.Small),
            new City(new(3), "한중", new HexCoord(6, 0), new(2), 1000, CastleSize.Small)],
        [new General(Officer, "장수", new Dictionary<TroopClass, AptitudeGrade>(), 50, 50, 50)],
        Postings: [new GeneralPosting(Officer, new(1), Source)]);

    [Fact]
    public void 장수이동은_같은세력_다른거점만_예약하고_출발시_태수를_해제한다()
    {
        var service = new CommandService(new CommandBalance());
        var state = State();
        Assert.False(service.Issue(state, new CommandRequest(Source, CommandKind.TransferGeneral, Officer,
            TargetCity: Source)).Ok);
        Assert.False(service.Issue(state, new CommandRequest(Source, CommandKind.TransferGeneral, Officer,
            TargetCity: new CityId(3))).Ok);

        var issued = service.Issue(state, new CommandRequest(Source, CommandKind.TransferGeneral, Officer,
            TargetCity: Destination));
        Assert.True(issued.Ok, issued.Error);
        Assert.Null(issued.State.PostingOf(Officer)!.Location);
        Assert.Null(issued.State.Cities.First(c => c.Id == Source).Governor);
        Assert.Equal(2, issued.State.Commands.Single().CompletionDay - issued.State.Day);
        Assert.True(issued.State.IsGeneralBusy(Officer));
        Assert.False(service.Issue(issued.State, new CommandRequest(Source, CommandKind.TransferGeneral, Officer,
            TargetCity: Destination)).Ok);
    }

    [Fact]
    public void 장수이동은_계략과_같은_기본속도_하루3칸을_쓴다()
    {
        var state = State() with { Cities = State().Cities.Select(city => city.Id == Destination
            ? city with { Position = new HexCoord(5, 0) } : city).ToList() };
        var issued = new CommandService(new CommandBalance { CourierSpeed = 3 }).Issue(state,
            new CommandRequest(Source, CommandKind.TransferGeneral, Officer, TargetCity: Destination));
        Assert.True(issued.Ok, issued.Error);
        Assert.Equal(2, issued.State.Commands.Single().CompletionDay - state.Day);
    }

    [Fact]
    public void 장수이동은_도착일에_한번만_배속되고_함락된_목적지는_다른_우호거점으로_바뀐다()
    {
        var service = new CommandService(new CommandBalance());
        var issued = service.Issue(State(), new CommandRequest(Source, CommandKind.TransferGeneral, Officer,
            TargetCity: Destination)).State;
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), new CommandBalance());
        var firstDay = world.AdvanceDays(issued, 1);
        Assert.Null(firstDay.PostingOf(Officer)!.Location);
        var arrived = world.AdvanceDays(firstDay, 1);
        Assert.Equal(Destination, arrived.PostingOf(Officer)!.Location);
        Assert.Empty(arrived.Commands);
        Assert.Single(world.LastEvents, e => e.Kind == WorldEventKind.GeneralTransfer);

        var captured = firstDay with { Cities = firstDay.Cities.Select(c => c.Id == Destination
            ? c with { Owner = new FactionId(2) } : c).ToList() };
        var rerouted = world.AdvanceDays(captured, 1);
        Assert.Equal(Source, rerouted.PostingOf(Officer)!.Location);
    }

    [Fact]
    public void 장수이동은_계략수행중인_장수를_거절한다()
    {
        var state = State() with { PendingCommands = [new CityCommand(Source, CommandKind.CityStratagem,
            Officer, null, 1, 8, 0, Facility: "scout", TargetCity: new CityId(3))] };

        var result = new CommandService(new CommandBalance()).Issue(state,
            new CommandRequest(Source, CommandKind.TransferGeneral, Officer, TargetCity: Destination));

        Assert.False(result.Ok);
        Assert.Contains("다른 업무", result.Error);
        Assert.Equal(state, result.State);
    }

    [Fact]
    public void 장수이동은_출전중인_장수를_거절한다()
    {
        var field = new FieldUnit(new UnitId(99), new FactionId(1), new HexCoord(0, 0),
            2, 1, 1, MovementDomain.Land, UnitMode.Standby, null, 99, 1);
        var army = new CombatUnit(field, new CombatStats(1000, 10, 10),
            new TroopPool(1000, 0), UnitCombatState.Create(60), VanguardId: Officer);
        var state = State() with { FieldArmies = [army] };

        var result = new CommandService(new CommandBalance()).Issue(state,
            new CommandRequest(Source, CommandKind.TransferGeneral, Officer, TargetCity: Destination));

        Assert.False(result.Ok);
        Assert.Contains("출전", result.Error);
        Assert.Equal(state, result.State);
    }
}
