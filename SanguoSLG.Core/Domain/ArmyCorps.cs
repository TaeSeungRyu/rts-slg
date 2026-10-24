namespace SanguoSLG.Core.Domain;

public enum ArmyCorpsPolicy { Domestic, Combat }

/// <summary>플레이어가 도시와 항구를 맡기는 군단. 군단별 공세 판단 시계를 가진다.</summary>
public sealed record ArmyCorps(int Id, FactionId Faction, GeneralId Commander,
    IReadOnlyList<CityId> Cities, ArmyCorpsPolicy Policy, int AiNextWarDecisionDay = 0);
