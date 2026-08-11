namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>전투 페이즈 1회 정산 결과. 부대별 받은/가한 총 피해와 적용 후 병력 구성.</summary>
/// <param name="DamageTaken">부대별 받은 총 피해(공격받지 않았으면 없음).</param>
/// <param name="DamageDealt">부대별 가한 총 피해(공격하지 않았으면 없음). 다대일·행군·방어 감소가 반영된 실피해.</param>
/// <param name="Pools">부대별 정산 후 <see cref="TroopPool"/>(피해·부상 반영).</param>
public sealed record CombatPhaseResult(
    IReadOnlyDictionary<UnitId, int> DamageTaken,
    IReadOnlyDictionary<UnitId, int> DamageDealt,
    IReadOnlyDictionary<UnitId, TroopPool> Pools);
