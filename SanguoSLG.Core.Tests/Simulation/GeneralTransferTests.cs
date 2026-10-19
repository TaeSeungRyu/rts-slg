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
}
