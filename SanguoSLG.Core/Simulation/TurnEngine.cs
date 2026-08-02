namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 턴 루프. 월 = 턴이며, 매월 모든 세력이 FactionId 오름차순의 고정 순서로 행동한다.
/// 스켈레톤 단계의 세력 행동은 세수 징수(보유 도시 수 × 밸런스 계수)뿐이다.
/// </summary>
public sealed class TurnEngine
{
    private readonly BalanceConfig _balance;

    public TurnEngine(BalanceConfig balance) => _balance = balance;

    /// <summary>한 달(한 턴)을 진행한 새 상태를 반환한다.</summary>
    public GameState AdvanceMonth(GameState state)
    {
        // 입력 순서와 무관하게 항상 FactionId 오름차순으로 처리·저장한다(결정론).
        var factions = state.Factions
            .OrderBy(f => f.Id.Value)
            .Select(f => CollectTax(f, state.Cities))
            .ToList();

        var (year, month) = NextMonth(state.Year, state.Month);
        return state with { Year = year, Month = month, Factions = factions };
    }

    private Faction CollectTax(Faction faction, IReadOnlyList<City> cities)
    {
        var ownedCityCount = cities.Count(c => c.Owner == faction.Id);
        var income = ownedCityCount * _balance.MonthlyTaxPerCity;
        return faction.AddGold(income);
    }

    private static (int Year, int Month) NextMonth(int year, int month) =>
        month >= 12 ? (year + 1, 1) : (year, month + 1);
}
