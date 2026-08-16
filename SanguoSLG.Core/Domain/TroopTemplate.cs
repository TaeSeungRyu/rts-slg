namespace SanguoSLG.Core.Domain;

/// <summary>
/// 병종 템플릿의 기본 공격·방어(design-combat.md "병종 기본 공격·방어").
/// 특수·이벤트 유닛은 이 템플릿을 상속한다. 수치는 data/troop-types.json에서 온다.
/// </summary>
/// <param name="Code">병종 코드(snake_case, 예: swordsman).</param>
/// <param name="Name">한국어 이름.</param>
/// <param name="Class">병종 분류.</param>
/// <param name="AtkUnit">유닛 공격력(유닛dmg). 정수 고정소수점 — atk 10 = "병사당 0.1".</param>
/// <param name="AtkBuilding">건물 공격력(건물dmg).</param>
/// <param name="Df">방어력(df). df 10이 기준값.</param>
/// <param name="MovementPerDay">하루 이동 칸수(1·2·3, design-movement.md). 이벤트 유닛만 0.</param>
/// <param name="Detection">탐지 범위 — 이 안에 적이 들면 공격모드가 추격한다.</param>
/// <param name="RangeUnit">유닛 대상 사거리(1=인접, 궁병·투석 등 2).</param>
/// <param name="RangeBuilding">건물 대상 사거리.</param>
/// <param name="RangeCastle">성 대상 사거리.</param>
/// <param name="ProvisionsCapacity">군량 적재능력(10k 병력 기준 ≈ 1개월치). 보급부대는 ×배수.</param>
public sealed record TroopTemplate(
    string Code,
    string Name,
    TroopClass Class,
    int AtkUnit,
    int AtkBuilding,
    int Df,
    int MovementPerDay = 2,
    int Detection = 2,
    int RangeUnit = 1,
    int RangeBuilding = 1,
    int RangeCastle = 1,
    int ProvisionsCapacity = 300);
