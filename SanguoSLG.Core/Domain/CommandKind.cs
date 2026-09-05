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

    /// <summary>시설 업그레이드 — 기존 시설 체력 1000→2000→5000. 장수 필요, 건설 비용과 일수 사용.</summary>
    Upgrade,

    /// <summary>세율 — 세율 변경(효율 무관). 7일.</summary>
    SetTaxRate,

    /// <summary>연구 — 세력 병종 연구 +1단계. 효율 능력 = 지력. 기본 30일(지력↑ 단축).</summary>
    Research,

    /// <summary>주력병종 선택 — 세력당 최대 2개, 한 번 선택하면 철회 불가. 즉시·비용/기간/잠금 없음.</summary>
    SelectMajorTroop,

    /// <summary>수리 — 손상된 성벽·파괴된 시설 복구. 효율 능력 = 정치. 15일. design-administration "건물 수리".</summary>
    Repair,

    /// <summary>도시 계략 — 적 도시 대상(성벽파괴·선동·정찰·방화·절취·이간). 지력 확률, 거리 비례 소요일. design-stratagem "도시 계략".</summary>
    CityStratagem,

    /// <summary>태수 임명 — 그 도시 주둔 장수를 태수로 지정(즉시·비용/기간/잠금 없음). 수입·내정 스킬·계략 방어·성 반격이 태수 능력에 연동. design-administration F.</summary>
    AppointGovernor,

    /// <summary>군사 임명 — 그 도시 주둔 장수를 군사로 지정(즉시). 지력으로 등용 성공/실패를 예측한다(신뢰도=지력%). design-general-lifecycle §6.</summary>
    AppointStrategist,

    /// <summary>v2 치안 담당 지정 — 무력으로 월말 치안 유지·회복. 즉시·비용/기간/잠금 없음.</summary>
    AppointSecurityOfficer,

    /// <summary>v2 내정 담당 지정 — 정치로 월말 금·군량 생산. 즉시·비용/기간/잠금 없음.</summary>
    AppointDomesticOfficer,

    /// <summary>v2 병력 담당 지정 — 무력으로 월말 병력 자동 생산. 즉시·비용/기간/잠금 없음.</summary>
    AppointRecruitmentOfficer,

    /// <summary>v2 훈련 담당 지정 — 무력으로 월말 대기 병력 훈련도 증가. 즉시·비용/기간/잠금 없음.</summary>
    AppointTrainingOfficer,

    /// <summary>등용 — 정찰된 적 성 장수·출전중 적 장수를 정치 확률로 영입. 거리 비례 소요일.</summary>
    Enlist,

    /// <summary>동맹 — 대상 세력에 사절을 보내 정치 확률로 동맹을 체결한다. 금 비용과 거리 비례 소요일.</summary>
    FormAlliance,

    /// <summary>동맹파기 — 대상 세력과의 동맹을 즉시 해제한다.</summary>
    BreakAlliance,
}
