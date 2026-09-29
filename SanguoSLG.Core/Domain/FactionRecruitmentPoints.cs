namespace SanguoSLG.Core.Domain;

/// <summary>세력 전체가 공유하는 인재 영입 포인트 잔액.</summary>
public sealed record FactionRecruitmentPoints(FactionId Faction, int Points);

/// <summary>같은 활동의 포인트가 저장/재진행 과정에서 중복 지급되지 않게 하는 원장 항목.</summary>
public sealed record RecruitmentPointGrant(FactionId Faction, string Key, int Amount);
