namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class SiegeDefenseCommandTests
{
    private static General G(int id, string name, AptitudeGrade defense, int might, int intellect)
        => new(new GeneralId(id), name, new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Defense] = defense,
        }, might, intellect, 50);

    private static City City(int governor = 0)
        => new(new CityId(1), "성", new HexCoord(0, 0), new FactionId(2), 0,
            Governor: governor == 0 ? null : new GeneralId(governor));

    private static GeneralPosting At(int general, int faction = 2, int? city = 1)
        => new(new GeneralId(general), new FactionId(faction), city is null ? null : new CityId(city.Value));

    [Fact]
    public void 태수가_성에_있으면_수성_지휘관은_태수다()
    {
        var governor = G(10, "태수", AptitudeGrade.C, 60, 70);
        var ace = G(11, "수성명장", AptitudeGrade.S, 95, 90);
        var state = new GameState(1, 190, [], [City(10)], [governor, ace],
            Postings: [At(10), At(11)]);

        var leader = SiegeDefenseCommand.Select(state, state.Cities[0]);

        Assert.Equal(new GeneralId(10), leader.General);
        Assert.False(leader.Acting);
    }

    [Fact]
    public void 태수가_출전중이면_수성적성이_가장_높은_주둔장수가_대리한다()
    {
        var governor = G(10, "출전태수", AptitudeGrade.SSS, 100, 100);
        var low = G(11, "무력장", AptitudeGrade.B, 99, 80);
        var high = G(12, "수성장", AptitudeGrade.S, 70, 70);
        var state = new GameState(1, 190, [], [City(10)], [governor, low, high],
            Postings: [At(10, city: null), At(11), At(12)]);

        var leader = SiegeDefenseCommand.Select(state, state.Cities[0]);

        Assert.Equal(new GeneralId(12), leader.General);
        Assert.True(leader.Acting);
        Assert.Equal(AptitudeGrade.S, leader.Aptitude);
    }

    [Fact]
    public void 수성적성이_같으면_무력_지력_id순으로_대리를_고른다()
    {
        var a = G(20, "A", AptitudeGrade.A, 80, 95);
        var b = G(21, "B", AptitudeGrade.A, 90, 40);
        var c = G(22, "C", AptitudeGrade.A, 90, 80);
        var state = new GameState(1, 190, [], [City()], [a, b, c],
            Postings: [At(20), At(21), At(22)]);

        var leader = SiegeDefenseCommand.Select(state, state.Cities[0]);

        Assert.Equal(new GeneralId(22), leader.General);
    }

    [Fact]
    public void 주둔장수가_없으면_수성_지휘관이_없다()
    {
        var state = new GameState(1, 190, [], [City()], [], Postings: []);

        var leader = SiegeDefenseCommand.Select(state, state.Cities[0]);

        Assert.Null(leader.General);
        Assert.False(leader.Acting);
    }
}
