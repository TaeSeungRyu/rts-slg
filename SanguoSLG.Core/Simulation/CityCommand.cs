namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 진행 중인 도시 명령(design-administration.md "명령 실행 공통 규칙"). 발행 시점에 산출량을
/// 확정하고 자원을 예약하며, 세계 시계가 <see cref="CompletionDay"/>에 도달하면 효과가 정산된다.
/// 수행 장수(<see cref="Main"/>·<see cref="Assist"/>)는 완료까지 잠긴다.
/// </summary>
/// <param name="Amount">정산 시 적용할 산출량 — 모병·징병 병력, 훈련 상승량, 세율 값(건설은 0).</param>
/// <param name="Facility">건설 시설 종류("paddy"/"farm"/"village"/"workshop"), 그 외 빈 문자열.</param>
public sealed record CityCommand(
    CityId City,
    CommandKind Kind,
    GeneralId Main,
    GeneralId? Assist,
    int StartDay,
    int CompletionDay,
    int Amount,
    string Facility = "")
{
    /// <summary>이 명령에 이 장수가 매여 있는가(주관 또는 보좌).</summary>
    public bool Locks(GeneralId general) => Main == general || Assist == general;
}
