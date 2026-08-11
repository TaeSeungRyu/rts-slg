namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 전투 페이즈에서 한 공격 부대가 거는 교전(design-combat.md "전투 페이즈 발동"·"야전 다대일").
/// <see cref="Targets"/>는 명령 순번 순서 — index 0이 주대상(100%), 나머지는 60%.
/// </summary>
/// <param name="Attacker">공격 부대.</param>
/// <param name="Targets">사거리 안 적들(주대상 먼저).</param>
public sealed record UnitEngagement(UnitId Attacker, IReadOnlyList<UnitId> Targets);
