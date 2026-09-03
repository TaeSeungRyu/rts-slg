namespace SanguoSLG.Core.Domain;

/// <summary>
/// 세력 대표 병종. 세력당 최대 2개까지 한 번만 선택할 수 있고 철회할 수 없다.
/// 일반 병종 연구는 7레벨까지, 주력병종은 10레벨까지 연구할 수 있다.
/// </summary>
public sealed record FactionMajorTroop(FactionId Faction, string TroopCode);
