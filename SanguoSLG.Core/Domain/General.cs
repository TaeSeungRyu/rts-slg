namespace SanguoSLG.Core.Domain;

/// <summary>
/// 무장(spec-general.md 2026-08-07 사양). 병종 분류 6종별 통솔 등급(F~SSS)과
/// 무력·지력·정치, 전투/내정 스킬(각 0~4개, 액티브는 최대 1개)을 가진 불변 값.
/// 부대에는 최대 2명(선봉·부관) — 적성은 선봉만, 스킬은 두 장수 모두 반영(design-skill.md).
/// <paramref name="Birth"/>는 출생년(음수 = 기원전), <paramref name="Region"/>은 출신 지역 코드(regions.json).
/// <paramref name="Loyalty"/>는 충성도(기본 100·장수별 시작값) — **숨은 수치**라 표현(Game) 계층은
/// 절대 실제 값을 노출하지 않고 "??"로만 표기한다. 100 = 배신 안 할 확률 100%, 초과분은 완충
/// (예: 관우 200). 급여 미지급·적 이간·포로 생활로 하락한다(design-general-lifecycle §1).
/// 스칼라라 값 동등성·결정론에 안전하다.
/// </summary>
public sealed record General(
    GeneralId Id,
    string Name,
    IReadOnlyDictionary<TroopClass, AptitudeGrade> Aptitudes,
    int Might,
    int Intellect,
    int Politics,
    string? BattleActive = null,
    IReadOnlyList<GeneralSkill>? BattlePassives = null,
    IReadOnlyList<GeneralSkill>? AdminPassives = null,
    int Birth = 0,
    string Region = "",
    string Desc = "",
    int Loyalty = 100)
{
    /// <summary>병종 분류의 통솔 등급. 정의가 없으면 F.</summary>
    public AptitudeGrade AptitudeFor(TroopClass troopClass)
        => Aptitudes.TryGetValue(troopClass, out var grade) ? grade : AptitudeGrade.F;

    /// <summary>보유 전투 패시브(없으면 빈 목록).</summary>
    public IReadOnlyList<GeneralSkill> Passives => BattlePassives ?? [];
}
