using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

// 규칙 #4(결정론)를 못박는 통합 테스트.
// GameState는 record지만 내부 리스트는 참조 비교이므로, 요소별 값 비교(Assert.Equal(IEnumerable))로 검증한다.
public class DeterminismTests
{
    private static GameState Run(int turns)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var engine = new WorldEngine(scenario.Balance);
        var state = GameState.FromScenario(scenario);
        for (var i = 0; i < turns; i++)
        {
            state = engine.AdvanceMonth(state);
        }

        return state;
    }

    private static void AssertSameState(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Day, actual.Day);
        Assert.Equal(expected.Year, actual.Year);
        Assert.Equal(expected.Month, actual.Month);
        Assert.Equal(expected.Factions, actual.Factions);
        Assert.Equal(expected.Cities, actual.Cities);
        // General은 컬렉션(병종별 통솔·스킬)을 품어 record 값 비교가 참조 비교로 샌다 —
        // 가변 상태가 될 수 있는 스칼라 필드를 골라 비교한다.
        Assert.Equal(
            expected.Generals.Select(g => (g.Id, g.Name, g.Might, g.Intellect, g.Politics)),
            actual.Generals.Select(g => (g.Id, g.Name, g.Might, g.Intellect, g.Politics)));
    }

    [Fact]
    public void 같은_초기상태로_N턴을_돌리면_최종상태가_완전히_동일하다()
    {
        // 지금은 턴 로직이 난수를 소비하지 않는다. 이후 IRandomSource를 소비하게 되면
        // 이 테스트는 동일한 시드로 두 실행을 구성해야 한다.
        AssertSameState(Run(50), Run(50));
    }

    [Fact]
    public void 턴을_나눠_돌려도_한번에_돌린것과_같다()
    {
        var whole = Run(30);

        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var engine = new WorldEngine(scenario.Balance);
        var split = GameState.FromScenario(scenario);
        for (var i = 0; i < 10; i++)
        {
            split = engine.AdvanceMonth(split);
        }

        for (var i = 0; i < 20; i++)
        {
            split = engine.AdvanceMonth(split);
        }

        AssertSameState(whole, split);
    }
}
