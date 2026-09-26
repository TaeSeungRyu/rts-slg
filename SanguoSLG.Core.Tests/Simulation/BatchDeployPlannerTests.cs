namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public sealed class BatchDeployPlannerTests
{
    private static readonly CityId City = new(1);
    private static readonly IReadOnlyList<TroopTemplate> Troops =
    [
        new("swordsman", "도검병", TroopClass.Infantry, 10, 10, 10),
        new("archer", "궁병", TroopClass.Archer, 10, 10, 10),
    ];

    [Fact]
    public void 추천은_병종적성_무력_ID순으로_선봉부터_배치한다()
    {
        var generals = new[]
        {
            General(1, AptitudeGrade.A, 99, AptitudeGrade.S, 30),
            General(2, AptitudeGrade.S, 70, AptitudeGrade.B, 90),
            General(3, AptitudeGrade.S, 80, AptitudeGrade.A, 80),
            General(4, AptitudeGrade.B, 100, AptitudeGrade.S, 70),
        };
        var result = new BatchDeployPlanner().Recommend(City,
            [new(City, "swordsman", 10_000, 60), new(City, "archer", 10_000, 60)],
            generals, generals.Select(g => g.Id).ToArray(), Troops, 10_000);

        Assert.Collection(result,
            first => Assert.Equal(new GeneralId(4), first.Vanguard),
            second => Assert.Equal(new GeneralId(3), second.Vanguard));
        Assert.Equal(new GeneralId(1), result[0].Adjutant);
        Assert.Equal(new GeneralId(2), result[1].Adjutant);
    }

    [Fact]
    public void 같은조건이면_장수ID가_작은순서로_결정된다()
    {
        var generals = new[]
        {
            General(9, AptitudeGrade.S, 80, AptitudeGrade.F, 1),
            General(3, AptitudeGrade.S, 80, AptitudeGrade.F, 1),
        };
        var result = new BatchDeployPlanner().Recommend(City,
            [new(City, "swordsman", 10_000, 60)], generals,
            generals.Select(g => g.Id).ToArray(), Troops, 10_000);

        Assert.Equal(new GeneralId(3), result.Single().Vanguard);
        Assert.Equal(new GeneralId(9), result.Single().Adjutant);
    }

    [Fact]
    public void 기존예약을_제외하고_최대5개까지만_추천한다()
    {
        var generals = Enumerable.Range(1, 12)
            .Select(id => General(id, AptitudeGrade.A, 60 + id, AptitudeGrade.A, 50)).ToArray();
        var result = new BatchDeployPlanner().Recommend(City,
            [new(City, "swordsman", 70_000, 60)], generals,
            generals.Select(g => g.Id).ToArray(), Troops, 10_000,
            reservedTroops: new Dictionary<string, int> { ["swordsman"] = 10_000 });

        Assert.Equal(5, result.Count);
        Assert.All(result, draft => Assert.Equal(10_000, draft.Troops));
    }

    [Fact]
    public void 검증은_병력과_장수의_중복예약을_행별로_거부한다()
    {
        var generals = new[]
        {
            General(1, AptitudeGrade.A, 80, AptitudeGrade.A, 50),
            General(2, AptitudeGrade.A, 70, AptitudeGrade.A, 50),
        };
        var drafts = new[]
        {
            new BatchDeployDraft("swordsman", 10_000, new GeneralId(1)),
            new BatchDeployDraft("swordsman", 10_000, new GeneralId(1), new GeneralId(2)),
        };
        var result = new BatchDeployPlanner().Validate(drafts, City,
            [new(City, "swordsman", 15_000, 60)], generals.Select(g => g.Id).ToArray(), 10_000);

        Assert.False(result.Ok);
        Assert.Contains(1, result.RowErrors.Keys);
    }

    private static General General(int id, AptitudeGrade infantry, int might, AptitudeGrade archer, int politics)
        => new(new GeneralId(id), $"장수{id}", new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Infantry] = infantry,
            [TroopClass.Archer] = archer,
        }, might, 50, politics);
}
