namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 세력·장수 라이프사이클 전이. 포로 시스템은 폐기되었으며 세력 소멸만 다룬다.
/// </summary>
public static class FactionLifecycle
{
    /// <summary>
    /// 세력 소멸(도시 0 — design-general-lifecycle §3): 그 세력의 모든 장수를 재야로(배속 해제),
    /// Faction 레코드 자체는 남기되 도시·장수 없는 빈 껍데기가 된다(소유 참조 안전).
    /// </summary>
    public static GameState EliminateFaction(GameState state, FactionId faction)
    {
        var postings = state.Assignments.Where(p => p.Faction != faction).ToList();
        return state with { Postings = postings };
    }

    /// <summary>도시가 하나도 없으면 세력을 소멸시킨다(변화 없으면 그대로).</summary>
    public static GameState EliminateIfNoCities(GameState state, FactionId faction)
        => state.CityCount(faction) == 0 ? EliminateFaction(state, faction) : state;
}
