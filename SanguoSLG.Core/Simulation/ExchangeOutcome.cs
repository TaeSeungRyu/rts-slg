namespace SanguoSLG.Core.Simulation;

/// <summary>1:1 동시 교환 1회의 결과(EngagementResolver). 피해는 방어 감소가 이미 반영된 최종값.</summary>
/// <param name="DamageToA">A가 받는 피해.</param>
/// <param name="DamageToB">B가 받는 피해.</param>
/// <param name="HealA">A가 회복한 병력.</param>
/// <param name="HealB">B가 회복한 병력.</param>
public sealed record ExchangeOutcome(int DamageToA, int DamageToB, int HealA, int HealB);
