namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;

/// <summary>
/// 한 시점의 게임 전체 상태(불변). 시간은 **일(日) 단위 세계 시계** 하나로 흐른다
/// (2026-08-13 확정 — design-administration "시간 축"). 1개월 = 30일, 1년 = 360일로
/// 고정해 연·월·일은 절대 일수에서 유도한다. 상태 전이는 WorldEngine이 새 GameState를 만든다.
/// </summary>
public sealed record GameState(
    int Day,
    int StartYear,
    IReadOnlyList<Faction> Factions,
    IReadOnlyList<City> Cities,
    IReadOnlyList<General> Generals,
    IReadOnlyList<CityCommand>? PendingCommands = null)
{
    /// <summary>진행 중인 도시 명령(발행됨·미완료). 수행 장수는 완료까지 잠긴다.</summary>
    public IReadOnlyList<CityCommand> Commands => PendingCommands ?? [];

    /// <summary>이 장수가 진행 중 명령에 매여 잠겨 있는가.</summary>
    public bool IsGeneralBusy(GeneralId general) => Commands.Any(c => c.Locks(general));

    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;
    public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

    public int Year => StartYear + (Day - 1) / DaysPerYear;

    public int Month => (Day - 1) % DaysPerYear / DaysPerMonth + 1;

    public int DayOfMonth => (Day - 1) % DaysPerMonth + 1;

    /// <summary>시나리오로부터 시작 상태(시작 연도 1월 1일)를 만든다.</summary>
    public static GameState FromScenario(Scenario scenario, int startYear = 1) =>
        new(1, startYear, scenario.Factions, scenario.Cities, scenario.Generals);
}
