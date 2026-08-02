namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;

/// <summary>
/// 한 시점의 게임 전체 상태(불변). 시간은 연/월로 표현하며 월이 곧 턴이다.
/// 상태 전이는 TurnEngine이 새 GameState를 만들어 반환한다.
/// </summary>
public sealed record GameState(
    int Year,
    int Month,
    IReadOnlyList<Faction> Factions,
    IReadOnlyList<City> Cities,
    IReadOnlyList<General> Generals)
{
    /// <summary>시나리오로부터 시작 상태(시작 연도 1월)를 만든다.</summary>
    public static GameState FromScenario(Scenario scenario, int startYear = 1) =>
        new(startYear, 1, scenario.Factions, scenario.Cities, scenario.Generals);
}
