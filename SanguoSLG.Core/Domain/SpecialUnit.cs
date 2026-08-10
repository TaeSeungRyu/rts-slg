namespace SanguoSLG.Core.Domain;

/// <summary>
/// 특수 유닛 정의(design-combat.md "특수 유닛 추가 효과"). 기반 병종 템플릿(<see cref="BaseCode"/>)을
/// 상속하고 그 위에 판정 전환·조건부 보정을 얹는다. 수치는 data/special-units.json에서 온다.
/// (등갑병 화공 +60%는 효과 단계 처리라 여기 없음.)
/// </summary>
/// <param name="Code">특수 유닛 코드.</param>
/// <param name="Name">한국어 이름.</param>
/// <param name="BaseCode">기반 병종 템플릿 코드.</param>
/// <param name="DfOverride">df 판정 전환(등갑병 14, 왜선 4). null이면 기반값.</param>
/// <param name="BuildingAtkOverride">건물 공격 판정 전환(궁기병 6, 화랑궁병 8). null이면 기반값.</param>
/// <param name="AtkBonusAll">모든 공격 가산 퍼센트(철기병 10).</param>
/// <param name="AtkBonusBuilding">건물 공격 가산 퍼센트(남만병 10).</param>
/// <param name="AtkBonusImpassable">이동불가 지형 공격 가산 퍼센트(무당비군 10).</param>
/// <param name="AtkBonusVsClass">특정 분류 공격 시 가산(극병: 기병 10).</param>
/// <param name="AttackerBonusFromClass">이 유닛을 치는 특정 분류 공격자가 얻는 가산(극병: 궁병 10 = 받는 피해 +10%).</param>
public sealed record SpecialUnit(
    string Code,
    string Name,
    string BaseCode,
    int? DfOverride,
    int? BuildingAtkOverride,
    int AtkBonusAll,
    int AtkBonusBuilding,
    int AtkBonusImpassable,
    IReadOnlyDictionary<TroopClass, int> AtkBonusVsClass,
    IReadOnlyDictionary<TroopClass, int> AttackerBonusFromClass);
