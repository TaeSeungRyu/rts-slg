namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class EnemyAttackAlertPolicyTests
{
    [Theory]
    [InlineData(true, "적성")]
    [InlineData(false, "소문")]
    public void 새_적군_공세는_정찰_여부에_따라_출발지를_제한한다(bool scouted, string expected)
    {
        var own = new City(new(1), "아군성", new(0, 0), new(1), 1000,
            Governor: new GeneralId(1), Strategist: new GeneralId(2));
        var enemy = new City(new(2), "적성", new(5, 0), new(2), 1000);
        var generals = Enumerable.Range(1, 3).Select(id => new General(new(id), $"장수{id}",
            new Dictionary<TroopClass, AptitudeGrade>(), 70, 70, 70)).ToArray();
        var before = new GameState(1, 190, [], [own, enemy], generals,
            Postings: generals.Select(g => new GeneralPosting(g.Id, own.Owner, own.Id)).ToArray(),
            ScoutedCities: scouted ? [new CityIntel(own.Owner, enemy.Id)] : []);
        var first = Attacker(1, enemy, own);
        var second = Attacker(2, enemy, own);
        var afterAi = before with { FieldArmies = [first, second] };

        var story = Assert.Single(EnemyAttackAlertPolicy.Detect(before, afterAi, own.Owner));

        Assert.Equal(new GeneralId(2), story.Speaker);
        Assert.Equal(own.Id, story.City);
        Assert.Equal(expected, story.Code);
        Assert.Empty(EnemyAttackAlertPolicy.Detect(afterAi, afterAi, own.Owner));
    }

    [Fact]
    public void 해당_성에_장수가_없으면_다른_아군_성_장수가_보고한다()
    {
        var own = new City(new(1), "아군성", new(0, 0), new(1), 1000);
        var reserve = new City(new(3), "후방성", new(-3, 0), new(1), 1000);
        var enemy = new City(new(2), "적성", new(5, 0), new(2), 1000);
        var general = new General(new(7), "후방장수", new Dictionary<TroopClass, AptitudeGrade>(), 70, 70, 70);
        var before = new GameState(1, 190, [], [own, reserve, enemy], [general],
            Postings: [new(general.Id, own.Owner, reserve.Id)]);
        var afterAi = before with { FieldArmies = [Attacker(1, enemy, own)] };

        Assert.Equal(general.Id, Assert.Single(EnemyAttackAlertPolicy.Detect(before, afterAi, own.Owner)).Speaker);
    }

    private static CombatUnit Attacker(int id, City origin, City target)
        => new(new FieldUnit(new(id), origin.Owner, origin.Position, 2, 2, 1,
                MovementDomain.Land, UnitMode.Attack, target.Position, id),
            new CombatStats(3000, 10, 10), new TroopPool(3000, 0),
            UnitCombatState.Create(60), OriginCity: origin.Id);
}
