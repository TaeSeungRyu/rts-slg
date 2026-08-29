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

    /// <summary>연구 — 세력 병종 연구 +1단계. 공방 도시 전제, 효율 능력 = 지력. 기본 30일(지력↑ 단축).</summary>
    Research,

    /// <summary>수리 — 손상된 성벽·파괴된 시설 복구. 효율 능력 = 정치. 15일. design-administration "건물 수리".</summary>
    Repair,

    /// <summary>도시 계략 — 적 도시 대상(성벽파괴·선동·정찰·방화·절취·이간). 지력 확률, 거리 비례 소요일. design-stratagem "도시 계략".</summary>
    CityStratagem,

    /// <summary>태수 임명 — 그 도시 주둔 장수를 태수로 지정(즉시·비용/기간/잠금 없음). 수입·내정 스킬·계략 방어·성 반격이 태수 능력에 연동. design-administration F.</summary>
    AppointGovernor,

    /// <summary>군사 임명 — 그 도시 주둔 장수를 군사로 지정(즉시). 지력으로 등용 성공/실패를 예측한다(신뢰도=지력%). design-general-lifecycle §6.</summary>
    AppointStrategist,

    /// <summary>등용 — 적 성 장수·출전중 적 장수·내 포로를 영입. 정치% → 대상 이탈(100−충성)% 2단계. 거리 비례 소요일. design-general-lifecycle §6.</summary>
    Enlist,
}
