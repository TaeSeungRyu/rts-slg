namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

/// <summary>
/// 게임 시작 시나리오. 초기 세력·도시·무장과 밸런스 설정을 담는 불변 묶음.
/// data/*.json에서 로드된 결과이며, 여기서 GameState 초기값을 만든다.
/// </summary>
public sealed record Scenario(
    IReadOnlyList<Faction> Factions,
    IReadOnlyList<City> Cities,
    IReadOnlyList<General> Generals,
    BalanceConfig Balance);
