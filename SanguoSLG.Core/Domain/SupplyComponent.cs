namespace SanguoSLG.Core.Domain;

/// <summary>
/// 보급부대의 병종별 구성(design-unit-state 1단계-보급 "혼합병종 편성"). 병력보충(같은 병종 20%)과
/// 균일 피해 분배, 입성 시 병종별 대기 병력 환원의 단위가 된다. 불변 값.
/// </summary>
public sealed record SupplyComponent(string TroopCode, int Troops, int TrainingLevel);
