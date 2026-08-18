namespace SanguoSLG.Core.Domain;

/// <summary>
/// 도시 명령의 종류(design-administration.md "명령 실행 공통 규칙"). 모든 명령은 수행 장수 +
/// 기간이 필요하고, 완료일에 효과가 정산된다.
/// </summary>
public enum CommandKind
{
    /// <summary>모병 — 금(자원)으로 훈련도 50 병력. 효율 능력 = 정치. 7일.</summary>
    Recruit,

    /// <summary>징병 — 무료·훈련도 0·치안 하락. 효율 능력 = 정치. 7일.</summary>
    Conscript,

    /// <summary>훈련 — 대기 병력 훈련도 상승. 효율 능력 = 무력. 7일.</summary>
    Train,

    /// <summary>건설 — 논·밭·마을·공방 1채. 효율 능력 = 정치, 전제 정치 > 70. 30일.</summary>
    Build,

    /// <summary>세율 — 세율 변경(효율 무관). 7일.</summary>
    SetTaxRate,

    /// <summary>연구 — 세력 병종 연구 +1단계. 공방 도시 전제, 효율 능력 = 지력. 기본 30일(지력↑ 단축).</summary>
    Research,

    /// <summary>수리 — 손상된 성벽·파괴된 시설 복구. 효율 능력 = 정치. 15일. design-administration "건물 수리".</summary>
    Repair,
}
