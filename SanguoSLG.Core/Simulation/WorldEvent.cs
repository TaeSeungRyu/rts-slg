namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>내정·라이프사이클 사건 종류(진행 중 발생 — 표현 계층 보고용).</summary>
public enum WorldEventKind
{
    Recruit,    // 모병 완료(병력 지급)
    Conscript,  // 징병 완료
    Train,      // 훈련 완료(훈련도 상승)
    Build,      // 건설 완공
    Research,   // 연구 완료
    Repair,     // 수리 완료
    Discord,    // 이간당함(충성 하락)
    Betray,     // 배신·재야화
    EnlistSuccess,  // 등용 성공(대상 합류)
    EnlistFail,     // 등용 실패
    EnlistCaptured, // 등용 실패 + 수행 장수 포로
}

/// <summary>
/// 진행 중 일어난 내정/라이프사이클 사건(WorldEngine이 수집 → 표현 계층이 보고 문장으로 렌더).
/// 결정론·순수 데이터. UI 문자열은 Game이 만든다(Core는 구조만).
/// </summary>
/// <param name="Faction">이 사건의 주체(또는 피해) 세력 — 플레이어 필터에 쓴다.</param>
/// <param name="General">관련 장수(수행·피해 대상 등). 없으면 null.</param>
/// <param name="City">관련 도시. 없으면 null.</param>
/// <param name="Amount">수치(병력·훈련 상승·충성 변화 등).</param>
/// <param name="Code">병종/시설 코드 등 부가 식별자(없으면 빈 문자열).</param>
public sealed record WorldEvent(
    WorldEventKind Kind,
    FactionId Faction,
    GeneralId? General = null,
    CityId? City = null,
    int Amount = 0,
    string Code = "");
