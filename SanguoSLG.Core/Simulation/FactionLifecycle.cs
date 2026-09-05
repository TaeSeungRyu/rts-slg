namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 세력·장수 라이프사이클 전이. 포로 전환과 세력 소멸을 순수 함수로 모은다.
/// 결정론: 순수 계산, 난수 미사용(확률 판정은 호출부가 시드 난수로 하고 결과만 여기 넘긴다).
/// </summary>
public static class FactionLifecycle
{
    /// <summary>
    /// 장수를 <paramref name="holder"/> 세력의 포로로 만든다 — 기존 배속 해제 + 포로 목록 등록
    /// (같은 장수 중복 방지). <paramref name="origin"/>은 원 세력.
    /// </summary>
    public static GameState MakePrisoner(GameState state, GeneralId general, FactionId holder, FactionId origin)
    {
        var postings = state.Assignments.Where(p => p.General != general).ToList();
        var captives = state.Prisoners.Where(p => p.General != general)
            .Append(new Prisoner(general, holder, origin))
            .ToList();
        return state with { Postings = postings, Captives = captives };
    }

    /// <summary>
    /// 세력 소멸(도시 0 — design-general-lifecycle §3): 그 세력의 모든 장수를 재야로(배속 해제),
    /// 그 세력이 억류하던 포로도 재야로 방출(억류 세력이 사라지므로). Faction 레코드 자체는 남기되
    /// 도시·장수 없는 빈 껍데기가 된다(소유 참조 안전). 그 세력의 장수가 타 세력 포로면 억류 유지(§3 ❓ 기본).
    /// </summary>
    public static GameState EliminateFaction(GameState state, FactionId faction)
    {
        var postings = state.Assignments.Where(p => p.Faction != faction).ToList();
        var captives = state.Prisoners.Where(p => p.Holder != faction).ToList();
        return state with { Postings = postings, Captives = captives };
    }

    /// <summary>도시가 하나도 없으면 세력을 소멸시킨다(변화 없으면 그대로).</summary>
    public static GameState EliminateIfNoCities(GameState state, FactionId faction)
        => state.CityCount(faction) == 0 ? EliminateFaction(state, faction) : state;
}
